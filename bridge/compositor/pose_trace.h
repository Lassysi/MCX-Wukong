#pragma once
#include <windows.h>
#include <Unknwn.h>
#include <cstdint>
#include <reshade.hpp>
#include "host_state.h"
namespace pose_trace {
// Exact private descriptor committed with the texture upload, not newest shared header.
void tick(reshade::api::effect_runtime *,reshade::api::command_list *,const DqjHostState &,
    const uint8_t *,int64_t,bool,uint64_t,uint64_t,uint64_t,bool,uint32_t);
}
