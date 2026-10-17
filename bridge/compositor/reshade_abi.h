#pragma once
#include <windows.h>
#include <reshade.hpp>

// ReShade 6.8 / API 20 is built with MSVC. MinGW returns the 8-byte handle
// in RAX, whereas MSVC uses a hidden output pointer after `this` because
// the SDK handle has a user-defined constructor. Keep the boundary explicit.
inline reshade::api::effect_uniform_variable dqj_find_uniform(
    reshade::api::effect_runtime *runtime,const char *effect,const char *name){
#if defined(__MINGW32__) && !defined(_MSC_VER)
    static_assert(RESHADE_API_VERSION == 20,"Re-audit the SDK vtable before changing ReShade API version");
    using handle=reshade::api::effect_uniform_variable;
    using method=handle *(*)(reshade::api::effect_runtime *,handle *,const char *,const char *);
    auto table=*reinterpret_cast<void ***>(runtime);
    handle result{};
    // API 20: slot 20, independently confirmed against the runtime crash dump.
    reinterpret_cast<method>(table[20])(runtime,&result,effect,name);
    return result;
#else
    return runtime->find_uniform_variable(effect,name);
#endif
}

inline reshade::api::resource dqj_resource_from_view(reshade::api::device *device, reshade::api::resource_view view) {
#if defined(__MINGW32__) && !defined(_MSC_VER)
    static_assert(RESHADE_API_VERSION == 20, "Re-audit API 20 device vtable");
    using handle = reshade::api::resource;
    using method = handle *(*)(reshade::api::device *, handle *, reshade::api::resource_view);
    handle result{};
    // api_object slots 0..2, device::get_resource_from_view is slot 13.
    reinterpret_cast<method>((*reinterpret_cast<void ***>(device))[13])(device, &result, view);
    return result;
#else
    return device->get_resource_from_view(view);
#endif
}
