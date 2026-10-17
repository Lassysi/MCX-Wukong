#include "compositor.h"
#include "host_state.h"
#include "reshade_abi.h"
#include "depth_probe.h"
#include "depth_trace.h"
#include "pose_trace.h"
#include <windows.h>
#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <mutex>
#include <vector>
#include <reshade.hpp>
#include <d3d12.h>

using namespace reshade::api;

namespace
{
#ifdef DQJ_COMPOSITOR_TEST
	constexpr const wchar_t *kMappingName = L"Local\\DqjDepth30TestFrame";
#else
	constexpr const wchar_t *kMappingName = L"Local\\MCPassthroughFrame";
#endif
	constexpr uint32_t kMagic = 0x5450434D; // "MCPT"
	constexpr int kHeader = 4096;
	constexpr int kSlotDesc = 256;
	constexpr int kSlotDescBytes = 128;
	constexpr const char *kEffect = "MCPassthrough.fx";

	std::atomic<bool> g_registered{false};
	std::atomic<bool> g_active{false};
	std::atomic<float> g_hostNear{0.15f};
	std::atomic<float> g_hostFar{10000.0f};
	std::atomic<uint32_t> g_bbWidth{0}, g_bbHeight{0};
	std::atomic<bool> g_cameraLocked{false};
	std::atomic<float> g_lookLight{-1.0f}, g_lookBias{-1.0f}, g_lookSlope{-1.0f};
	std::atomic<float> g_shakeX{0.0f}, g_shakeY{0.0f}, g_shakeRoll{0.0f}, g_portalWarp{0.0f};
	float g_savedLight = -1.0f, g_savedBias = -1.0f, g_savedSlope = -1.0f;

	struct Pose
	{
		float yaw = 0, pitch = 0, roll = 0, fov = 70;
		double x = 0, y = 0, z = 0;
		bool valid = false;
	};
	std::mutex g_poseLock;
	// GTA's recent camera poses, newest last. The script reads the camera GTA is about to render (the next frame),
	// while the picture being presented was rendered with the one before: re-project to that (g_poseLag back).
	Pose g_hostPoses[4];
	unsigned g_hostPoseCount = 0;
	std::atomic<int> g_poseLag{0}; // measured: 0 matches best (third and first person)
	Pose g_mcPose;

	HANDLE g_mapping = nullptr;
	const uint8_t *g_view = nullptr;
	int32_t g_slots = 0;
	int64_t g_stride = 0;
	int64_t g_lastPublish = -1;
	// Preview42 frame matching. The host writes the MC frame its camera used at
	// the start of each engine frame; upload that exact ring slot instead of the
	// newest one (live41: newest was 1-4 MC frames ahead of the rendered scene).
	struct MatchEntry { int64_t hostFrame = -1; uint32_t mcFrame = 0; };
	MatchEntry g_matchHistory[8];
	unsigned g_matchNext = 0;
	int g_matchLag = 0;
	bool g_matchEnabled = true;
	int64_t g_uploadedMcFrame = -1;
	uint64_t g_matchUploads = 0, g_nearestUploads = 0, g_matchHeld = 0, g_matchMissed = 0, g_newestUploads = 0;
	DWORD g_nextOpenAttempt = 0;

	struct Layer
	{
		resource tex = {0};
		resource_view srv = {0};
	};
	Layer g_world, g_depth, g_overlay;
	uint32_t g_width = 0, g_height = 0;
	bool g_hasFrame = false;
	ULONGLONG g_lastFrameAt = 0;
	std::vector<uint8_t> g_staging;
	std::vector<uint8_t> g_candidate;
	uint8_t g_uploadedDescriptor[kSlotDescBytes]{};
	struct UploadSlot { resource buffer{0}; void *mapped = nullptr; uint64_t done = 0; };
	UploadSlot g_upload[3];
	fence g_uploadFence{0};
	uint64_t g_uploadNumber = 0, g_pendingSignal = 0;
	size_t g_uploadLayerBytes = 0;
	uint32_t g_uploadRowPitch = 0;
	effect_runtime *g_ownerRuntime = nullptr;
	effect_texture_variable g_hostDepthVariable{0};
	effect_runtime *g_depthVariableRuntime = nullptr;
	uint64_t g_reportedDepth = UINT64_MAX;
	bool g_reportedReady = false;
	ULONGLONG g_depthReportAt = 0;
	ULONGLONG g_depthChangeReportAt = 0;
	command_queue *g_uploadQueue = nullptr;
	float g_mcNear = 0.05f, g_mcFar = 2048.0f;
	int32_t g_mcFlags = 7;

