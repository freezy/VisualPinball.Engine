// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
#pragma once
#include <cstdint>
#if defined(_WIN32)
#define VPE_PCM_API extern "C" __declspec(dllexport)
#else
#define VPE_PCM_API extern "C" __attribute__((visibility("default")))
#endif

// Control/clip APIs have one main-thread producer; Render has one audio-thread consumer.
// StreamPush has its own single producer. Destroy only after both consumers are quiescent.
VPE_PCM_API void* VpePcmCreate();
VPE_PCM_API int VpePcmAbi();
VPE_PCM_API void VpePcmDestroy(void* context);
VPE_PCM_API int VpePcmRegisterClip(void* context, const float* samples, int frames, int channels, int rate);
VPE_PCM_API std::uint64_t VpePcmPlay(void* context, int clip, int bus, float gain, float pitch, float pan, int loop);
VPE_PCM_API int VpePcmUpdate(void* context, std::uint64_t voice, float gain, float pitch, float pan, int paused);
// The same with the voice's depth on the playfield: 0 at the player's end, 1 at the backbox. It only matters for the
// two mechanical buses while they play on four speakers (see VpePcmSetSurround); Play and Update use the middle.
VPE_PCM_API std::uint64_t VpePcmPlayAt(void* context, int clip, int bus, float gain, float pitch, float pan, float depth, int loop);
VPE_PCM_API int VpePcmUpdateAt(void* context, std::uint64_t voice, float gain, float pitch, float pan, float depth, int paused);
VPE_PCM_API void VpePcmStop(void* context, std::uint64_t voice);
VPE_PCM_API int VpePcmActive(void* context, std::uint64_t voice);
VPE_PCM_API float VpePcmPosition(void* context, std::uint64_t voice);
VPE_PCM_API int VpePcmSetBusGain(void* context, int bus, float gain);
VPE_PCM_API void VpePcmSetStreamGain(void* context, float gain);
VPE_PCM_API int VpePcmStreamPush(void* context, const void* samples, int frames, int channels, int rate, int encoding, std::int64_t sequence);
VPE_PCM_API void VpePcmFlushStream(void* context);
VPE_PCM_API void VpePcmEnableStream(void* context, int enabled);
// More live inputs besides the ROM stream (which is stream 0, also reachable through the functions above). Open and
// close on the main thread; each stream has one producer. Open returns 1..7, or -1 when all are in use.
VPE_PCM_API int VpePcmStreamOpen(void* context, int bus);
VPE_PCM_API void VpePcmStreamClose(void* context, int stream);
VPE_PCM_API int VpePcmStreamPushTo(void* context, int stream, const void* samples, int frames, int channels, int rate, int encoding, std::int64_t sequence);
VPE_PCM_API void VpePcmStreamSetGain(void* context, int stream, float gain);
VPE_PCM_API void VpePcmStreamFlush(void* context, int stream);
VPE_PCM_API void VpePcmStreamEnable(void* context, int stream, int enabled);
// Input frames queued before playing after start, flush or underflow: 0 plays as soon as data arrives (the ROM stream's
// default, which also leaves its queue uncontrolled), -1 (the default of opened streams) holds the producer's and the
// renderer's largest recent blocks plus a quarter, -2 does so too but plays at once until the first underflow. Buffered
// streams are steered to that size: up to 0.3% faster or slower against clock drift, and a jump back after a burst.
VPE_PCM_API int VpePcmStreamSetBuffer(void* context, int stream, int frames);
// Queued frames, buffer size steered to, underflows, overflows, discontinuities, catch-up drops.
VPE_PCM_API int VpePcmStreamMetrics(void* context, int stream, float* values, int count);
// Temporary output envelope, independent of processing preferences. Zero ms snaps; otherwise ramps per sample.
VPE_PCM_API int VpePcmSetTransitionGain(void* context, float gain, int milliseconds);
// The user's output level, 0..1, after the processing and the limiter. Unlike the transition envelope it also holds
// for a settings preview while the game is paused. Ramps over one render block, unless immediate is nonzero or it is
// the session's first level, which apply from the next block on.
VPE_PCM_API int VpePcmSetMasterGain(void* context, float gain, int immediate);
// Renders interleaved frames of 2 to 16 channels. Each bus plays in one of four output zones, and each zone on one
// stereo pair of channels (see VpePcmSetRouting); a pair the output doesn't have plays on the first one.
VPE_PCM_API int VpePcmRender(void* context, float* output, int frames, int channels, int rate, int paused);
// Output routing extension. Zones are stereo mixes with their own processing chain and limiter. A zone plays on
// channel pair zonePair (0..7: channels 1/2, 3/4, ...) of the output that renders the graph, or, with zoneTap 1..4, of
// another device that reads tap zoneTap-1. busZone has 7 entries (0..4), zonePair, zoneTap and zoneReserved 5. A
// reserved zone keeps its channels open on its output while no bus plays in it, so buses can move there later without
// reopening the output; zoneReserved may be null. Main thread.
VPE_PCM_API int VpePcmRoutingAbi();
VPE_PCM_API int VpePcmSetRouting(void* context, const int* busZone, const int* zonePair, const int* zoneTap, const int* zoneReserved);
// Four speakers for a mechanical bus (0 or 1): its zone from SetRouting is the front pair, near the player, and
// backZone the pair near the backbox. Each of its voices fades between the two by its depth, with constant power, and
// pans left to right in both. A bus whose front zone is its back zone plays in that zone alone. SetRouting keeps
// these settings. In VpePcmPreview, bus 7 and 8 are the back pair of bus 0 and 1. Main thread.
VPE_PCM_API int VpePcmSetSurround(void* context, int bus, int enabled, int backZone);
// Channels an output needs for the zones it plays that have a bus or are reserved: tap -1 is the rendering output, 0..3 a tap.
VPE_PCM_API int VpePcmOutputChannels(void* context, int tap);
// A tap carries its zones from the renderer to one reader on another device's clock. The reader attaches before it
// reads and detaches when it stops; without a reader the tap's zones are silent. TapRender waits for a jitter buffer
// sized from both sides' blocks and plays up to 0.3% faster or slower to hold it against clock drift.
VPE_PCM_API int VpePcmTapAttach(void* context, int tap, int attached);
VPE_PCM_API int VpePcmTapRender(void* context, int tap, float* output, int frames, int channels);
// Queued frames, buffer size steered to, underflows, overflows, catch-up drops.
VPE_PCM_API int VpePcmTapMetrics(void* context, int tap, float* values, int count);
// Seven bus peaks, stream underflows/overflows/discontinuities, command drops, active voices.
VPE_PCM_API int VpePcmMetrics(void* context, float* values, int count);
// Optional DSP extension. Preparation allocates off-thread; adoption is at a render boundary.
VPE_PCM_API int VpePcmDspAbi();
VPE_PCM_API int VpePcmPrepareDsp(void* context, int rate);
VPE_PCM_API int VpePcmSetProcessing(void* context, const float* values, int count);
VPE_PCM_API int VpePcmProcessingMeters(void* context, float* values, int count);
VPE_PCM_API void VpePcmPreview(void* context, int bus, int enabled);
VPE_PCM_API int VpePcmPreviewClip(void* context, int clip, int bus);
VPE_PCM_API float VpePcmEqResponse(void* context, int route, float hz);
// Optional low-latency device output (Windows WASAPI shared mode; null elsewhere or without a device).
// While an output exists it is the graph's only renderer: never call VpePcmRender concurrently.
// Destroy the output before the graph.
VPE_PCM_API void* VpePcmOutputCreate(void* context, int rate, int paused);
// Mode 0 shares the device through the Windows audio engine; 1 opens it exclusively (no mixing with other apps)
// with a period of bufferFrames (0 for the device's minimum). Exclusive fails while another app owns the device.
VPE_PCM_API void* VpePcmOutputCreateEx(void* context, int rate, int paused, int mode, int bufferFrames);
// Like CreateEx, on a chosen device: deviceId is an endpoint id from VpePcmDeviceList (UTF-8), or null/empty for the
// default device, which the stream then follows. With tap -1 the output renders the graph; with 0..3 it plays that
// tap (see VpePcmTapRender) and never renders. It opens as many channels as its zones need (VpePcmOutputChannels).
// Unlike CreateEx, an exclusive stream opened here continues shared when the device it has to reopen on (after the
// default device changed, say) can't be taken exclusively, rather than staying silent.
VPE_PCM_API void* VpePcmOutputOpen(void* context, int rate, int paused, int mode, int bufferFrames, const char* deviceId, int tap);
// Playback devices as UTF-8 lines "id<TAB>name<TAB>channels<TAB>default(0/1)". Returns the bytes needed including
// the terminator (also when the buffer is too small or null), 0 without device support.
VPE_PCM_API int VpePcmDeviceList(char* buffer, int length);
VPE_PCM_API void VpePcmOutputDestroy(void* output);
VPE_PCM_API void VpePcmOutputSetPaused(void* output, int paused);
// HRESULT of the last create that returned null because no device stream opened (0 when none failed).
VPE_PCM_API unsigned VpePcmOutputLastError();
// State (0 stopped, 1 playing, 2 waiting for a device), render callbacks, period/buffer/queued/margin ms, underruns, reopens, last HRESULT, rate,
// seconds of the first and last underrun since creation, longest gap between device wake-ups in ms.
VPE_PCM_API int VpePcmOutputMetrics(void* output, float* values, int count);
