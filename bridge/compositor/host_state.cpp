#include "host_state.h"
#include "compositor.h"
#include <windows.h>
#include <cstring>
#include <cmath>

bool dqj_read_host_state(DqjHostState &out) {
#ifdef DQJ_COMPOSITOR_TEST
    HANDLE mapping = OpenFileMappingW(FILE_MAP_READ, FALSE, L"Local\\DqjDepth30TestHost");
#else
    HANDLE mapping = OpenFileMappingW(FILE_MAP_READ, FALSE, L"Local\\DqjWukongBridge");
#endif
    if (!mapping) return false;
    auto memory = static_cast<const volatile DqjHostState *>(MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, sizeof(DqjHostState)));
    if (!memory) { CloseHandle(mapping); return false; }
    bool valid = false;
    for (int i = 0; i < 3; ++i) {
        int64_t before = memory->sequence;
        if (before & 1) continue;
        MemoryBarrier();
        std::memcpy(&out, const_cast<const DqjHostState *>(memory), sizeof(out));
        MemoryBarrier();
        if (before != memory->sequence) continue;
        valid = out.magic == 0x484a5144 && out.version == 1;
        break;
    }
    UnmapViewOfFile(const_cast<const DqjHostState *>(memory));
    CloseHandle(mapping);
    return valid;
}

void dqj_update_host_state(DqjHostState *observed) {
    DqjHostState state{};
    bool valid = dqj_read_host_state(state);
    if(observed)*observed=state;
    LARGE_INTEGER counter, frequency;
    QueryPerformanceCounter(&counter);
    QueryPerformanceFrequency(&frequency);
    int64_t now = (counter.QuadPart / frequency.QuadPart) * 1000000000LL
        + (counter.QuadPart % frequency.QuadPart) * 1000000000LL / frequency.QuadPart;
    int64_t age = now - state.heartbeatNs;
    valid = valid && state.active && age >= 0 && age < 500000000LL
        && std::isfinite(state.verticalFov) && state.verticalFov > 1 && state.verticalFov < 179
        && std::isfinite(state.x) && std::isfinite(state.y) && std::isfinite(state.z)
        && state.nearMetres > 0 && state.farMetres > state.nearMetres;
    compositor::set_active(valid);
    if (!valid) return;
    compositor::set_host_planes(state.nearMetres, state.farMetres);
    compositor::set_host_pose(state.yaw, state.pitch, state.roll, state.verticalFov, state.x, state.y, state.z);
}
