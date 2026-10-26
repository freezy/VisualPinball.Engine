// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
#include "PcmRenderer.h"
#include "AudioProcessing.h"
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstring>
#include <memory>
#include <vector>

namespace {
constexpr int BusCount = 7, VoiceCount = 512, QueueSize = 4096, StreamSize = 8192, BlockSize = 4096, StreamCount = 8, RomBus = 2;
static_assert(std::atomic<float>::is_always_lock_free && std::atomic<std::uint64_t>::is_always_lock_free, "PCM control atomics must be lock-free");
float Finite(float value) { return std::isfinite(value) ? value : 0.f; }
bool Controls(float gain, float pitch, float pan) { return std::isfinite(gain) && gain >= 0 && gain <= 4 && std::isfinite(pitch) && pitch >= .01f && pitch <= 3 && std::isfinite(pan) && pan >= -1 && pan <= 1; }
struct Clip { std::vector<float> samples; int frames, channels, rate; };
struct Command { std::uint64_t id = 0; Clip* clip = nullptr; int bus = 0; float gain = 1, pitch = 1, pan = 0; bool start = false, loop = false, paused = false; };
struct Slot { std::atomic<std::uint64_t> generation{0}, stop{0}; std::atomic<int> state{0}; std::atomic<float> seconds{0}; };
struct Voice { Command controls{}; double cursor = 0; float left = 0, right = 0; bool active = false, fresh = true; };
struct Stereo { float left = 0, right = 0; };
// One live PCM input: a single producer pushes, the renderer reads. Slot 0 is the ROM stream of the original API;
// slots 1.. are opened for other producers (plugin game audio, videos). A jitter buffer waits for `target` frames
// after start, flush or underflow, so producers that deliver in bursts don't click on every gap.
struct StreamInput {
	std::array<Stereo, StreamSize> ring{};
	std::atomic<std::uint64_t> write{0}, read{0}, flush{0};
	std::atomic<int> rate{0}, enabled{1}, open{0}, bus{RomBus};
	// Closes so far, and the count the renderer last saw while the slot was closed. Only a slot whose every close was
	// seen by the renderer is reused, so it can't be reset while the renderer still plays an earlier stream in it.
	std::atomic<std::uint32_t> closes{0}, seenCloses{0};
	std::atomic<int> target{0}; // frames to queue before playing; -1 sizes it from the producer's blocks, -2 does so after the first underflow
	std::atomic<int> largestPush{0}, largestPull{0}; // biggest block in and out, in input frames
	std::atomic<float> gain{1};
	std::atomic<float> underflows{0}, overflows{0}, discontinuities{0}, drops{0};
	std::int64_t sequence = 0; // producer only
	// renderer only
	float currentGain = -1;
	std::uint64_t flushed = 0;
	double fraction = 0;
	bool priming = true;
	// The queue the renderer steers to (drift correction, catch-up). 0 leaves the stream uncontrolled.
	int Control() const {
		const int configured = target.load(std::memory_order_relaxed);
		if (configured >= 0) return configured;
		// Both sides move whole blocks: hold the producer's and the renderer's largest, plus room for arrival jitter.
		const int blocks = largestPush.load(std::memory_order_relaxed) + largestPull.load(std::memory_order_relaxed);
		return std::min(blocks + blocks / 4, StreamSize / 2);
	}
	// What to queue before playing after start, flush or underflow.
	int Prime() const {
		if (target.load(std::memory_order_relaxed) == -2 && underflows.load(std::memory_order_relaxed) == 0) return 0; // no wait while the producer keeps up
		return Control();
	}
};
struct Context {
	std::vector<std::unique_ptr<Clip>> clips; // Main thread only; the queue publishes stable Clip pointers.
	std::array<const Clip*, 4096> previewClips{}; // Entries are immutable and published by previewRequest.
	std::size_t clipSamples = 0;
	std::array<Slot, VoiceCount> slots{};
	std::array<Voice, VoiceCount> voices{};
	std::array<Command, QueueSize> commands{};
	std::atomic<std::uint64_t> write{0}, read{0};
	std::uint64_t next = 0;
	std::array<std::atomic<float>, BusCount> busGain{};
	std::array<float, BusCount * BlockSize * 2> buses{};
	std::array<std::atomic<float>, 12> metrics{};
	std::array<StreamInput, StreamCount> streams{};
	vpe::AudioProcessing* processing = nullptr; // Render thread only.
	vpe::AudioProcessing* controlProcessing = nullptr; // Main thread only; latest prepared graph.
	std::atomic<vpe::AudioProcessing*> pendingProcessing{nullptr};
	std::array<vpe::AudioProcessing*, 8> retired{};
	std::atomic<unsigned> retiredWrite{0}, retiredRead{0};
	std::atomic<int> previewRequest{-1};
	int previewBus = -1, previewFrames = 0, previewRate = 0;
	const Clip* previewClip = nullptr;
	unsigned noise = 1234567;
	std::array<float, vpe::ProcessingValues> processingSettings{};
	bool hasProcessingSettings = false;
	// Single control writer publishes a target + milliseconds together; only the render thread owns the envelope.
	std::atomic<std::uint64_t> transitionRequest{0x3f800000u};
	std::uint64_t transitionSeen = 0x3f800000u;
	float transitionTarget = 1;
	double transitionGain = 1, transitionStep = 0;
	int transitionRemaining = 0, transitionRate = 0;
	void Collect() {
		auto r = retiredRead.load(); const auto w = retiredWrite.load(std::memory_order_acquire);
		while (r != w) { delete retired[r % retired.size()]; ++r; }
		retiredRead.store(r, std::memory_order_release);
	}
	~Context() { Collect(); delete processing; delete pendingProcessing.load(); }
	Context() { for (auto& gain : busGain) gain.store(1); for (auto& metric : metrics) metric.store(0); streams[0].open.store(1); }
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
int VpePcmDspAbi() { return 1; }
int VpePcmPrepareDsp(void* context, int rate) {
	if (!context || rate < 8000 || rate > 192000) return 0;
	auto& c = *C(context); c.Collect();
	if (c.pendingProcessing.load(std::memory_order_acquire)) return 0;
	try {
		auto next = std::make_unique<vpe::AudioProcessing>(rate);
		if (c.hasProcessingSettings) next->Set(c.processingSettings.data(), vpe::ProcessingValues);
		c.controlProcessing = next.get();
		c.pendingProcessing.store(next.release(), std::memory_order_release); return 1;
	} catch (...) { return 0; }
}
int VpePcmSetProcessing(void* context, const float* values, int count) {
	if (!context || !values || count != vpe::ProcessingValues || !C(context)->controlProcessing) return 0;
	auto& c = *C(context);
	if (!c.controlProcessing->Set(values, count)) return 0;
	std::copy_n(values, count, c.processingSettings.data()); c.hasProcessingSettings = true; return 1;
}
int VpePcmProcessingMeters(void* context, float* values, int count) {
	if (!context || !values || count != vpe::ProcessingMeters || !C(context)->controlProcessing) return 0;
	C(context)->controlProcessing->Meters(values); return 1;
}
void VpePcmPreview(void* context, int bus, int enabled) {
	if (context && bus >= 0 && bus < BusCount) C(context)->previewRequest.store(enabled ? bus + 1 : 0, std::memory_order_release);
}
int VpePcmPreviewClip(void* context, int clip, int bus) {
	if (!context || clip < 0 || clip >= static_cast<int>(C(context)->clips.size()) || bus < 0 || bus >= BusCount) return 0;
	// One atomic message publishes the immutable clip and destination together, including when replacing a preview.
	C(context)->previewRequest.store((clip + 1) * 8 + bus, std::memory_order_release);
	return 1;
}
float VpePcmEqResponse(void* context, int route, float hz) { return context && C(context)->controlProcessing ? C(context)->controlProcessing->EqResponse(route, hz) : 0.f; }
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
		owner.clips.push_back(std::move(clip)); owner.clipSamples += count;
		const int index = static_cast<int>(owner.clips.size() - 1);
		owner.previewClips[index] = owner.clips[index].get();
		return index;
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
namespace {
StreamInput* Stream(void* context, int id) { return context && id >= 0 && id < StreamCount && C(context)->streams[id].open.load(std::memory_order_acquire) ? &C(context)->streams[id] : nullptr; }
void Bump(std::atomic<float>& metric) { metric.store(metric.load(std::memory_order_relaxed) + 1, std::memory_order_relaxed); }
}
void VpePcmSetStreamGain(void* context, float gain) { VpePcmStreamSetGain(context, 0, gain); }
void VpePcmFlushStream(void* context) { VpePcmStreamFlush(context, 0); }
void VpePcmEnableStream(void* context, int enabled) { VpePcmStreamEnable(context, 0, enabled); }
int VpePcmStreamPush(void* context, const void* samples, int frames, int channels, int rate, int encoding, std::int64_t sequence) {
	return VpePcmStreamPushTo(context, 0, samples, frames, channels, rate, encoding, sequence);
}
int VpePcmStreamOpen(void* context, int bus) {
	if (!context || bus < 0 || bus >= BusCount) return -1;
	auto& c = *C(context);
	for (int id = 1; id < StreamCount; ++id) {
		auto& s = c.streams[id];
		if (s.open.load(std::memory_order_acquire) || s.seenCloses.load(std::memory_order_acquire) != s.closes.load(std::memory_order_relaxed)) continue; // the renderer may still be in it
		// The renderer skips closed slots, so its own fields can be reset here; opening publishes them.
		s.rate.store(0); s.gain.store(1); s.bus.store(bus); s.target.store(-1); s.largestPush.store(0); s.largestPull.store(0); s.enabled.store(1);
		s.underflows.store(0); s.overflows.store(0); s.discontinuities.store(0); s.drops.store(0); s.sequence = 0;
		s.read.store(s.write.load()); s.flushed = s.flush.load(); s.fraction = 0; s.priming = true; s.currentGain = -1;
		s.open.store(1, std::memory_order_release);
		return id;
	}
	return -1;
}
void VpePcmStreamClose(void* context, int stream) {
	if (auto* s = stream > 0 ? Stream(context, stream) : nullptr) {
		s->enabled.store(0, std::memory_order_release);
		s->closes.fetch_add(1, std::memory_order_release); // counted before the slot reads as closed
		s->open.store(0, std::memory_order_release);
	}
}
void VpePcmStreamSetGain(void* context, int stream, float gain) { if (auto* s = Stream(context, stream); s && std::isfinite(gain) && gain >= 0 && gain <= 1) s->gain.store(gain); }
void VpePcmStreamFlush(void* context, int stream) { if (auto* s = Stream(context, stream)) s->flush.fetch_add(1, std::memory_order_release); }
void VpePcmStreamEnable(void* context, int stream, int enabled) { if (auto* s = Stream(context, stream)) s->enabled.store(enabled != 0 ? 1 : 0, std::memory_order_release); }
int VpePcmStreamSetBuffer(void* context, int stream, int frames) {
	auto* s = Stream(context, stream);
	if (!s || frames < -2 || frames > StreamSize / 2) return 0;
	s->target.store(frames); return 1;
}
int VpePcmStreamPushTo(void* context, int stream, const void* samples, int frames, int channels, int rate, int encoding, std::int64_t sequence) {
	auto* input = Stream(context, stream);
	if (!input || !samples || frames <= 0 || frames > StreamSize || (channels != 1 && channels != 2) || rate < 8000 || rate > 192000 || (encoding != 0 && encoding != 1)) return 0;
	auto& c = *C(context); auto& s = *input;
	const auto previousRate = s.rate.load();
	if (previousRate && previousRate != rate) return 0; // A new format requires a new session.
	s.rate.store(rate);
	{
		// The largest recent block: a burst raises it at once, and it decays back over the next blocks.
		int largest = s.largestPush.load(std::memory_order_relaxed);
		largest = frames >= largest ? frames : largest - std::max(1, (largest - frames) / 64);
		s.largestPush.store(std::min(largest, StreamSize / 2), std::memory_order_relaxed);
	}
	if (s.sequence && sequence != s.sequence + 1) { s.flush.fetch_add(1); Bump(s.discontinuities); if (stream == 0) Bump(c.metrics[9]); }
	s.sequence = sequence;
	const auto w = s.write.load(std::memory_order_relaxed), r = s.read.load(std::memory_order_acquire);
	if (w - r + frames > StreamSize) {
		// Unbuffered streams start over; buffered ones drop this block and the renderer's catch-up trims back to the newest audio.
		if (s.Control() == 0) s.flush.fetch_add(1);
		Bump(s.overflows); if (stream == 0) Bump(c.metrics[8]); return 0;
	}
	const auto* bytes = static_cast<const unsigned char*>(samples);
	for (int f = 0; f < frames; ++f) {
		auto sample = [&](int channel) {
			const auto offset = static_cast<size_t>(f * channels + channel) * (encoding == 1 ? 4 : 2);
			if (encoding == 1) { float value; std::memcpy(&value, bytes + offset, 4); return Finite(value); }
			const auto value = static_cast<std::int16_t>(static_cast<unsigned int>(bytes[offset]) | (static_cast<unsigned int>(bytes[offset+1]) << 8));
			return value / 32768.f;
		};
		const auto left = sample(0); s.ring[(w + f) % StreamSize] = {left, channels == 1 ? left : sample(1)};
	}
	s.write.store(w + frames, std::memory_order_release); return frames;
}
int VpePcmStreamMetrics(void* context, int stream, float* values, int count) {
	auto* s = Stream(context, stream);
	if (!s || !values || count != 6) return 0;
	values[0] = static_cast<float>(s->write.load() - s->read.load()); values[1] = static_cast<float>(s->Control());
	values[2] = s->underflows.load(); values[3] = s->overflows.load(); values[4] = s->discontinuities.load(); values[5] = s->drops.load();
	return 1;
}
int VpePcmSetTransitionGain(void* context, float gain, int milliseconds) {
	if (!context || !std::isfinite(gain) || gain < 0 || gain > 1 || milliseconds < 0 || milliseconds > 1000) return 0;
	std::uint32_t bits; std::memcpy(&bits, &gain, sizeof(bits));
	C(context)->transitionRequest.store((static_cast<std::uint64_t>(milliseconds) << 32) | bits, std::memory_order_release);
	return 1;
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
	if (auto* next = c.pendingProcessing.exchange(nullptr, std::memory_order_acquire)) {
		if (c.processing) {
			const auto w = c.retiredWrite.load(std::memory_order_relaxed);
			c.retired[w % c.retired.size()] = c.processing; c.retiredWrite.store(w + 1, std::memory_order_release);
		}
		c.processing = next;
	}
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
	for (int id = 0; id < StreamCount; ++id) {
		auto& s = c.streams[id];
		if (!s.open.load(std::memory_order_acquire)) { s.seenCloses.store(s.closes.load(std::memory_order_acquire), std::memory_order_release); continue; }
		const auto flush = s.flush.load(std::memory_order_acquire);
		const bool enabled = s.enabled.load(std::memory_order_acquire) != 0;
		if (paused || !enabled || flush != s.flushed) {
			s.read.store(s.write.load(std::memory_order_acquire), std::memory_order_release);
			s.fraction = 0; s.flushed = flush; s.priming = true;
		}
		if (paused || !enabled) { s.currentGain = -1; continue; }
		auto read = s.read.load(std::memory_order_relaxed); const auto written = s.write.load(std::memory_order_acquire);
		const auto inputRate = s.rate.load();
		if (!inputRate) continue;
		const int pull = static_cast<int>(std::ceil(frames * static_cast<double>(inputRate) / rate));
		if (pull > s.largestPull.load(std::memory_order_relaxed)) s.largestPull.store(std::min(pull, StreamSize / 2), std::memory_order_relaxed);
		const auto target = static_cast<std::uint64_t>(s.Control());
		if (s.priming) {
			if (written - read < std::max<std::uint64_t>(2, static_cast<std::uint64_t>(s.Prime()))) continue; // still filling the jitter buffer
			s.priming = false;
		} else if (target && written - read > target * 2 + 256) {
			// The producer ran far ahead (a burst after a stall, or a stalled output): skip back to the target instead of
			// staying late; drift correction alone would take many seconds to drain it.
			read = written - target; s.fraction = 0; Bump(s.drops);
		}
		const int bus = std::clamp(s.bus.load(std::memory_order_relaxed), 0, BusCount - 1);
		const float targetGain = s.gain.load() * gains[bus];
		if (s.currentGain < 0 || targetGain == 0) s.currentGain = targetGain;
		const int ramp = std::max(1, rate/200); const float gainStep = (targetGain-s.currentGain)/ramp;
		double step = static_cast<double>(inputRate) / rate;
		if (target) {
			// The producer's clock and the device's drift apart. Keep the queue a little above the buffer by playing up to
			// 0.3% faster or slower (under 5 cents), instead of letting it run dry or jump back.
			const double setpoint = target * 1.25;
			const double error = (static_cast<double>(written - read) - setpoint) / setpoint;
			step *= 1 + std::clamp(error * .004, -.003, .003);
		}
		float* out = c.buses.data() + bus*BlockSize*2;
		bool underflow = false;
		for (int f = 0; f < frames; ++f) {
			if (read + 1 >= written) { underflow = true; s.fraction = 0; break; }
			const auto a = s.ring[read % StreamSize], b = s.ring[(read+1) % StreamSize];
			s.currentGain = f < ramp ? s.currentGain + gainStep : targetGain;
			const float l = (a.left + (b.left-a.left)*static_cast<float>(s.fraction)) * s.currentGain;
			const float rr = (a.right + (b.right-a.right)*static_cast<float>(s.fraction)) * s.currentGain;
			out[f*2] += l; out[f*2+1] += rr;
			s.fraction += step; const auto advance = static_cast<std::uint64_t>(s.fraction);
			read = std::min(read + advance, written); s.fraction -= advance;
		}
		s.read.store(read, std::memory_order_release);
		if (underflow) { Bump(s.underflows); if (id == 0) Bump(c.metrics[7]); s.priming = s.Prime() > 0; }
	}
	const auto preview = c.previewRequest.exchange(-1, std::memory_order_acquire);
	if (preview >= 0) {
		c.previewClip = preview >= 8 ? c.previewClips[preview / 8 - 1] : nullptr;
		c.previewBus = preview >= 8 ? preview % 8 : preview - 1;
		c.previewFrames = preview ? rate * 3 : 0;
		c.previewRate = rate;
	}
	if (c.previewRate != rate) { c.previewFrames = 0; c.previewClip = nullptr; }
	const bool previewActive = c.previewFrames > 0;
	if (previewActive) for (int f = 0; f < frames && c.previewFrames > 0; ++f, --c.previewFrames) {
		if (c.previewClip) {
			// Three real flipper strokes with gaps to hear the room tail. Game voices remain paused.
			const int elapsed = rate * 3 - c.previewFrames;
			const int interval = std::max(1, rate * 4 / 5);
			const double cursor = double(elapsed % interval) * c.previewClip->rate / rate;
			if (elapsed / interval < 3 && cursor < c.previewClip->frames) {
				const auto sample = Sample(*c.previewClip, cursor, false);
				c.buses[c.previewBus*BlockSize*2 + f*2] += sample.left * .53f;
				c.buses[c.previewBus*BlockSize*2 + f*2+1] += sample.right * .53f;
			}
			continue;
		}
		// Quiet, bounded stereo pulses audition processing without unpausing any game voice.
		c.noise = c.noise * 1664525u + 1013904223u;
		const float phase = float(c.previewFrames % (rate/2)) / (rate/2);
		const float sample = (static_cast<float>(c.noise >> 8) / 8388608.f - 1) * .0630957f * std::max(0.f, 1 - phase*4);
		const int channel = (c.previewFrames / (rate/2)) % 2;
		c.buses[c.previewBus*BlockSize*2 + f*2 + channel] += sample;
	}
	for (int bus = 0; bus < BusCount; ++bus) {
		const float* samples = c.buses.data() + bus*BlockSize*2;
		for (int i = 0; i < frames*2; ++i) { output[i] += samples[i]; peaks[bus] = std::max(peaks[bus], std::abs(samples[i])); }
		c.metrics[bus].store(peaks[bus], std::memory_order_relaxed);
	}
	c.metrics[11].store(static_cast<float>(active));
	if (c.processing) c.processing->Process(c.buses.data(), BlockSize*2, output, frames, paused != 0, previewActive);
	const auto transition = c.transitionRequest.load(std::memory_order_acquire);
	if (transition != c.transitionSeen) {
		const auto bits = static_cast<std::uint32_t>(transition);
		std::memcpy(&c.transitionTarget, &bits, sizeof(bits));
		c.transitionRemaining = static_cast<int>((transition >> 32) * rate / 1000);
		c.transitionSeen = transition;
	} else if (c.transitionRate && c.transitionRate != rate) {
		c.transitionRemaining = static_cast<int>(std::llround(static_cast<double>(c.transitionRemaining) * rate / c.transitionRate));
	}
	c.transitionRate = rate;
	c.transitionStep = c.transitionRemaining ? (c.transitionTarget - c.transitionGain) / c.transitionRemaining : 0;
	if (!c.transitionRemaining) c.transitionGain = c.transitionTarget;
	for (int f = 0; f < frames; ++f) {
		if (c.transitionRemaining && !--c.transitionRemaining) c.transitionGain = c.transitionTarget;
		else if (c.transitionRemaining) c.transitionGain += c.transitionStep;
		// Settings previews are independent of the paused game's temporary master envelope.
		const float gain = paused && previewActive ? 1.f : static_cast<float>(c.transitionGain);
		output[f*2] = Finite(output[f*2]) * gain;
		output[f*2+1] = Finite(output[f*2+1]) * gain;
	}
	return 1;
}
int VpePcmMetrics(void* context, float* values, int count) {
	if (!context || !values || count != 12) return 0;
	for (int i = 0; i < count; ++i) values[i] = C(context)->metrics[i].load(std::memory_order_relaxed);
	return 1;
}
