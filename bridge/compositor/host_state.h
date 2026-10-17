#pragma once
#include <cstdint>

// Written by the Wukong adapter on its game thread. Units and axes are already
// Minecraft's: metres, yaw 0 toward +Z, positive pitch downward, vertical FOV.
// An odd sequence means the writer is busy; header reads use a seqlock.
struct DqjHostState {
    uint32_t magic;       // 0x484a5144: DQJH
    uint32_t version;     // 1
    int64_t sequence;
    uint32_t active;
    uint32_t width;
    uint32_t height;
    uint32_t matchedMcFrame; // low 32 bits of the MC frame the host camera used this frame; 0 = unknown
    int64_t heartbeatNs;
    int64_t frame;
    double x, y, z;
    float yaw, pitch, roll, verticalFov;
    float nearMetres, farMetres;
};
static_assert(sizeof(DqjHostState) == 96);

bool dqj_read_host_state(DqjHostState &out);
void dqj_update_host_state(DqjHostState *observed=nullptr);
