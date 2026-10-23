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
VPE_PCM_API void VpePcmStop(void* context, std::uint64_t voice);
VPE_PCM_API int VpePcmActive(void* context, std::uint64_t voice);
VPE_PCM_API float VpePcmPosition(void* context, std::uint64_t voice);
VPE_PCM_API int VpePcmSetBusGain(void* context, int bus, float gain);
VPE_PCM_API void VpePcmSetStreamGain(void* context, float gain);
VPE_PCM_API int VpePcmStreamPush(void* context, const void* samples, int frames, int channels, int rate, int encoding, std::int64_t sequence);
VPE_PCM_API void VpePcmFlushStream(void* context);
VPE_PCM_API void VpePcmEnableStream(void* context, int enabled);
VPE_PCM_API int VpePcmRender(void* context, float* output, int frames, int channels, int rate, int paused);
// Seven bus peaks, stream underflows/overflows/discontinuities, command drops, active voices.
VPE_PCM_API int VpePcmMetrics(void* context, float* values, int count);
// Optional DSP extension. Preparation allocates off-thread; adoption is at a render boundary.
VPE_PCM_API int VpePcmDspAbi();
VPE_PCM_API int VpePcmPrepareDsp(void* context, int rate);
VPE_PCM_API int VpePcmSetProcessing(void* context, const float* values, int count);
VPE_PCM_API int VpePcmProcessingMeters(void* context, float* values, int count);
VPE_PCM_API void VpePcmPreview(void* context, int bus, int enabled);
VPE_PCM_API float VpePcmEqResponse(void* context, int route, float hz);