	template <typename T>
	T read(const uint8_t *p)
	{
		T v;
		std::memcpy(&v, p, sizeof(T));
		return v;
	}

	bool open_mapping()
	{
		if (g_view != nullptr)
			return true;
		if (GetTickCount() < g_nextOpenAttempt)
			return false;
		g_nextOpenAttempt = GetTickCount() + 1000;
		g_mapping = OpenFileMappingW(FILE_MAP_READ, FALSE, kMappingName);
		if (g_mapping == nullptr)
			return false;
		const auto *header = static_cast<const uint8_t *>(MapViewOfFile(g_mapping, FILE_MAP_READ, 0, 0, kHeader));
		if (header == nullptr || read<uint32_t>(header) != kMagic || read<uint32_t>(header + 4) != 1
			|| read<uint32_t>(header + 8) != kHeader)
		{
			if (header != nullptr)
				UnmapViewOfFile(header);
			CloseHandle(g_mapping);
			g_mapping = nullptr;
			return false;
		}
		g_slots = read<int32_t>(header + 12);
		g_stride = read<int64_t>(header + 16);
		UnmapViewOfFile(header);
		if (g_slots < 1 || g_slots > 8 || g_stride < 12 || g_stride > 3840LL * 2160 * 12)
		{
			CloseHandle(g_mapping); g_mapping = nullptr; return false;
		}
		g_view = static_cast<const uint8_t *>(MapViewOfFile(g_mapping, FILE_MAP_READ, 0, 0, static_cast<SIZE_T>(kHeader + g_stride * g_slots)));
		if (g_view == nullptr)
		{
			CloseHandle(g_mapping);
			g_mapping = nullptr;
			return false;
		}
		reshade::log::message(reshade::log::level::info, "MCPassthrough: connected to Minecraft's frame export");
		return true;
	}

	void destroy_layers(device *dev)
	{
		// Resize/destruction only; ordinary frames never wait on the GPU.
		if (g_uploadQueue) g_uploadQueue->wait_idle();
		for (auto &slot : g_upload) {
			if (slot.mapped) dev->unmap_buffer_region(slot.buffer);
			if (slot.buffer.handle) dev->destroy_resource(slot.buffer);
			slot = UploadSlot{};
		}
		if (g_uploadFence.handle) dev->destroy_fence(g_uploadFence);
		g_uploadFence = fence{0}; g_pendingSignal = 0;
		g_uploadLayerBytes = 0; g_uploadRowPitch = 0;
		for (Layer *layer : {&g_world, &g_depth, &g_overlay})
		{
			if (layer->srv.handle != 0)
				dev->destroy_resource_view(layer->srv);
			if (layer->tex.handle != 0)
				dev->destroy_resource(layer->tex);
			*layer = Layer();
		}
		g_width = g_height = 0;
		g_hasFrame = false;
	}

	bool create_layer(device *dev, Layer &layer, uint32_t w, uint32_t h, format fmt)
	{
		if (!dev->create_resource(
				resource_desc(w, h, 1, 1, fmt, 1, memory_heap::default_, resource_usage::shader_resource | resource_usage::copy_dest),
				nullptr, resource_usage::shader_resource, &layer.tex))
			return false;
		return dev->create_resource_view(layer.tex, resource_usage::shader_resource, resource_view_desc(fmt), &layer.srv);
	}

	void bind(effect_runtime *runtime)
	{
		runtime->update_texture_bindings("MCWORLD", g_world.srv, g_world.srv);
		runtime->update_texture_bindings("MCDEPTH", g_depth.srv, g_depth.srv);
		runtime->update_texture_bindings("MCOVERLAY", g_overlay.srv, g_overlay.srv);
	}

