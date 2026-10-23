// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
#include "PcmRenderer.h"
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstring>
#include <memory>
#include <vector>

namespace {
constexpr int BusCount = 7, VoiceCount = 512, QueueSize = 4096, StreamSize = 8192, BlockSize = 4096;
static_assert(std::atomic<float>::is_always_lock_free && std::atomic<std::uint64_t>::is_always_lock_free, "PCM control atomics must be lock-free");
float Finite(float value) { return std::isfinite(value) ? value : 0.f; }
bool Controls(float gain, float pitch, float pan) { return std::isfinite(gain) && gain >= 0 && gain <= 4 && std::isfinite(pitch) && pitch >= .01f && pitch <= 3 && std::isfinite(pan) && pan >= -1 && pan <= 1; }
struct Clip { std::vector<float> samples; int frames, channels, rate; };
struct Command { std::uint64_t id = 0; Clip* clip = nullptr; int bus = 0; float gain = 1, pitch = 1, pan = 0; bool start = false, loop = false, paused = false; };
struct Slot { std::atomic<std::uint64_t> generation{0}, stop{0}; std::atomic<int> state{0}; std::atomic<float> seconds{0}; };
struct Voice { Command controls{}; double cursor = 0; float left = 0, right = 0; bool active = false, fresh = true; };
struct Stereo { float left = 0, right = 0; };
struct Context {
	std::vector<std::unique_ptr<Clip>> clips; // Main thread only; the queue publishes stable Clip pointers.
	std::size_t clipSamples = 0;
	std::array<Slot, VoiceCount> slots{};
	std::array<Voice, VoiceCount> voices{};
	std::array<Command, QueueSize> commands{};
	std::atomic<std::uint64_t> write{0}, read{0};
	std::uint64_t next = 0;
	std::array<std::atomic<float>, BusCount> busGain{};
	std::array<float, BusCount * BlockSize * 2> buses{};
	std::array<std::atomic<float>, 12> metrics{};
	std::array<Stereo, StreamSize> stream{};
	std::atomic<std::uint64_t> streamWrite{0}, streamRead{0}, flush{0};
	std::atomic<int> streamRate{0};
	std::atomic<int> streamEnabled{1};
	std::atomic<float> streamGain{1};
	float currentStreamGain = -1;
	std::uint64_t flushed = 0;
	std::int64_t sequence = 0; // Stream producer only.
	double streamFraction = 0;
	Context() { for (auto& gain : busGain) gain.store(1); for (auto& metric : metrics) metric.store(0); }
	bool Push(const Command& command) {
		const auto w = write.load(std::memory_order_relaxed);
		if (w - read.load(std::memory_order_acquire) >= QueueSize) { metrics[10].store(metrics[10].load() + 1); return false; }
		commands[w % QueueSize] = command; write.store(w + 1, std::memory_order_release); return true;
	}
};
Context* C(void* value) { return static_cast<Context*>(value); }
int Index(std::uint64_t id) { return static_cast<int>(id % VoiceCount); }
bool Current(Context* c, std::uint64_t id) { return id && c->slots[Index(id)].generation.load(std::memory_order_acquire) == id; }
void End(Context& c, Voice& voice) {
	voice.active = false;
	if (Current(&c, voice.controls.id)) c.slots[Index(voice.controls.id)].state.store(3, std::memory_order_release);
}
Stereo Sample(const Clip& clip, double cursor, bool loop) {
	const int a = static_cast<int>(cursor), b = a + 1 < clip.frames ? a + 1 : loop ? 0 : a;
	const float fraction = static_cast<float>(cursor - a);
	auto channel = [&](int ch) { const auto first = clip.samples[a * clip.channels + ch]; return first + (clip.samples[b * clip.channels + ch] - first) * fraction; };
	const float left = channel(0);
	return {left, clip.channels == 1 ? left : channel(1)};
}
}

