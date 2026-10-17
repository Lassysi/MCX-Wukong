#include "compositor.h"
#include "host_state.h"
#include <windows.h>
#include <reshade.hpp>
#include <cmath>
#ifdef DQJ_COMPOSITOR_LAB
void dqj_lab_capture(reshade::api::effect_runtime *runtime);
#endif
#ifdef DQJ_COMPOSITOR_TEST
void dqj_depth30_capture(reshade::api::effect_runtime *runtime);
#endif

extern "C" __declspec(dllexport) const char *NAME = "Dqj MC Runtime Bridge";
extern "C" __declspec(dllexport) const char *DESCRIPTION = "Real Minecraft world, depth and hand/HUD frame compositor.";

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(module);
        if (!compositor::try_register(module)) return FALSE;
        compositor::set_pose_lag(0);
#ifdef DQJ_COMPOSITOR_TEST
        reshade::register_event<reshade::addon_event::reshade_present>(dqj_depth30_capture);
#endif
#ifdef DQJ_COMPOSITOR_LAB
        reshade::register_event<reshade::addon_event::reshade_present>(dqj_lab_capture);
#endif
    } else if (reason == DLL_PROCESS_DETACH) {
        compositor::unregister(module);
    }
    return TRUE;
}