	/// Optional DqjFrameMatch.ini beside the game executable: "off", or "lag=N"
	/// (0-3 host frames). Absent file: matching on, lag 0. Polled once a second.
	void poll_match_config()
	{
		static ULONGLONG next = 0;
		const ULONGLONG now = GetTickCount64();
		if (now < next) return;
		next = now + 1000;
		wchar_t path[MAX_PATH]{};
		GetModuleFileNameW(nullptr, path, MAX_PATH);
		wchar_t *slash = wcsrchr(path, L'\\');
		if (!slash) return;
		wcscpy_s(slash + 1, MAX_PATH - (slash + 1 - path), L"DqjFrameMatch.ini");
		bool enabled = true; int lag = 0;
		if (FILE *f = _wfopen(path, L"r")) {
			char text[64]{};
			std::fread(text, 1, sizeof(text) - 1, f);
			std::fclose(f);
			if (std::strstr(text, "off")) enabled = false;
			if (const char *l = std::strstr(text, "lag=")) lag = std::clamp(std::atoi(l + 4), 0, 3);
		}
		if (enabled != g_matchEnabled || lag != g_matchLag) {
			char info[128];
			std::snprintf(info, sizeof(info), "[frame-match] config enabled=%d lag=%d", int(enabled), lag);
			reshade::log::message(reshade::log::level::info, info);
		}
		g_matchEnabled = enabled; g_matchLag = lag;
	}

	/// The MC frame the scene being presented was rendered with, or 0 if unknown.
	uint32_t match_target(const DqjHostState &host)
	{
		if (!host.active || host.matchedMcFrame == 0) return 0;
		auto &last = g_matchHistory[(g_matchNext + 7) % 8];
		if (last.hostFrame != host.frame) { g_matchHistory[g_matchNext] = MatchEntry{host.frame, host.matchedMcFrame}; g_matchNext = (g_matchNext + 1) % 8; }
		else last.mcFrame = host.matchedMcFrame;
		if (!g_matchEnabled) return 0;
		if (g_matchLag == 0) return host.matchedMcFrame;
		for (const auto &entry : g_matchHistory)
			if (entry.hostFrame == host.frame - g_matchLag) return entry.mcFrame;
		return 0;
	}

	/// Complete ring slot for this MC frame. If it was already overwritten, the
	/// complete slot closest to it (newest not after it, else oldest); -1 if none.
	int32_t find_slot(uint32_t target, bool &exact)
	{
		int32_t best = -1; int64_t bestDistance = INT64_MAX;
		exact = false;
		for (int32_t s = 0; s < g_slots; ++s) {
			const uint8_t *d = g_view + kSlotDesc + kSlotDescBytes * s;
			const int64_t seq = read<int64_t>(d);
			if (seq & 1 || seq == 0) continue;
			const int32_t delta = int32_t(uint32_t(read<int64_t>(d + 8)) - target);
			MemoryBarrier();
			if (read<int64_t>(d) != seq) continue;
			if (delta == 0) { exact = true; return s; }
			// Older frames rank before newer ones at the same distance.
			const int64_t distance = delta < 0 ? int64_t(-delta) * 2 : int64_t(delta) * 2 + 1;
			if (distance < bestDistance) { bestDistance = distance; best = s; }
		}
		return best;
	}