void* VpePcmCreate() { try { return new Context(); } catch (...) { return nullptr; } }
int VpePcmAbi() { return 1; }
void VpePcmDestroy(void* context) { delete C(context); }
int VpePcmRegisterClip(void* context, const float* samples, int frames, int channels, int rate) {
	if (!context || !samples || frames <= 0 || frames > 48000 * 60 * 10 || (channels != 1 && channels != 2) || rate < 8000 || rate > 192000) return -1;
	try {
		auto& owner = *C(context);
		const auto count = static_cast<std::size_t>(frames) * channels;
		if (count > 8 * 1024 * 1024 || owner.clipSamples + count > 64 * 1024 * 1024 || owner.clips.size() >= 4096) return -1;
		auto clip = std::make_unique<Clip>(); clip->frames = frames; clip->channels = channels; clip->rate = rate;
		clip->samples.assign(samples, samples + static_cast<size_t>(frames) * channels);
		for (auto& value : clip->samples) value = Finite(value);
		owner.clips.push_back(std::move(clip)); owner.clipSamples += count; return static_cast<int>(owner.clips.size() - 1);
	} catch (...) { return -1; }
}
std::uint64_t VpePcmPlay(void* context, int clip, int bus, float gain, float pitch, float pan, int loop) {
	if (!context || clip < 0 || clip >= static_cast<int>(C(context)->clips.size()) || bus < 0 || bus >= BusCount || !Controls(gain, pitch, pan)) return 0;
	auto& c = *C(context);
	for (int i = 0; i < VoiceCount; ++i) {
		const int state = c.slots[i].state.load(std::memory_order_acquire);
		if (state != 0 && state != 3) continue;
		const auto id = (++c.next) * VoiceCount + i;
		c.slots[i].seconds.store(0); c.slots[i].generation.store(id, std::memory_order_release); c.slots[i].stop.store(0); c.slots[i].state.store(1);
		Command command; command.id = id; command.clip = c.clips[clip].get(); command.bus = bus; command.gain = gain; command.pitch = pitch; command.pan = pan; command.loop = loop != 0; command.start = true;
		if (c.Push(command)) return id;
		c.slots[i].state.store(3); return 0;
	}
	c.metrics[10].store(c.metrics[10].load() + 1); return 0;
}
int VpePcmUpdate(void* context, std::uint64_t id, float gain, float pitch, float pan, int paused) {
	if (!context || !Current(C(context), id) || !Controls(gain, pitch, pan)) return 0;
	Command command; command.id = id; command.gain = gain; command.pitch = pitch; command.pan = pan; command.paused = paused != 0;
	return C(context)->Push(command) ? 1 : 0;
}
void VpePcmStop(void* context, std::uint64_t id) { if (context && Current(C(context), id)) C(context)->slots[Index(id)].stop.store(id, std::memory_order_release); }
int VpePcmActive(void* context, std::uint64_t id) {
	if (!context || !Current(C(context), id)) return 0;
	const auto& slot = C(context)->slots[Index(id)];
	return slot.stop.load() != id && (slot.state.load() == 1 || slot.state.load() == 2) ? 1 : 0;
}
int VpePcmSetBusGain(void* context, int bus, float gain) {
	if (!context || bus < 0 || bus >= BusCount || !std::isfinite(gain) || gain < 0 || gain > 4) return 0;
	C(context)->busGain[bus].store(gain, std::memory_order_relaxed); return 1;
}
float VpePcmPosition(void* context, std::uint64_t id) { return context && Current(C(context), id) ? C(context)->slots[Index(id)].seconds.load() : 0.f; }
void VpePcmSetStreamGain(void* context, float gain) { if (context && std::isfinite(gain) && gain >= 0 && gain <= 1) C(context)->streamGain.store(gain); }
void VpePcmFlushStream(void* context) { if (context) C(context)->flush.fetch_add(1, std::memory_order_release); }
void VpePcmEnableStream(void* context, int enabled) { if (context) C(context)->streamEnabled.store(enabled != 0 ? 1 : 0, std::memory_order_release); }
int VpePcmStreamPush(void* context, const void* samples, int frames, int channels, int rate, int encoding, std::int64_t sequence) {
	if (!context || !samples || frames <= 0 || frames > StreamSize || (channels != 1 && channels != 2) || rate < 8000 || rate > 192000 || (encoding != 0 && encoding != 1)) return 0;
	auto& c = *C(context);
	const auto previousRate = c.streamRate.load();
	if (previousRate && previousRate != rate) return 0; // A new format requires a new session.
	c.streamRate.store(rate);
	if (c.sequence && sequence != c.sequence + 1) { c.flush.fetch_add(1); c.metrics[9].store(c.metrics[9].load() + 1); }
	c.sequence = sequence;
	const auto w = c.streamWrite.load(std::memory_order_relaxed), r = c.streamRead.load(std::memory_order_acquire);
	if (w - r + frames > StreamSize) { c.flush.fetch_add(1); c.metrics[8].store(c.metrics[8].load() + 1); return 0; }
	const auto* bytes = static_cast<const unsigned char*>(samples);
	for (int f = 0; f < frames; ++f) {
		auto sample = [&](int channel) {
			const auto offset = static_cast<size_t>(f * channels + channel) * (encoding == 1 ? 4 : 2);
			if (encoding == 1) { float value; std::memcpy(&value, bytes + offset, 4); return Finite(value); }
			const auto value = static_cast<std::int16_t>(static_cast<unsigned int>(bytes[offset]) | (static_cast<unsigned int>(bytes[offset+1]) << 8));
			return value / 32768.f;
		};
		const auto left = sample(0); c.stream[(w + f) % StreamSize] = {left, channels == 1 ? left : sample(1)};
	}
	c.streamWrite.store(w + frames, std::memory_order_release); return frames;
}
int VpePcmRender(void* context, float* output, int frames, int channels, int rate, int paused) {
	if (!output || frames < 0 || channels <= 0 || channels > 8) return 0;
	std::fill_n(output, static_cast<size_t>(frames) * channels, 0.f);
	if (!context || channels != 2 || rate < 8000 || rate > 192000) return 0; // This milestone exposes stereo only.
	if (frames > BlockSize) {
		for (int offset = 0; offset < frames; offset += BlockSize) VpePcmRender(context, output + offset * 2, std::min(BlockSize, frames-offset), 2, rate, paused);
		return 1;
	}
	auto& c = *C(context);
	auto r = c.read.load(std::memory_order_relaxed); const auto w = c.write.load(std::memory_order_acquire);
	while (r != w) {
		const auto command = c.commands[r % QueueSize]; ++r;
		if (!Current(&c, command.id)) continue;
		auto& voice = c.voices[Index(command.id)];
		if (command.start) { voice = Voice{}; voice.controls = command; voice.active = true; c.slots[Index(command.id)].state.store(2); }
		else if (voice.active && voice.controls.id == command.id) { voice.controls.gain = command.gain; voice.controls.pitch = command.pitch; voice.controls.pan = command.pan; voice.controls.paused = command.paused; }
	}
	c.read.store(r, std::memory_order_release);
	std::array<float, BusCount> gains{}, peaks{};
	for (int bus = 0; bus < BusCount; ++bus) { gains[bus] = c.busGain[bus].load(std::memory_order_relaxed); std::fill_n(c.buses.data() + bus*BlockSize*2, frames*2, 0.f); }
	int active = 0;
	for (auto& voice : c.voices) {
		if (!voice.active) continue;
		const auto& control = voice.controls;
		if (!Current(&c, control.id) || c.slots[Index(control.id)].stop.load(std::memory_order_acquire) == control.id) { End(c, voice); continue; }
		++active;
		if (paused || control.paused) continue;
		const auto& clip = *control.clip;
		const float gain = control.gain * gains[control.bus];
		// Mono constant-power panning. Authored stereo retains both channels at center.
		const float left = clip.channels == 1 ? std::sqrt((1-control.pan)*.5f) : std::min(1.f, 1-control.pan);
		const float right = clip.channels == 1 ? std::sqrt((1+control.pan)*.5f) : std::min(1.f, 1+control.pan);
		const float targetLeft = left * gain, targetRight = right * gain;
		if (voice.fresh) { voice.left = targetLeft; voice.right = targetRight; voice.fresh = false; }
		const int ramp = std::max(1, rate/200);
		const float leftStep = (targetLeft-voice.left)/ramp, rightStep = (targetRight-voice.right)/ramp;
		float* bus = c.buses.data() + control.bus*BlockSize*2;
		for (int f = 0; f < frames; ++f) {
			if (voice.cursor >= clip.frames) { if (control.loop) voice.cursor = std::fmod(voice.cursor, clip.frames); else { End(c, voice); break; } }
			const auto sample = Sample(clip, voice.cursor, control.loop);
			if (f < ramp) { voice.left += leftStep; voice.right += rightStep; } else { voice.left = targetLeft; voice.right = targetRight; }
			bus[f*2] += sample.left * voice.left; bus[f*2+1] += sample.right * voice.right;
			voice.cursor += static_cast<double>(clip.rate) / rate * control.pitch;
		}
		if (voice.active) {
			c.slots[Index(control.id)].seconds.store(static_cast<float>((control.loop ? std::fmod(voice.cursor, clip.frames) : voice.cursor) / clip.rate));
			if (!control.loop && voice.cursor >= clip.frames) End(c, voice);
		}
	}
	const auto flush = c.flush.load(std::memory_order_acquire);
	const bool streamEnabled = c.streamEnabled.load(std::memory_order_acquire) != 0;
	if (paused || !streamEnabled || flush != c.flushed) { c.streamRead.store(c.streamWrite.load(std::memory_order_acquire), std::memory_order_release); c.streamFraction = 0; c.flushed = flush; }
	if (paused || !streamEnabled) c.currentStreamGain = -1;
	if (!paused && streamEnabled) {
		auto read = c.streamRead.load(std::memory_order_relaxed); const auto written = c.streamWrite.load(std::memory_order_acquire);
		const auto inputRate = c.streamRate.load(); const float targetGain = c.streamGain.load() * gains[2];
		if (inputRate && (c.currentStreamGain < 0 || targetGain == 0)) c.currentStreamGain = targetGain;
		const int ramp = std::max(1, rate/200); const float gainStep = (targetGain-c.currentStreamGain)/ramp;
		const double step = static_cast<double>(inputRate) / rate;
		bool underflow = false;
		for (int f = 0; f < frames && inputRate; ++f) {
			if (read + 1 >= written) { underflow = true; c.streamFraction = 0; break; }
			const auto a = c.stream[read % StreamSize], b = c.stream[(read+1) % StreamSize];
			c.currentStreamGain = f < ramp ? c.currentStreamGain + gainStep : targetGain;
			const float l = (a.left + (b.left-a.left)*static_cast<float>(c.streamFraction)) * c.currentStreamGain;
			const float rr = (a.right + (b.right-a.right)*static_cast<float>(c.streamFraction)) * c.currentStreamGain;
			c.buses[2*BlockSize*2 + f*2] += l; c.buses[2*BlockSize*2 + f*2+1] += rr;
			c.streamFraction += step; const auto advance = static_cast<std::uint64_t>(c.streamFraction);
			read = std::min(read + advance, written); c.streamFraction -= advance;
		}
		c.streamRead.store(read, std::memory_order_release);
		if (underflow) c.metrics[7].store(c.metrics[7].load() + 1);
	}
	for (int bus = 0; bus < BusCount; ++bus) {
		const float* samples = c.buses.data() + bus*BlockSize*2;
		for (int i = 0; i < frames*2; ++i) { output[i] += samples[i]; peaks[bus] = std::max(peaks[bus], std::abs(samples[i])); }
		c.metrics[bus].store(peaks[bus], std::memory_order_relaxed);
	}
	c.metrics[11].store(static_cast<float>(active));
	for (int i = 0; i < frames*channels; ++i) output[i] = Finite(output[i]);
	return 1;
}
int VpePcmMetrics(void* context, float* values, int count) {
	if (!context || !values || count != 12) return 0;
	for (int i = 0; i < count; ++i) values[i] = C(context)->metrics[i].load(std::memory_order_relaxed);
	return 1;
}
