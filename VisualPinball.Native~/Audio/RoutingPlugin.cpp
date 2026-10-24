// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
// Development routing probe; not enabled by the production player.
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <new>
#include "ThirdParty/Unity/AudioPluginInterface.h"

namespace {
constexpr int SlotCount = 16;
constexpr int ChannelCount = 8;
constexpr int MetricCount = 24;
static_assert(std::atomic<float>::is_always_lock_free, "Audio metrics require lock-free floats");
static_assert(std::atomic<std::uint64_t>::is_always_lock_free, "Generator controls require lock-free snapshots");
std::atomic<std::uint64_t> generatorSettings{0};
struct Metrics {
	std::array<std::atomic<float>, MetricCount> values{};
};
std::array<Metrics, SlotCount> metrics;
struct Effect {
	// X, Y, mode (0: passthrough, 1: cabinet, 2: backbox), diagnostic slot.
	std::array<std::atomic<float>, 4> parameters{{.5f, .5f, 0.f, 1.f}};
	double phase = 0;
};

void Peak(std::atomic<float>& target, float value) {
	// One audio-thread writer per slot. The probe resets only between silent cases.
	if (value > target.load(std::memory_order_relaxed)) target.store(value, std::memory_order_relaxed);
}

UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK Attenuation(UnityAudioEffectState*, float, float, float* result) {
	*result = 1.f;
	return UNITY_AUDIODSP_OK;
}
UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK Create(UnityAudioEffectState* state) {
	state->effectdata = new(std::nothrow) Effect();
	if (!state->effectdata) return UNITY_AUDIODSP_ERR_UNSUPPORTED;
	if (state->hostapiversion >= 0x010300 && state->spatializerdata)
		state->spatializerdata->distanceattenuationcallback = Attenuation;
	return UNITY_AUDIODSP_OK;
}
UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK Release(UnityAudioEffectState* state) {
	delete static_cast<Effect*>(state->effectdata);
	state->effectdata = nullptr;
	return UNITY_AUDIODSP_OK;
}
UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK Set(UnityAudioEffectState* state, int index, float value) {
	if (!state->effectdata || index < 0 || index >= 4 || !std::isfinite(value)) return UNITY_AUDIODSP_ERR_UNSUPPORTED;
	if ((index < 2 && (value < 0 || value > 1)) || (index == 2 && (value < 0 || value > 2 || value != std::floor(value))) ||
		(index == 3 && (value < 1 || value >= SlotCount || value != std::floor(value)))) return UNITY_AUDIODSP_ERR_UNSUPPORTED;
	static_cast<Effect*>(state->effectdata)->parameters[index].store(value, std::memory_order_relaxed);
	return UNITY_AUDIODSP_OK;
}
UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK Get(UnityAudioEffectState* state, int index, float* value, char* text) {
	if (!state->effectdata || !value || index < 0 || index >= 4) return UNITY_AUDIODSP_ERR_UNSUPPORTED;
	*value = static_cast<Effect*>(state->effectdata)->parameters[index].load(std::memory_order_relaxed);
	if (text) text[0] = 0;
	return UNITY_AUDIODSP_OK;
}

UNITY_AUDIODSP_RESULT Process(UnityAudioEffectState* state, float* input, float* output, unsigned int frames, int inputs, int outputs, bool meter) {
	if (!output || outputs <= 0) return UNITY_AUDIODSP_ERR_UNSUPPORTED;
	if (!input || !state->effectdata || inputs <= 0 || inputs > ChannelCount || outputs > ChannelCount) {
		std::fill(output, output + static_cast<size_t>(frames) * outputs, 0.f);
		return UNITY_AUDIODSP_ERR_UNSUPPORTED;
	}
	auto& effect = *static_cast<Effect*>(state->effectdata);
	const int mode = meter ? 0 : static_cast<int>(effect.parameters[2].load(std::memory_order_relaxed));
	const int slot = meter ? 0 : static_cast<int>(effect.parameters[3].load(std::memory_order_relaxed));
	auto& stats = metrics[slot].values;
	stats[0].store(static_cast<float>(inputs), std::memory_order_relaxed);
	stats[1].store(static_cast<float>(outputs), std::memory_order_relaxed);
	stats[2].store(stats[2].load(std::memory_order_relaxed) + 1, std::memory_order_relaxed);
	stats[4].store(static_cast<float>(state->samplerate), std::memory_order_relaxed);
	stats[5].store(static_cast<float>(state->hostapiversion), std::memory_order_relaxed);
	Peak(stats[6], static_cast<float>(frames));
	const bool supported = inputs == outputs && (mode == 0 || outputs == ChannelCount);
	if (!supported) stats[3].store(1, std::memory_order_relaxed);
	std::array<float, ChannelCount> inputPeak{}, outputPeak{};
	const float x = effect.parameters[0].load(std::memory_order_relaxed);
	const float y = effect.parameters[1].load(std::memory_order_relaxed);
	const std::array<float, 4> gains{{std::sqrt((1-x)*(1-y)), std::sqrt(x*(1-y)), std::sqrt((1-x)*y), std::sqrt(x*y)}};
	for (unsigned int frame = 0; frame < frames; ++frame) {
		std::array<float, ChannelCount> captured{};
		std::copy_n(input + static_cast<size_t>(frame) * inputs, inputs, captured.data());
		const float* in = captured.data();
		float* out = output + static_cast<size_t>(frame) * outputs;
		std::fill_n(out, outputs, 0.f);
		for (int c = 0; c < inputs; ++c) if (std::isfinite(in[c])) inputPeak[c] = std::max(inputPeak[c], std::abs(in[c]));
		if (supported) {
			if (mode == 0) {
				for (int c = 0; c < outputs; ++c) out[c] = std::isfinite(in[c]) ? in[c] : 0.f;
			} else if (mode == 1) {
				// Offline matrix reference uses an equal-power dual-mono pair. Unity's
				// ordinary spatializer supplies stereo; its eight-channel mode is a negative control.
				const float mono = .7071067811865475f * (in[0] + in[1]);
				if (std::isfinite(mono)) for (int c = 0; c < 4; ++c) out[c + 4] = mono * gains[c];
			} else {
				out[0] = std::isfinite(in[0]) ? in[0] : 0.f;
				out[1] = std::isfinite(in[1]) ? in[1] : 0.f;
			}
		}
		for (int c = 0; c < outputs; ++c) outputPeak[c] = std::max(outputPeak[c], std::abs(out[c]));
	}
	for (int c = 0; c < ChannelCount; ++c) { Peak(stats[8+c], inputPeak[c]); Peak(stats[16+c], outputPeak[c]); }
	return UNITY_AUDIODSP_OK;
}
UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK Spatialize(UnityAudioEffectState* s, float* in, float* out, unsigned int n, int ic, int oc) {
	return Process(s, in, out, n, ic, oc, false);
}
UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK Meter(UnityAudioEffectState* s, float* in, float* out, unsigned int n, int ic, int oc) {
	return Process(s, in, out, n, ic, oc, true);
}

UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK Generate(UnityAudioEffectState* state, float*, float* output, unsigned int frames, int inputs, int outputs) {
	if (!output || outputs <= 0) return UNITY_AUDIODSP_ERR_UNSUPPORTED;
	std::fill_n(output, static_cast<size_t>(frames) * outputs, 0.f);
	if (!state->effectdata || state->samplerate == 0 || outputs > ChannelCount) return UNITY_AUDIODSP_ERR_UNSUPPORTED;
	const auto settings = generatorSettings.load(std::memory_order_relaxed);
	const auto amplitudeBits = static_cast<std::uint32_t>(settings);
	float amplitude = 0;
	std::memcpy(&amplitude, &amplitudeBits, sizeof(amplitude));
	const int channel = static_cast<int>((settings >> 32) & 7);
	const int frequency = static_cast<int>((settings >> 35) & 2047);
	auto& effect = *static_cast<Effect*>(state->effectdata);
	auto& stats = metrics[2].values;
	stats[0].store(static_cast<float>(inputs), std::memory_order_relaxed);
	stats[1].store(static_cast<float>(outputs), std::memory_order_relaxed);
	stats[2].store(stats[2].load(std::memory_order_relaxed) + 1, std::memory_order_relaxed);
	stats[3].store(outputs == ChannelCount ? 0.f : 1.f, std::memory_order_relaxed);
	stats[4].store(static_cast<float>(state->samplerate), std::memory_order_relaxed);
	stats[5].store(static_cast<float>(state->hostapiversion), std::memory_order_relaxed);
	Peak(stats[6], static_cast<float>(frames));
	float peak = 0;
	for (unsigned int frame = 0; frame < frames; ++frame) {
		const float value = amplitude * static_cast<float>(std::sin(effect.phase * 6.283185307179586));
		effect.phase += static_cast<double>(frequency) / state->samplerate;
		effect.phase -= std::floor(effect.phase);
		if (channel < outputs) { output[static_cast<size_t>(frame) * outputs + channel] = value; peak = std::max(peak, std::abs(value)); }
	}
	Peak(stats[16 + channel], peak);
	return UNITY_AUDIODSP_OK;
}

UnityAudioParameterDefinition parameters[4] = {
	{"X", "", "Left (0) to right (1)", 0, 1, .5f, 1, 1},
	{"Y", "", "Lockbar (0) to backbox (1)", 0, 1, .5f, 1, 1},
	{"Mode", "", "0 passthrough, 1 cabinet, 2 backbox", 0, 2, 0, 1, 1},
	{"Slot", "", "Diagnostic source slot (master meter reserves zero)", 1, 15, 1, 1, 1}
};
struct Definitions {
	UnityAudioEffectDefinition panner{}, meter{}, generator{};
	UnityAudioEffectDefinition* pointers[3];
	Definitions() {
		panner.structsize = sizeof(panner);
		panner.paramstructsize = sizeof(UnityAudioParameterDefinition);
		// Only the 1.4.2 ABI is required; no decoder-only output-count flag is used.
		panner.apiversion = 0x010402;
		panner.pluginversion = 0x000100;
		panner.channels = 0;
		panner.numparameters = 4;
		panner.paramdefs = parameters;
		panner.flags = UnityAudioEffectDefinitionFlags_IsSpatializer | UnityAudioEffectDefinitionFlags_AppliesDistanceAttenuation;
		std::memcpy(panner.name, "VPE Cabinet Panner", sizeof("VPE Cabinet Panner"));
		panner.create = Create; panner.release = Release;
		panner.process = Spatialize; panner.setfloatparameter = Set; panner.getfloatparameter = Get;
		meter = panner; meter.flags = 0; meter.numparameters = 0; meter.paramdefs = nullptr;
		meter.setfloatparameter = nullptr; meter.getfloatparameter = nullptr;
		meter.process = Meter;
		std::memcpy(meter.name, "VPE Routing Meter", sizeof("VPE Routing Meter"));
		generator = meter; generator.channels = ChannelCount; generator.process = Generate;
		std::memcpy(generator.name, "VPE Routing Generator", sizeof("VPE Routing Generator"));
		pointers[0] = &panner; pointers[1] = &meter; pointers[2] = &generator;
	}
};
}

extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION UnityGetAudioEffectDefinitions(UnityAudioEffectDefinition*** result) {
	static Definitions definitions;
	*result = definitions.pointers;
	return 3;
}
extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION VpeRoutingSetGenerator(int channel, int frequency, float amplitude) {
	if (channel < 0 || channel >= ChannelCount || frequency < 20 || frequency > 2000 || !std::isfinite(amplitude) || amplitude < 0 || amplitude > .02f) return 0;
	std::uint32_t bits = 0;
	std::memcpy(&bits, &amplitude, sizeof(bits));
	generatorSettings.store((static_cast<std::uint64_t>(channel | (frequency << 3)) << 32) | bits, std::memory_order_relaxed);
	return 1;
}
extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION VpeRoutingGetMetrics(int slot, float* result, int count) {
	if (slot < 0 || slot >= SlotCount || !result || count != MetricCount) return 0;
	for (int i = 0; i < count; ++i) result[i] = metrics[slot].values[i].load(std::memory_order_relaxed);
	return 1;
}
extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION VpeRoutingClearMetrics(int slot) {
	if (slot < 0 || slot >= SlotCount) return 0;
	for (auto& value : metrics[slot].values) value.store(0, std::memory_order_relaxed);
	return 1;
}