	/// Upload the Minecraft frame the host camera used (target), else the newest
	/// published one we haven't shown yet.
	void upload(effect_runtime *runtime, command_list *cmd, uint32_t target)
	{
		const int64_t published = read<int64_t>(g_view + 32);
		int32_t slot = -1;
		bool exact = false;
		if (target != 0) {
			if (g_hasFrame && uint32_t(g_uploadedMcFrame) == target) { ++g_matchHeld; return; }
			slot = find_slot(target, exact);
			if (!exact) ++g_matchMissed;
			// The nearest frame is already shown: keep it rather than re-uploading.
			if (slot >= 0 && !exact && g_hasFrame
				&& read<int64_t>(g_view + kSlotDesc + kSlotDescBytes * slot + 8) == g_uploadedMcFrame) return;
		}
		const bool matched = slot >= 0;
		if (!matched) {
			if (published == g_lastPublish)
			{
				// The live host lease gates the effect. Keep a validated frame through
				// a render stall; restoring the host here caused Steve/Wukong cuts.
				return;
			}
			slot = read<int32_t>(g_view + 40);
		}
		if (slot < 0 || slot >= g_slots)
			return;
		const uint8_t *sharedDesc = g_view + kSlotDesc + kSlotDescBytes * slot;
		const int64_t seq = read<int64_t>(sharedDesc);
		if (seq & 1)
			return;
		uint8_t descriptor[kSlotDescBytes];
		std::memcpy(descriptor, sharedDesc, sizeof descriptor);
		const uint8_t *desc = descriptor;
		if (read<int64_t>(desc) != seq) return;
		const uint32_t w = read<uint32_t>(desc + 24), h = read<uint32_t>(desc + 28);
		const size_t layer = size_t(w) * h * 4;
		if (w == 0 || h == 0 || w > 3840 || h > 2160 || layer * 3 > static_cast<size_t>(g_stride))
			return;
		const uint8_t *base = g_view + kHeader + g_stride * slot;
		g_candidate.resize(layer * 3);
		std::memcpy(g_candidate.data(), base, layer * 3);
		MemoryBarrier();
		if (read<int64_t>(sharedDesc) != seq) return;
#ifdef DQJ_COMPOSITOR_TEST
		// Private fixture fault injection: model an unavailable upload slot after
		// the newer producer frame was copied. Never compiled into release.
		if(GetFileAttributesW(L"DqjDropUploadTest.request")!=INVALID_FILE_ATTRIBUTES)return;
#endif
		device *dev = runtime->get_device();
		if (w != g_width || h != g_height)
		{
			destroy_layers(dev);
			if (!create_layer(dev, g_world, w, h, format::r8g8b8a8_unorm) ||
				!create_layer(dev, g_depth, w, h, format::r32_float) ||
				!create_layer(dev, g_overlay, w, h, format::r8g8b8a8_unorm))
			{
				destroy_layers(dev);
				return;
			}
			g_width = w;
			g_height = h;
			bind(runtime);
		}
		if (dev->get_api() == device_api::d3d12) {
			g_uploadQueue = runtime->get_command_queue();
			if (!g_uploadFence.handle && !dev->create_fence(0, fence_flags::none, &g_uploadFence)) return;
			g_uploadRowPitch = (w * 4 + 255u) & ~255u;
			g_uploadLayerBytes = (size_t(g_uploadRowPitch) * h + 511u) & ~size_t(511u);
			UploadSlot *free = nullptr;
			const auto completed = dev->get_completed_fence_value(g_uploadFence);
			for (auto &slot : g_upload) if (!slot.done || slot.done <= completed) { free = &slot; break; }
			if (!free) return; // Keep the previous frame instead of stalling the host.
			if (!free->buffer.handle) {
				if (!dev->create_resource(resource_desc(g_uploadLayerBytes * 3, memory_heap::upload, resource_usage::copy_source),
					nullptr, resource_usage::cpu_access, &free->buffer)) return;
				if (!dev->map_buffer_region(free->buffer, 0, UINT64_MAX, map_access::write_only, &free->mapped)) {
					dev->destroy_resource(free->buffer); free->buffer = resource{0}; return;
				}
			}
			for (size_t plane = 0; plane < 3; ++plane)
				for (size_t y = 0; y < h; ++y)
					std::memcpy(static_cast<uint8_t *>(free->mapped) + plane * g_uploadLayerBytes + y * g_uploadRowPitch,
						g_candidate.data() + plane * layer + y * w * 4, w * 4);
			const resource textures[] = {g_world.tex, g_depth.tex, g_overlay.tex};
			const resource_usage readable[] = {resource_usage::shader_resource, resource_usage::shader_resource, resource_usage::shader_resource};
			const resource_usage writable[] = {resource_usage::copy_dest, resource_usage::copy_dest, resource_usage::copy_dest};
			cmd->barrier(3, textures, readable, writable);
			for (size_t plane = 0; plane < 3; ++plane)
				cmd->copy_buffer_to_texture(free->buffer, plane * g_uploadLayerBytes, g_uploadRowPitch / 4, h, textures[plane], 0);
			cmd->barrier(3, textures, writable, readable);
			free->done = ++g_uploadNumber; g_pendingSignal = free->done;
		} else {
			subresource_data data;
			data.row_pitch = w * 4; data.slice_pitch = static_cast<uint32_t>(layer);
			data.data = g_candidate.data(); dev->update_texture_region(data, g_world.tex, 0);
			data.data = g_candidate.data() + layer; dev->update_texture_region(data, g_depth.tex, 0);
			data.data = g_candidate.data() + 2 * layer; dev->update_texture_region(data, g_overlay.tex, 0);
		}
		// Pixels and metadata now belong to the same private, verified snapshot.
		g_staging.swap(g_candidate);
		std::memcpy(g_uploadedDescriptor,descriptor,sizeof(g_uploadedDescriptor));
		g_lastPublish = published;
		g_uploadedMcFrame = read<int64_t>(desc + 8);
		if (matched && exact) ++g_matchUploads; else if (matched) ++g_nearestUploads; else ++g_newestUploads;
		g_lastFrameAt = GetTickCount64();
		g_mcNear = read<float>(desc + 32);
		g_mcFar = read<float>(desc + 36);
		g_mcFlags = read<int32_t>(desc + 44);
		g_mcPose.fov = read<float>(desc + 40);
		g_mcPose.yaw = read<float>(desc + 72);
		g_mcPose.pitch = read<float>(desc + 76);
		g_mcPose.roll = read<float>(desc + 80);
		g_mcPose.x = read<double>(desc + 48);
		g_mcPose.y = read<double>(desc + 56);
		g_mcPose.z = read<double>(desc + 64);
		g_mcPose.valid = true;
		g_hasFrame = true;
	}

