#include "depth_trace.h"
#include "reshade_abi.h"
#include <d3d12.h>
#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <unordered_map>
using namespace reshade::api;
namespace {
constexpr unsigned grid=32, mcPoints=12, points=grid+mcPoints, maxSlots=96;
struct UV {float u=0,v=0;};
struct State {resource depth{0};viewport vp{};uint64_t draws=0,verts=0,indirect=0;bool pass=false;};
struct Shot {
    resource buffer{0};command_list *cmd=nullptr;command_queue *queue=nullptr;
    fence ready{0};uint64_t id=0,frame=0,event=0,source=0,done=0;
    uint64_t capacity=0,bytes=0;unsigned pitch=0,bpp=0;
    unsigned w=0,h=0,fmt=0;char stage[24]{};bool submitted=false;
    UV uv[points];
};
std::mutex guard;std::atomic<bool> active{false},pending{false};
std::unordered_map<command_list *,State> states;
Shot shots[maxSlots];UV mcUv[mcPoints];unsigned mcCount=0;
effect_runtime *owner=nullptr;device *ownerDev=nullptr;
uint64_t frame=0,event=0,serial=0;unsigned left=0,captures=0,events=0;
uint64_t allocated=0;constexpr uint64_t memoryLimit=128ull*1024*1024;
float frameAspect=16.f/9;
DWORD nextCheck=0;FILETIME seen{};
template<typename... A> void log(const char *fmt,A...args){char s[4096];std::snprintf(s,sizeof(s),fmt,args...);reshade::log::message(reshade::log::level::info,s);}
bool eligible(resource r,D3D12_RESOURCE_DESC &d){
    if(!r.handle)return false;d=reinterpret_cast<ID3D12Resource *>(r.handle)->GetDesc();
    if(d.Dimension!=D3D12_RESOURCE_DIMENSION_TEXTURE2D||d.SampleDesc.Count!=1||d.DepthOrArraySize!=1||d.MipLevels!=1)return false;
    if(d.Format!=DXGI_FORMAT_R32G8X24_TYPELESS && d.Format!=DXGI_FORMAT_D32_FLOAT_S8X24_UINT &&
       d.Format!=DXGI_FORMAT_R32_TYPELESS && d.Format!=DXGI_FORMAT_D32_FLOAT)return false;
    // Include scaled and square candidates; do not assume scene aspect/resolution.
    return d.Width>=320 && d.Height>=180 && d.Width<=8192 && d.Height<=8192;
}
void record(command_list *cmd,const char *stage,resource r,unsigned a=0,unsigned b=0){
    D3D12_RESOURCE_DESC d{};if(events>=512||!eligible(r,d))return;
    auto &s=states[cmd];++events;++event;
    log("[depth-trace-event] frame=%llu event=%llu cmd=%llx stage=%s res=%llx size=%llux%u fmt=%u a=%x b=%x draws=%llu verts=%llu indirect=%llu vp=%.1f,%.1f,%.1f,%.1f pass=%d",
        frame,event,(uint64_t)cmd,stage,r.handle,d.Width,d.Height,unsigned(d.Format),a,b,s.draws,s.verts,s.indirect,
        s.vp.x,s.vp.y,s.vp.width,s.vp.height,int(s.pass));
}
void capture(command_list *cmd,resource r,resource_usage usage,const char *stage){
    D3D12_RESOURCE_DESC d{};if(captures>=(std::strcmp(stage,"effect-bound")==0?24u:23u)||states[cmd].pass||!eligible(r,d))return;
    Shot *shot=nullptr;for(auto &s:shots)if(!s.id){shot=&s;break;}if(!shot)return;
    // Record every candidate's metadata, but reserve readbacks for scene-like
    // aspect ratios. Square/atlas shadow maps must not consume the memory budget.
    if(std::abs(float(d.Width)/d.Height-frameAspect)>.15f*frameAspect)return;
    auto dev=cmd->get_device();
    auto nativeDev=reinterpret_cast<ID3D12Device *>(dev->get_native());
    D3D12_PLACED_SUBRESOURCE_FOOTPRINT fp{};UINT rows=0;UINT64 rowBytes=0,total=0;
    nativeDev->GetCopyableFootprints(&d,0,1,0,&fp,&rows,&rowBytes,&total);
    if(!total||total>memoryLimit||rows!=d.Height||rowBytes%d.Width||rowBytes/d.Width<4)return;
    if(shot->capacity<total){
        if(allocated-shot->capacity+total>memoryLimit)return;
        if(shot->buffer.handle){dev->destroy_resource(shot->buffer);allocated-=shot->capacity;shot->capacity=0;shot->buffer=resource{0};}
        if(!dev->create_resource(resource_desc(total,memory_heap::readback,resource_usage::copy_dest),nullptr,resource_usage::copy_dest,&shot->buffer))return;
        shot->capacity=total;allocated+=total;
    }
    shot->bytes=total;shot->pitch=fp.Footprint.RowPitch;shot->bpp=unsigned(rowBytes/d.Width);
    for(unsigned i=0;i<grid;++i)shot->uv[i]={(float(i%8)+.5f)/8,(float(i/8)+.5f)/4};
    for(unsigned i=0;i<mcPoints;++i)shot->uv[grid+i]=i<mcCount?mcUv[i]:UV{.5f,.5f};
    // Only capture at API-documented states: immediately before a depth clear,
    // before a texture copy (COPY_SOURCE), or the known shader binding at effects.
    // Do not guess depth plane state from barrier events: API20 drops subresource
    // indices, so a D32S8 stencil-plane barrier cannot safely establish plane0.
    auto nc=reinterpret_cast<ID3D12GraphicsCommandList *>(cmd->get_native());
    D3D12_RESOURCE_BARRIER barrier{};barrier.Type=D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
    barrier.Transition.pResource=reinterpret_cast<ID3D12Resource *>(r.handle);barrier.Transition.Subresource=0;
    barrier.Transition.StateBefore=static_cast<D3D12_RESOURCE_STATES>(uint32_t(usage));barrier.Transition.StateAfter=D3D12_RESOURCE_STATE_COPY_SOURCE;
    if(usage!=resource_usage::copy_source)nc->ResourceBarrier(1,&barrier);
    D3D12_TEXTURE_COPY_LOCATION src{};src.pResource=barrier.Transition.pResource;src.Type=D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;
    D3D12_TEXTURE_COPY_LOCATION dst{};dst.pResource=reinterpret_cast<ID3D12Resource *>(shot->buffer.handle);dst.Type=D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT;dst.PlacedFootprint=fp;
    // D3D12 depth-stencil copies require the entire subresource and a null box.
    // Grid extraction happens on CPU after completion; there are no sparse GPU
    // copies from a depth attachment, nor waits in an ordinary render callback.
    nc->CopyTextureRegion(&dst,0,0,0,&src,nullptr);
    if(usage!=resource_usage::copy_source){std::swap(barrier.Transition.StateBefore,barrier.Transition.StateAfter);nc->ResourceBarrier(1,&barrier);}
    shot->cmd=cmd;shot->id=++serial;shot->frame=frame;shot->event=event;shot->source=r.handle;
    shot->w=unsigned(d.Width);shot->h=d.Height;shot->fmt=unsigned(d.Format);std::snprintf(shot->stage,sizeof(shot->stage),"%s",stage);++captures;
    pending=true;
    log("[depth-trace-capture] id=%llu frame=%llu event=%llu res=%llx stage=%s",shot->id,frame,event,r.handle,stage);
}
void harvest(){
    if(!ownerDev)return;
    for(auto &s:shots){if(!s.id||!s.submitted||!s.ready.handle||ownerDev->get_completed_fence_value(s.ready)<s.done)continue;
        void *data=nullptr;if(ownerDev->map_buffer_region(s.buffer,0,s.bytes,map_access::read_only,&data)){
            char text[4096];int n=std::snprintf(text,sizeof(text),"[depth-trace-pixels] id=%llu frame=%llu event=%llu res=%llx stage=%s size=%ux%u fmt=%u grid8x4+mc12=",s.id,s.frame,s.event,s.source,s.stage,s.w,s.h,s.fmt);
            for(unsigned i=0;i<points;++i){auto x=std::min(s.w-1,unsigned(s.uv[i].u*s.w));auto y=std::min(s.h-1,unsigned(s.uv[i].v*s.h));float raw;
                std::memcpy(&raw,(const uint8_t *)data+size_t(y)*s.pitch+size_t(x)*s.bpp,4);n+=std::snprintf(text+n,sizeof(text)-n," %.9g",raw);}
            ownerDev->unmap_buffer_region(s.buffer);reshade::log::message(reshade::log::level::info,text);
        }s.id=0;s.submitted=false;s.queue=nullptr;s.cmd=nullptr;
    }
    bool any=false;for(const auto &s:shots)any|=s.id!=0;pending=any;
}
void barrier_event(command_list *cmd,uint32_t n,const resource *r,const resource_usage *a,const resource_usage *b){
    if(!active)return;std::lock_guard<std::mutex> lock(guard);if(!active)return;
    for(unsigned i=0;i<n;++i)record(cmd,"barrier-after",r[i],uint32_t(a[i]),uint32_t(b[i]));
}
void bind(command_list *cmd,uint32_t,const resource_view *,resource_view dsv){
    if(!active)return;std::lock_guard<std::mutex> lock(guard);if(!active)return;
    auto &s=states[cmd];record(cmd,"unbind",s.depth);s.depth=dsv.handle?dqj_resource_from_view(cmd->get_device(),dsv):resource{0};s.draws=s.verts=s.indirect=0;record(cmd,"bind",s.depth);
}
void viewport_event(command_list *cmd,uint32_t first,uint32_t count,const viewport *vp){if(!active||first||!count)return;std::lock_guard<std::mutex> lock(guard);if(active)states[cmd].vp=vp[0];}
bool draw(command_list *cmd,uint32_t v,uint32_t instances,uint32_t,uint32_t){if(!active)return false;std::lock_guard<std::mutex> lock(guard);if(active){auto &s=states[cmd];++s.draws;s.verts+=uint64_t(v)*instances;}return false;}
bool indexed(command_list *cmd,uint32_t v,uint32_t instances,uint32_t first,int32_t,uint32_t base){return draw(cmd,v,instances,first,base);}
bool indirect(command_list *cmd,indirect_command type,resource,uint64_t,uint32_t count,uint32_t){if(!active||type==indirect_command::dispatch)return false;std::lock_guard<std::mutex> lock(guard);if(active){auto &s=states[cmd];s.draws+=count;s.indirect+=count;}return false;}
bool clear(command_list *cmd,resource_view dsv,const float *depth,const uint8_t *,uint32_t,const rect *){
    if(!active||!depth)return false;std::lock_guard<std::mutex> lock(guard);if(!active)return false;
    auto r=dqj_resource_from_view(cmd->get_device(),dsv);unsigned raw=0;std::memcpy(&raw,depth,4);record(cmd,"pre-clear",r,raw);
    capture(cmd,r,resource_usage::depth_stencil_write,"pre-clear");states[cmd].draws=states[cmd].verts=states[cmd].indirect=0;return false;
}
bool copy(command_list *cmd,resource src,resource dst){if(!active)return false;std::lock_guard<std::mutex> lock(guard);if(active){record(cmd,"copy-source",src);record(cmd,"copy-dest",dst);capture(cmd,src,resource_usage::copy_source,"copy-source");}return false;}
bool begin(command_list *cmd,uint32_t,const render_pass_render_target_desc *,const render_pass_depth_stencil_desc *ds,render_pass_flags){
    if(!active)return false;std::lock_guard<std::mutex> lock(guard);if(active){auto &s=states[cmd];s.depth=ds&&ds->view.handle?dqj_resource_from_view(cmd->get_device(),ds->view):resource{0};record(cmd,"pass-begin",s.depth,ds?unsigned(ds->depth_load_op):0,ds?unsigned(ds->depth_store_op):0);s.pass=true;}return false;
}
bool end(command_list *cmd){if(!active)return false;std::lock_guard<std::mutex> lock(guard);if(active){record(cmd,"pass-end-before",states[cmd].depth);states[cmd].pass=false;}return false;}
void reset(command_list *cmd){if(!active)return;std::lock_guard<std::mutex> lock(guard);if(active)record(cmd,"reset",states[cmd].depth);states.erase(cmd);
    // A reset without submission invalidates this recording, never read old bytes.
    for(auto &s:shots)if(s.id&&s.cmd==cmd&&!s.queue){log("[depth-trace-discard] id=%llu unsubmitted-reset",s.id);s.id=0;}}
void execute(command_queue *q,command_list *cmd){if(!active&&!pending)return;std::lock_guard<std::mutex> lock(guard);bool observed=false;
    for(auto &s:shots)if(s.id&&s.cmd==cmd&&!s.queue){s.queue=q;observed=true;}
    if(observed)log("[depth-trace-submit] frame=%llu cmd=%llx queue=%llx",frame,(uint64_t)cmd,(uint64_t)q);}
void poll(){
    if(GetTickCount()<nextCheck)return;nextCheck=GetTickCount()+1000;
    wchar_t p[MAX_PATH]{};GetModuleFileNameW(nullptr,p,MAX_PATH);auto slash=wcsrchr(p,L'\\');if(!slash)return;wcscpy_s(slash+1,MAX_PATH-(slash+1-p),L"DqjDepthTrace.request");
    WIN32_FILE_ATTRIBUTE_DATA st{};if(!GetFileAttributesExW(p,GetFileExInfoStandard,&st)||CompareFileTime(&seen,&st.ftLastWriteTime)==0)return;seen=st.ftLastWriteTime;
    FILE *f=_wfopen(p,L"r");unsigned n=0;if(f){if(fscanf(f,"%u",&n)!=1)n=0;fclose(f);}left=std::min(n,6u);states.clear();active=left>0;
    log("[depth-trace-request] frames=%u max6 captures24/frame events512/frame whole-depth-plane memory128MiB; barrier subresource/alias not exposed, never guessed",left);
}
}
namespace depth_trace {
void register_events(){
    using reshade::addon_event;using reshade::register_event;
    register_event<addon_event::barrier>(barrier_event);register_event<addon_event::bind_render_targets_and_depth_stencil>(bind);
    register_event<addon_event::bind_viewports>(viewport_event);register_event<addon_event::draw>(draw);register_event<addon_event::draw_indexed>(indexed);
    register_event<addon_event::draw_or_dispatch_indirect>(indirect);register_event<addon_event::clear_depth_stencil_view>(clear);
    register_event<addon_event::copy_resource>(copy);register_event<addon_event::begin_render_pass>(begin);register_event<addon_event::end_render_pass>(end);
    register_event<addon_event::reset_command_list>(reset);register_event<addon_event::destroy_command_list>(reset);register_event<addon_event::execute_command_list>(execute);
}
void tick(effect_runtime *r,command_list *cmd,resource_view srv,const uint8_t *mc,uint32_t w,uint32_t h,int flags,int64_t mcSequence){
    if(r->get_device()->get_api()!=device_api::d3d12||(owner&&owner!=r))return;
    std::lock_guard<std::mutex> lock(guard);owner=r;ownerDev=r->get_device();harvest();
    uint32_t bw=0,bh=0;r->get_screenshot_width_and_height(&bw,&bh);if(bh)frameAspect=float(bw)/bh;
    if(active){
        const uint8_t guid[16]={0xc7,0x63,0x63,0x7c,0x4e,0xf9,0x7a,0x43,0x91,0x60,0x14,0x17,0x82,0xc4,0x4a,0x98};uint64_t state=0,selected=0;r->get_private_data(guid,&state);if(state)std::memcpy(&selected,(void *)state,8);
        auto bound=srv.handle?dqj_resource_from_view(ownerDev,srv):resource{0};
        log("[depth-trace-frame] frame=%llu selected=%llx bound=%llx mcSequence=%lld mc=%ux%u captures=%u events=%u",frame,selected,bound.handle,mcSequence,w,h,captures,events);
        if(bound.handle){record(cmd,"effect-bound",bound);capture(cmd,bound,resource_usage::shader_resource,"effect-bound");for(auto &s:shots)if(s.id&&s.cmd==cmd&&!s.queue)s.queue=r->get_command_queue();}
        if(left)--left;if(!left){active=false;states.clear();log("[depth-trace-stop] bounded capture finished; asynchronous readback continues");}
    }
    ++frame;captures=events=0;
    poll();if(!active)return;
    mcCount=0;if(mc&&w&&h){size_t pixels=size_t(w)*h,count=0;for(size_t i=0;i<pixels;++i)if(mc[i*4+3]>10)++count;size_t rank=0;
        for(size_t i=0;i<pixels&&mcCount<mcPoints;++i){if(mc[i*4+3]<=10)continue;if(rank++>=size_t((mcCount+.5)*count/mcPoints))mcUv[mcCount++]={(float(i%w)+.5f)/w,(flags&2)?1-(float(i/w)+.5f)/h:(float(i/w)+.5f)/h};}}
    if(active){char text[1024];int n=std::snprintf(text,sizeof(text),"[depth-trace-uv] frame=%llu mcSequence=%lld mc12=",frame,mcSequence);for(unsigned i=0;i<mcCount;++i)n+=std::snprintf(text+n,sizeof(text)-n," %.6f,%.6f",mcUv[i].u,mcUv[i].v);reshade::log::message(reshade::log::level::info,text);}
}
void finish(command_queue *presentQueue){if(!pending)return;std::lock_guard<std::mutex> lock(guard);if(!ownerDev)return;
    for(auto &s:shots)if(s.id&&!s.submitted&&s.queue && (s.queue==presentQueue||frame>s.frame+1)){
        // Wukong submits scene work on another queue than the swapchain's queue.
        // Signal that actual submission queue, after an additional effects
        // boundary; signaling only the Present queue stranded producer readback.
        // No CPU/GPU wait is introduced here.
        if(!s.ready.handle&&!ownerDev->create_fence(0,fence_flags::none,&s.ready))continue;s.done=s.id;
        if(s.queue->signal(s.ready,s.done)){s.submitted=true;log("[depth-trace-signal] id=%llu queue=%llx presentQueue=%llx",s.id,(uint64_t)s.queue,(uint64_t)presentQueue);}
    }harvest();}
void destroy(effect_runtime *r){std::lock_guard<std::mutex> lock(guard);if(owner&&owner!=r)return;active=false;auto dev=r->get_device();
    for(auto &s:shots)if(s.queue)s.queue->wait_idle();harvest();for(auto &s:shots){if(s.buffer.handle)dev->destroy_resource(s.buffer);if(s.ready.handle)dev->destroy_fence(s.ready);s=Shot{};}states.clear();owner=nullptr;ownerDev=nullptr;left=0;allocated=0;pending=false;
}
}
