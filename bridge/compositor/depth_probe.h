#pragma once
#include <windows.h>
#include <Unknwn.h>
#include <reshade.hpp>
#include <cstdint>
namespace depth_probe {
// Optional, bounded observational GPU instrumentation. Triggered only by the
// developer-created DqjDepthProbe.request beside the host executable.
void tick(reshade::api::effect_runtime *, reshade::api::command_list *, reshade::api::resource_view,
    const uint8_t *mcPlanes, uint32_t w, uint32_t h, float nearMc, float farMc, int flags, float nearHost, float farHost);
void signal(reshade::api::swapchain *);
void destroy(reshade::api::effect_runtime *);
}