	/// Camera-to-world rotation, as Minecraft builds it: rotationYXZ(pi - yaw, -pitch, roll) (camera looks down -z).
	void camera_rotation(const Pose &p, float m[3][3])
	{
		const float d2r = 3.14159265f / 180.0f;
		const float a = 3.14159265f - p.yaw * d2r, b = -p.pitch * d2r, c = p.roll * d2r;
		const float ca = std::cos(a), sa = std::sin(a), cb = std::cos(b), sb = std::sin(b), cc = std::cos(c), sc = std::sin(c);
		// Ry(a) * Rx(b) * Rz(c)
		const float ry[3][3] = {{ca, 0, sa}, {0, 1, 0}, {-sa, 0, ca}};
		const float rx[3][3] = {{1, 0, 0}, {0, cb, -sb}, {0, sb, cb}};
		const float rz[3][3] = {{cc, -sc, 0}, {sc, cc, 0}, {0, 0, 1}};
		float t[3][3] = {};
		for (int i = 0; i < 3; ++i)
			for (int j = 0; j < 3; ++j)
				for (int k = 0; k < 3; ++k)
					t[i][j] += ry[i][k] * rx[k][j];
		for (int i = 0; i < 3; ++i)
			for (int j = 0; j < 3; ++j)
			{
				m[i][j] = 0;
				for (int k = 0; k < 3; ++k)
					m[i][j] += t[i][k] * rz[k][j];
			}
	}

	/// Rows of R_mc^T * R_host: turns a ray in GTA's camera space into Minecraft's camera space.
	void warp_matrix(const Pose &host, const Pose &mc, float out[3][3])
	{
		float rh[3][3], rm[3][3];
		camera_rotation(host, rh);
		camera_rotation(mc, rm);
		for (int i = 0; i < 3; ++i)
			for (int j = 0; j < 3; ++j)
			{
				out[i][j] = 0;
				for (int k = 0; k < 3; ++k)
					out[i][j] += rm[k][i] * rh[k][j];
			}
	}

	/// Reload MCPassthrough.fx when the file changes (ReShade doesn't watch it), so tweaks don't need a GTA restart.
	void watch_effect_file(effect_runtime *runtime)
	{
		static DWORD next = 0;
		static FILETIME last = {};
		static wchar_t path[MAX_PATH] = {};
		if (GetTickCount() < next)
			return;
		next = GetTickCount() + 1000;
		if (path[0] == 0)
		{
			GetModuleFileNameW(nullptr, path, MAX_PATH);
			wchar_t *slash = wcsrchr(path, L'\\');
			if (slash)
				wcscpy_s(slash + 1, MAX_PATH - (slash + 1 - path), L"reshade-shaders\\Shaders\\MCPassthrough.fx");
		}
		WIN32_FILE_ATTRIBUTE_DATA info;
		if (!GetFileAttributesExW(path, GetFileExInfoStandard, &info))
			return;
		if (last.dwLowDateTime != 0 && CompareFileTime(&info.ftLastWriteTime, &last) != 0)
			runtime->reload_effect_next_frame(kEffect);
		last = info.ftLastWriteTime;
	}

