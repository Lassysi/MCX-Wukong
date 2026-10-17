#pragma once
#include <windows.h>
#include <Unknwn.h>
#include <reshade.hpp>
#include <cstdint>
namespace depth_trace {
void register_events();
// Opt-in observation only. Never selects or changes the compositor's depth.
void tick(reshade::api::effect_runtime *, reshade::api::command_list *, reshade::api::resource_view,
    const uint8_t *, uint32_t, uint32_t, int, int64_t);
void finish(reshade::api::command_queue *);
void destroy(reshade::api::effect_runtime *);
}
