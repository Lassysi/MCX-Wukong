#include "pose_trace.h"
#include "reshade_abi.h"
#include <windows.h>
#include <algorithm>
#include <cstdio>
#include <cstring>
#include <cstdarg>
namespace {
template<class T>T read(const uint8_t *p){T v{};std::memcpy(&v,p,sizeof(v));return v;}
ULONGLONG nextPoll=0,until=0,nextSample=0;
unsigned samples=0;bool active=false;
void log(const char *fmt,...){char b[2048]{};va_list a;va_start(a,fmt);std::vsnprintf(b,sizeof(b),fmt,a);va_end(a);reshade::log::message(reshade::log::level::info,b);}
bool diagnostic(uint8_t *out){
#ifdef DQJ_COMPOSITOR_TEST
 const wchar_t *name=L"Local\\DqjDepth30TestPoseDiagnostic";
#else
 const wchar_t *name=L"Local\\DqjPoseDiagnostic";
#endif
 HANDLE map=OpenFileMappingW(FILE_MAP_READ,FALSE,name);if(!map)return false;
 auto p=static_cast<const uint8_t *>(MapViewOfFile(map,FILE_MAP_READ,0,0,160));bool ok=false;
 if(p){for(unsigned i=0;i<3;++i){auto seq=read<int64_t>(p+8);if(seq&1)continue;MemoryBarrier();std::memcpy(out,p,160);MemoryBarrier();if(seq==read<int64_t>(p+8)){ok=read<uint32_t>(out)==0x504a5144&&read<uint32_t>(out+4)==1;break;}}UnmapViewOfFile(p);}
 CloseHandle(map);return ok;
}
void poll(ULONGLONG now){
 if(now<nextPoll)return;nextPoll=now+1000;
 wchar_t path[MAX_PATH]{};GetModuleFileNameW(nullptr,path,MAX_PATH);auto slash=wcsrchr(path,L'\\');if(!slash)return;
 wcscpy_s(slash+1,MAX_PATH-(slash+1-path),L"DqjPoseTrace.request");
 FILE *f=_wfopen(path,L"r");if(!f)return;unsigned seconds=10;int got=std::fscanf(f,"%u",&seconds);std::fclose(f);DeleteFileW(path);
 seconds=std::clamp(got==1?seconds:10u,1u,30u);until=now+seconds*1000;nextSample=now;samples=0;active=true;
 LARGE_INTEGER frequency{};QueryPerformanceFrequency(&frequency);
 log("[pose-trace-start] seconds=%u maxSamples=600 sampleIntervalMs=50 qpcFrequency=%lld; CPU command-recording metadata, no GPU view-matrix claim",seconds,frequency.QuadPart);
}
}
namespace pose_trace {
void tick(reshade::api::effect_runtime *runtime,reshade::api::command_list *cmd,const DqjHostState &h,
 const uint8_t *d,int64_t published,bool on,uint64_t upload,uint64_t completed,uint64_t pending,bool depthReady,uint32_t cpuFirstRgba){
 auto now=GetTickCount64();poll(now);if(!active)return;
 if(now>=until||samples>=600){log("[pose-trace-stop] samples=%u",samples);active=false;return;}
 if(now<nextSample)return;nextSample=now+50;++samples;
 LARGE_INTEGER q{};QueryPerformanceCounter(&q);
 bool reproject=false;auto v=dqj_find_uniform(runtime,"MCPassthrough.fx","Reproject");if(v.handle)runtime->get_uniform_value_bool(v,&reproject,1);
 log("[pose-trace-consumer] sample=%u qpc=%lld cmd=%llx on=%d hostFrame=%lld hostNs=%lld hostEye=%.12g,%.12g,%.12g hostRot=%.9g,%.9g,%.9g hostVfov=%.9g hostMatchMcFrame=%u mcPublished=%lld mcFrame=%lld mcEcho=%lld mcCaptureJavaNs=%lld mcPublishJavaNs=%lld mcEye=%.12g,%.12g,%.12g mcRot=%.9g,%.9g,%.9g mcVfov=%.9g upload=%llu completed=%llu pending=%llu depthReady=%d ReprojectUniformPresent=%d ReprojectUniform=%d cpuFirstRgba=%08x",
 samples,q.QuadPart,(uint64_t)cmd,int(on),h.frame,h.heartbeatNs,h.x,h.y,h.z,h.yaw,h.pitch,h.roll,h.verticalFov,h.matchedMcFrame,published,
 read<int64_t>(d+8),read<int64_t>(d+16),read<int64_t>(d+88),read<int64_t>(d+96),read<double>(d+48),read<double>(d+56),read<double>(d+64),
 read<float>(d+72),read<float>(d+76),read<float>(d+80),read<float>(d+40),upload,completed,pending,int(depthReady),int(v.handle!=0),int(reproject),cpuFirstRgba);
 uint8_t pcm[160]{};if(diagnostic(pcm)){
  log("[pose-trace-pcm] sample=%u hostFrame=%lld sampledNs=%lld wireMcFrame=%lld wireReceivedNs=%lld requestedEye=%.12g,%.12g,%.12g requestedRot=%.9g,%.9g,%.9g pcmEye=%.12g,%.12g,%.12g pcmRot=%.9g,%.9g,%.9g pcmHorizontalFov=%.9g flags=%u completedNs=%lld; game-thread cache, not final GPU view",
  samples,read<int64_t>(pcm+16),read<int64_t>(pcm+24),read<int64_t>(pcm+32),read<int64_t>(pcm+40),
  read<double>(pcm+48),read<double>(pcm+56),read<double>(pcm+64),read<float>(pcm+72),read<float>(pcm+76),read<float>(pcm+80),
  read<double>(pcm+88),read<double>(pcm+96),read<double>(pcm+104),read<float>(pcm+112),read<float>(pcm+116),read<float>(pcm+120),read<float>(pcm+124),read<uint32_t>(pcm+128),read<int64_t>(pcm+136));
 }else log("[pose-trace-pcm] sample=%u unavailable=1",samples);
}
}