	void on_present(effect_runtime *runtime)
	{
		watch_effect_file(runtime); // every frame, even when the effect failed to compile
		static ULONGLONG nextStats = 0;
		if (runtime == g_ownerRuntime && g_uploadFence.handle && GetTickCount64() >= nextStats) {
			nextStats = GetTickCount64() + 10000;
			char info[160];
			std::snprintf(info, sizeof(info), "D3D12 MC active=%d frame=%d uploads=%llu completed=%llu pending=%llu",
				int(g_active.load()), int(g_hasFrame),
				static_cast<unsigned long long>(g_uploadNumber),
				static_cast<unsigned long long>(runtime->get_device()->get_completed_fence_value(g_uploadFence)),
				static_cast<unsigned long long>(g_pendingSignal));
			reshade::log::message(reshade::log::level::info, info);
			char match[256];
			std::snprintf(match, sizeof(match), "[frame-match] enabled=%d lag=%d exactUploads=%llu nearestUploads=%llu held=%llu missed=%llu newestUploads=%llu shownMcFrame=%lld",
				int(g_matchEnabled), g_matchLag, static_cast<unsigned long long>(g_matchUploads), static_cast<unsigned long long>(g_nearestUploads), static_cast<unsigned long long>(g_matchHeld),
				static_cast<unsigned long long>(g_matchMissed), static_cast<unsigned long long>(g_newestUploads), static_cast<long long>(g_uploadedMcFrame));
			reshade::log::message(reshade::log::level::info, match);
		}
	}

	void cache_depth_variable(effect_runtime *runtime) {
		g_hostDepthVariable = effect_texture_variable{0}; g_depthVariableRuntime = runtime;
		runtime->enumerate_texture_variables(kEffect, [](effect_runtime *r, effect_texture_variable v) {
			char name[128]{}; r->get_texture_variable_name(v, name);
			if (std::strcmp(name, "HostDepthTex") == 0) g_hostDepthVariable = v;
		});
	}

