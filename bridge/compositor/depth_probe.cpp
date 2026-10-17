#include "depth_probe.h"
#include "reshade_abi.h"
#include <windows.h>
#include <d3d12.h>
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
using namespace reshade::api;
namespace {
constexpr unsigned points=12;
struct Point {float u=0,v=0,mc=0;};
struct Slot {resource buffer{0};uint64_t done=0,id=0,selected=0;bool pending=false;unsigned format=0;float n=.1f,f=10000;Point p[points];};
Slot slots[3];fence complete{0};uint64_t serial=0;effect_runtime *owner=nullptr;command_queue *queue=nullptr;
unsigned left=0;DWORD nextCheck=0;FILETIME seen{};
float linear(float d,float n,float f,bool reversed){return reversed?(d>0?n*f/(n+d*(f-n)):1e9f):(d<1?n*f/(f-d*(f-n)):1e9f);}
void poll_request(){
    if(GetTickCount()<nextCheck)return;nextCheck=GetTickCount()+1000;
    wchar_t path[MAX_PATH]{};GetModuleFileNameW(nullptr,path,MAX_PATH);auto slash=wcsrchr(path,L'\\');if(!slash)return;
    wcscpy_s(slash+1,MAX_PATH-(slash+1-path),L"DqjDepthProbe.request");
    WIN32_FILE_ATTRIBUTE_DATA st{};if(!GetFileAttributesExW(path,GetFileExInfoStandard,&st))return;
    if(CompareFileTime(&seen,&st.ftLastWriteTime)==0)return;seen=st.ftLastWriteTime;
    FILE *file=_wfopen(path,L"r");unsigned wanted=0;if(file){if(fscanf(file,"%u",&wanted)!=1)wanted=0;fclose(file);}
    left=std::min(wanted,120u);reshade::log::message(reshade::log::level::info,"[depth-probe] bounded request observed (max120 frames, no GPU wait)");
}
void harvest(device *dev){
    if(!complete.handle)return;const auto done=dev->get_completed_fence_value(complete);
    for(auto &s:slots){
        if(!s.done||s.pending||s.done>done)continue;
        void *data=nullptr;if(dev->map_buffer_region(s.buffer,0,points*512,map_access::read_only,&data)){
            char msg[2048];int used=std::snprintf(msg,sizeof(msg),"[depth-probe] id=%llu selected=%llx format=%u n=%.4f uv,mc,raw,host=",(unsigned long long)s.id,(unsigned long long)s.selected,s.format,s.n);
            for(unsigned j=0;j<points && used<int(sizeof(msg))-100;++j){float raw;std::memcpy(&raw,(const uint8_t *)data+j*512,4);
                used+=std::snprintf(msg+used,sizeof(msg)-used," (%.4f,%.4f,%.3f,%.8g,%.3f)",s.p[j].u,s.p[j].v,s.p[j].mc,raw,linear(raw,s.n,s.f,true));}
            dev->unmap_buffer_region(s.buffer);reshade::log::message(reshade::log::level::info,msg);
        }
        s.done=0;
    }
}
}
namespace depth_probe {
void tick(effect_runtime *runtime,command_list *cmd,resource_view srv,const uint8_t *mc,uint32_t w,uint32_t h,float nearMc,float farMc,int flags,float nearHost,float farHost){
    if(owner && owner!=runtime)return;if(runtime->get_device()->get_api()!=device_api::d3d12)return;
    auto dev=runtime->get_device();harvest(dev);poll_request();if(!left||!srv.handle||!mc||!w||!h)return;
    auto source=dqj_resource_from_view(dev,srv);if(!source.handle)return;
    auto native=reinterpret_cast<ID3D12Resource *>(source.handle);auto desc=native->GetDesc();
    if(desc.SampleDesc.Count!=1||desc.Dimension!=D3D12_RESOURCE_DIMENSION_TEXTURE2D)return;
    Slot *free=nullptr;for(auto &s:slots)if(!s.done){free=&s;break;}if(!free)return;
    if(!complete.handle&&!dev->create_fence(0,fence_flags::none,&complete))return;
    if(!free->buffer.handle&&!dev->create_resource(resource_desc(points*512,memory_heap::readback,resource_usage::copy_dest),nullptr,resource_usage::copy_dest,&free->buffer))return;
    // Deterministic quantiles of actual world coverage, excluding the hand/HUD plane.
    size_t count=0,pixels=size_t(w)*h;for(size_t i=0;i<pixels;++i)if(mc[i*4+3]>10)++count;if(!count)return;
    unsigned taken=0;size_t rank=0;for(size_t i=0;i<pixels && taken<points;++i){
        if(mc[i*4+3]<=10)continue;
        const double jitter=std::fmod((taken+1)*.6180339887498949,1.0);
        if(rank++ >= size_t((taken+jitter)*count/points)){
            float raw;std::memcpy(&raw,mc+pixels*4+i*4,4);
            free->p[taken++]={(float(i%w)+.5f)/w,(flags&2)?1-(float(i/w)+.5f)/h:(float(i/w)+.5f)/h,linear(raw,nearMc,farMc,(flags&4)!=0)};
        }
    }
    if(taken!=points)return;
    // Inspect only the public prefix of the pinned 6.8.0 built-in state, by its
    // official UUID. Observational only; never change or lock its internals.
    const uint8_t guid[16]={0xc7,0x63,0x63,0x7c,0x4e,0xf9,0x7a,0x43,0x91,0x60,0x14,0x17,0x82,0xc4,0x4a,0x98};
    uint64_t state=0;runtime->get_private_data(guid,&state);free->selected=0;
    if(state)std::memcpy(&free->selected,reinterpret_cast<void *>(state),8);
    auto nativeDev=reinterpret_cast<ID3D12Device *>(dev->get_native());
    auto copyDesc=desc;copyDesc.Width=1;copyDesc.Height=1;copyDesc.DepthOrArraySize=1;copyDesc.MipLevels=1;
    D3D12_PLACED_SUBRESOURCE_FOOTPRINT footprint{};UINT rows=0;UINT64 rowBytes=0,total=0;
    nativeDev->GetCopyableFootprints(&copyDesc,0,1,0,&footprint,&rows,&rowBytes,&total);
    if(total>512)return;
    auto nativeCmd=reinterpret_cast<ID3D12GraphicsCommandList *>(cmd->get_native());
    D3D12_TEXTURE_COPY_LOCATION src{};src.pResource=native;src.Type=D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;src.SubresourceIndex=0;
    D3D12_TEXTURE_COPY_LOCATION dst{};dst.pResource=reinterpret_cast<ID3D12Resource *>(free->buffer.handle);dst.Type=D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT;dst.PlacedFootprint=footprint;
    cmd->barrier(source,resource_usage::shader_resource,resource_usage::copy_source);
    for(unsigned j=0;j<points;++j){
        auto x=std::min(UINT(desc.Width-1),UINT(free->p[j].u*desc.Width));auto y=std::min(desc.Height-1,UINT(free->p[j].v*desc.Height));
        D3D12_BOX box{x,y,0,x+1,y+1,1};dst.PlacedFootprint.Offset=j*512;nativeCmd->CopyTextureRegion(&dst,0,0,0,&src,&box);
    }
    cmd->barrier(source,resource_usage::copy_source,resource_usage::shader_resource);
    owner=runtime;queue=runtime->get_command_queue();free->done=free->id=++serial;free->pending=true;free->format=unsigned(desc.Format);free->n=nearHost;free->f=farHost;--left;
}
void signal(swapchain *swap){if(!owner||!queue||owner->get_hwnd()!=swap->get_hwnd())return;
    for(auto &s:slots)if(s.pending && queue->signal(complete,s.done))s.pending=false;
}
void destroy(effect_runtime *runtime){if(owner&&owner!=runtime)return;auto dev=runtime->get_device();
    if(queue)queue->wait_idle();harvest(dev);for(auto &s:slots){if(s.buffer.handle)dev->destroy_resource(s.buffer);s=Slot{};}
    if(complete.handle)dev->destroy_fence(complete);complete=fence{0};owner=nullptr;queue=nullptr;left=0;
}
}