	void on_begin_effects(effect_runtime *runtime, command_list *cmd, resource_view, resource_view)
	{
		if (g_ownerRuntime && g_ownerRuntime != runtime) return;
		if (g_depthVariableRuntime != runtime) cache_depth_variable(runtime);
		// Generic Depth is built into the pinned ReShade 6.8.0 binary and runs
		// before external add-ons. Observe its actual shader binding, never
		// register a second competing depth selector.
		bool depthReady = false;
		if (auto v = dqj_find_uniform(runtime, kEffect, "HostDepthReady"); v.handle)
			runtime->get_uniform_value_bool(v, &depthReady, 1);
		resource_view depthSrv{0};
		if (g_hostDepthVariable.handle)
			runtime->get_texture_binding(g_hostDepthVariable, &depthSrv, nullptr);
		if (((depthSrv.handle != g_reportedDepth || depthReady != g_reportedReady) && GetTickCount64() >= g_depthChangeReportAt) || GetTickCount64() >= g_depthReportAt) {
			g_depthReportAt = GetTickCount64() + 10000;
			g_depthChangeReportAt = GetTickCount64() + 1000;
			g_reportedDepth = depthSrv.handle; g_reportedReady = depthReady;
			char message[256]; uint64_t width = 0; unsigned height = 0, fmt = 0;
			if (depthSrv.handle && runtime->get_device()->get_api() == device_api::d3d12) {
				auto res = dqj_resource_from_view(runtime->get_device(), depthSrv);
				if (res.handle) { auto desc = reinterpret_cast<ID3D12Resource *>(res.handle)->GetDesc();
					width = desc.Width; height = desc.Height; fmt = unsigned(desc.Format); }
			}
			std::snprintf(message, sizeof(message), "[native-depth] ready=%d srv=%llx size=%llux%u format=%u near=%.4fm; world hidden if unavailable",
				int(depthReady), static_cast<unsigned long long>(depthSrv.handle), static_cast<unsigned long long>(width), height, fmt, double(g_hostNear.load()));
			reshade::log::message(reshade::log::level::info, message);
		}
		DqjHostState observedHost{};
		dqj_update_host_state(&observedHost);
		uint32_t bw = 0, bh = 0;
		runtime->get_screenshot_width_and_height(&bw, &bh);
		g_bbWidth = bw;
		g_bbHeight = bh;
		poll_match_config();
		const uint32_t matchTarget = match_target(observedHost);
		bool on = g_active && open_mapping();
		if (on) {
			g_ownerRuntime = runtime;
			upload(runtime, cmd, matchTarget);
		}
		on = on && g_hasFrame;
		pose_trace::tick(runtime,cmd,observedHost,g_uploadedDescriptor,g_lastPublish,on,
			g_uploadNumber,g_uploadFence.handle?runtime->get_device()->get_completed_fence_value(g_uploadFence):0,g_pendingSignal,depthReady,
			g_staging.size()>=4?read<uint32_t>(g_staging.data()):0);
		depth_trace::tick(runtime,cmd,depthReady?depthSrv:resource_view{0},
			on?g_staging.data():nullptr,g_width,g_height,g_mcFlags,g_lastPublish);
		// The technique stays enabled (preset); McActive gates it, so GTA passes through untouched until a
		// Minecraft frame is here. (Toggling techniques from inside this callback crashes ReShade.)
		if (const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, "McActive"); v.handle != 0)
			runtime->set_uniform_value_bool(v, on);
		if (!on)
			return;
		depth_probe::tick(runtime,cmd,depthReady?depthSrv:resource_view{0},g_staging.data(),g_width,g_height,g_mcNear,g_mcFar,g_mcFlags,g_hostNear.load(),g_hostFar.load());
		if (const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, "McPlanes"); v.handle != 0)
			runtime->set_uniform_value_float(v, g_mcNear, g_mcFar, float(g_mcFlags));
		// a scene's look overrides the preset's light matching and depth bias; the preset's values come back after
		auto look = [&](const char *name, float want, float &saved) {
			const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, name);
			if (v.handle == 0)
				return;
			if (want >= 0.0f)
			{
				if (saved < 0.0f)
					runtime->get_uniform_value_float(v, &saved, 1);
				runtime->set_uniform_value_float(v, want);
			}
			else if (saved >= 0.0f)
			{
				runtime->set_uniform_value_float(v, saved);
				saved = -1.0f;
			}
		};
		look("LightMatch", g_lookLight.load(), g_savedLight);
		look("DepthBias", g_lookBias.load(), g_savedBias);
		look("SlopeBias", g_lookSlope.load(), g_savedSlope);
		if (const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, "Shake"); v.handle != 0)
			runtime->set_uniform_value_float(v, g_shakeX.load(), g_shakeY.load(), g_shakeRoll.load());
		if (const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, "PortalWarp"); v.handle != 0)
			runtime->set_uniform_value_float(v, g_portalWarp.load());
		if (const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, "HostPlanes"); v.handle != 0)
			runtime->set_uniform_value_float(v, g_hostNear.load(), g_hostFar.load());

		// Re-projection from Minecraft's pose to GTA's latest (extrapolated by the effect's PosePrediction frames).
		Pose host, prev;
		{
			std::lock_guard<std::mutex> lock(g_poseLock);
			const unsigned lag = unsigned(std::clamp(g_poseLag.load(), 0, 2));
			if (g_hostPoseCount > lag)
				host = g_hostPoses[(g_hostPoseCount - 1 - lag) & 3];
			if (g_hostPoseCount > lag + 1)
				prev = g_hostPoses[(g_hostPoseCount - 2 - lag) & 3];
		}
		float predict = 0.0f;
		if (const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, "PosePrediction"); v.handle != 0)
			runtime->get_uniform_value_float(v, &predict, 1);
		const bool warp = host.valid && g_mcPose.valid && !g_cameraLocked;
		if (warp && prev.valid && predict != 0.0f)
		{
			auto delta = [](float a, float b) { float d = std::fmod(a - b + 540.0f, 360.0f) - 180.0f; return d; };
			host.yaw += delta(host.yaw, prev.yaw) * predict;
			host.pitch += (host.pitch - prev.pitch) * predict;
			host.roll += (host.roll - prev.roll) * predict;
			host.x += (host.x - prev.x) * predict;
			host.y += (host.y - prev.y) * predict;
			host.z += (host.z - prev.z) * predict;
		}
		float m[3][3] = {{1, 0, 0}, {0, 1, 0}, {0, 0, 1}};
		float t[3] = {0, 0, 0};
		if (warp)
		{
			warp_matrix(host, g_mcPose, m);
			// T = R_mc^T (host position - Minecraft's camera position), in Minecraft's camera space
			float rm[3][3];
			camera_rotation(g_mcPose, rm);
			const float d[3] = {float(host.x - g_mcPose.x), float(host.y - g_mcPose.y), float(host.z - g_mcPose.z)};
			for (int i = 0; i < 3; ++i)
				t[i] = rm[0][i] * d[0] + rm[1][i] * d[1] + rm[2][i] * d[2];
		}
		const char *rows[3] = {"WarpRow0", "WarpRow1", "WarpRow2"};
		for (int i = 0; i < 3; ++i)
			if (const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, rows[i]); v.handle != 0)
				runtime->set_uniform_value_float(v, m[i][0], m[i][1], m[i][2]);
		if (const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, "WarpT"); v.handle != 0)
			runtime->set_uniform_value_float(v, t[0], t[1], t[2]);
		const float d2r = 3.14159265f / 180.0f;
		const float tanHost = std::tan((warp ? host.fov : g_mcPose.fov) * d2r * 0.5f), tanMc = std::tan(g_mcPose.fov * d2r * 0.5f);
		if (const effect_uniform_variable v = dqj_find_uniform(runtime, kEffect, "WarpTan"); v.handle != 0)
			runtime->set_uniform_value_float(v, tanHost, tanMc, float(g_width) / float(g_height));
	}

	void on_reloaded_effects(effect_runtime *runtime)
	{
		if (g_ownerRuntime && g_ownerRuntime != runtime) return;
		cache_depth_variable(runtime);
		if (g_width != 0)
			bind(runtime);
	}

	void on_destroy_effect_runtime(effect_runtime *runtime)
	{
		if (g_ownerRuntime && g_ownerRuntime != runtime) return;
		depth_probe::destroy(runtime);
		depth_trace::destroy(runtime);
		destroy_layers(runtime->get_device());
		g_uploadQueue = nullptr; g_ownerRuntime = nullptr;
		g_hostDepthVariable = effect_texture_variable{0};
		g_depthVariableRuntime = nullptr;
	}

	void on_finish_present(command_queue *queue, swapchain *swapchain)
	{
		depth_trace::finish(queue);
		depth_probe::signal(swapchain);
		// Present has flushed the runtime's immediate list. Signal after those copies.
		if (!g_ownerRuntime || g_ownerRuntime->get_hwnd() != swapchain->get_hwnd() || !g_pendingSignal) return;
		if (g_uploadQueue && g_uploadQueue->signal(g_uploadFence, g_pendingSignal)) g_pendingSignal = 0;
	}
}

namespace compositor
{
	bool try_register(void *module)
	{
		if (g_registered)
			return true;
		if (!reshade::register_addon(module))
			return false;
		reshade::register_event<reshade::addon_event::reshade_begin_effects>(on_begin_effects);
		reshade::register_event<reshade::addon_event::reshade_present>(on_present);
		reshade::register_event<reshade::addon_event::reshade_reloaded_effects>(on_reloaded_effects);
		reshade::register_event<reshade::addon_event::destroy_effect_runtime>(on_destroy_effect_runtime);
		reshade::register_event<reshade::addon_event::finish_present>(on_finish_present);
		depth_trace::register_events();
		g_registered = true;
		reshade::log::message(reshade::log::level::info, "MCPassthrough: registered");
		return true;
	}

	void unregister(void *module)
	{
		if (g_registered.exchange(false))
			reshade::unregister_addon(module);
	}

	void set_active(bool active)
	{
		g_active = active;
	}

	void set_host_planes(float near_clip, float far_clip)
	{
		g_hostNear = near_clip;
		g_hostFar = far_clip;
	}

	void backbuffer_size(int &width, int &height)
	{
		width = int(g_bbWidth.load());
		height = int(g_bbHeight.load());
	}

	void set_camera_locked(bool locked)
	{
		g_cameraLocked = locked;
	}

	void set_screen_fx(float shake_x, float shake_y, float shake_roll, float portal_warp)
	{
		g_shakeX = shake_x;
		g_shakeY = shake_y;
		g_shakeRoll = shake_roll;
		g_portalWarp = portal_warp;
	}

	void set_look(float light_match, float depth_bias, float slope_bias)
	{
		g_lookLight = light_match;
		g_lookBias = depth_bias;
		g_lookSlope = slope_bias;
	}

	void set_host_pose(float yaw, float pitch, float roll, float fov, double x, double y, double z)
	{
		std::lock_guard<std::mutex> lock(g_poseLock);
		g_hostPoses[g_hostPoseCount & 3] = {yaw, pitch, roll, fov, x, y, z, true};
		++g_hostPoseCount;
	}

	void set_pose_lag(int frames)
	{
		g_poseLag = frames;
	}
}

