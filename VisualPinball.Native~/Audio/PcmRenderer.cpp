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
constexpr int ZoneCount = 5, TapCount = 4, TapSize = 8192, MaxChannels = 16;
// The mechanical buses (0 and 1) have a second buffer each for the pair near the backbox, after the seven buses.
constexpr int SurroundBuses = 2, BufferCount = BusCount + SurroundBuses;
static_assert(std::atomic<float>::is_always_lock_free && std::atomic<std::uint64_t>::is_always_lock_free, "PCM control atomics must be lock-free");
float Finite(float value) { return std::isfinite(value) ? value : 0.f; }
bool Depth(float depth) { return std::isfinite(depth) && depth >= 0 && depth <= 1; }
bool Controls(float gain, float pitch, float pan) { return std::isfinite(gain) && gain >= 0 && gain <= 4 && std::isfinite(pitch) && pitch >= .01f && pitch <= 3 && std::isfinite(pan) && pan >= -1 && pan <= 1; }
struct Clip { std::vector<float> samples; int frames, channels, rate; };
struct Command { std::uint64_t id = 0; Clip* clip = nullptr; int bus = 0; float gain = 1, pitch = 1, pan = 0, depth = .5f; bool start = false, loop = false, paused = false; };
struct Slot { std::atomic<std::uint64_t> generation{0}, stop{0}; std::atomic<int> state{0}; std::atomic<float> seconds{0}; };
struct Voice { Command controls{}; double cursor = 0; float left = 0, right = 0, backLeft = 0, backRight = 0; bool active = false, fresh = true; };
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
// Where the buses play: each in one zone, each zone on one stereo pair of the rendering output or of a tap's device.
// Published as one word, so the renderer never sees half of a change.
struct Routing {
	std::array<int, BusCount> busZone{};
	std::array<int, ZoneCount> zonePair{}, zoneTap{}; // tap 0 is the rendering output, 1..4 a tap
	std::array<bool, ZoneCount> reserved{}; // the zone's channels stay open on its output while no bus plays in it
	// A mechanical bus on two pairs: its own zone near the player, backZone near the backbox.
	std::array<bool, SurroundBuses> surround{};
	std::array<int, SurroundBuses> backZone{};
	// 7 x 3 bits of zones, 5 x 3 of pairs, 5 x 3 of taps, 5 reserved flags, then the surround settings: 64 in all.
	static constexpr int PairShift = 21, TapShift = 36, ReservedShift = 51, SurroundShift = 56, BackShift = 58;
	static constexpr std::uint64_t SurroundBits = ~std::uint64_t(0) << SurroundShift;
	std::uint64_t Pack() const {
		std::uint64_t bits = 0;
		for (int bus = 0; bus < BusCount; ++bus) bits |= static_cast<std::uint64_t>(busZone[bus]) << (bus * 3);
		for (int zone = 0; zone < ZoneCount; ++zone) {
			bits |= static_cast<std::uint64_t>(zonePair[zone]) << (PairShift + zone * 3) | static_cast<std::uint64_t>(zoneTap[zone]) << (TapShift + zone * 3);
			if (reserved[zone]) bits |= std::uint64_t(1) << (ReservedShift + zone);
		}
		for (int bus = 0; bus < SurroundBuses; ++bus) {
			if (surround[bus]) bits |= std::uint64_t(1) << (SurroundShift + bus);
			bits |= static_cast<std::uint64_t>(backZone[bus]) << (BackShift + bus * 3);
		}
		return bits;
	}
	static Routing Unpack(std::uint64_t bits) {
		Routing routing;
		for (int bus = 0; bus < BusCount; ++bus) routing.busZone[bus] = std::min(static_cast<int>(bits >> (bus * 3) & 7), ZoneCount - 1);
		for (int zone = 0; zone < ZoneCount; ++zone) {
			routing.zonePair[zone] = static_cast<int>(bits >> (PairShift + zone * 3) & 7);
			routing.zoneTap[zone] = std::min(static_cast<int>(bits >> (TapShift + zone * 3) & 7), TapCount);
			routing.reserved[zone] = (bits >> (ReservedShift + zone) & 1) != 0;
		}
		for (int bus = 0; bus < SurroundBuses; ++bus) {
			routing.surround[bus] = (bits >> (SurroundShift + bus) & 1) != 0;
			routing.backZone[bus] = std::min(static_cast<int>(bits >> (BackShift + bus * 3) & 7), ZoneCount - 1);
		}
		return routing;
	}
	// Whether a mechanical bus is split over two pairs; not when both would be the same zone.
	bool Split(int bus) const { return bus < SurroundBuses && surround[bus] && busZone[bus] != backZone[bus]; }
	// Bit per buffer that plays in the zone: the seven buses, then the back halves of the split ones.
	unsigned Buffers(int zone) const {
		unsigned mask = 0;
		for (int bus = 0; bus < BusCount; ++bus) if (busZone[bus] == zone) mask |= 1u << bus;
		for (int bus = 0; bus < SurroundBuses; ++bus) if (Split(bus) && backZone[bus] == zone) mask |= 1u << (BusCount + bus);
		return mask;
	}
};
// Zones on their way to another device: the renderer writes, that device's thread reads on its own clock. A frame
// holds every zone's stereo pair, silent for the zones of other outputs.
struct Tap {
	static constexpr int FrameSize = ZoneCount * 2;
	std::array<float, TapSize * FrameSize> ring{};
	std::atomic<std::uint64_t> write{0}, read{0};
	std::atomic<int> attached{0};
	std::atomic<int> largestPush{0}, largestPull{0}; // biggest recent block in and out
	std::atomic<float> underflows{0}, overflows{0}, drops{0};
	// reader only
	double fraction = 0;
	bool priming = true;
	// The queue the reader steers to: both sides move whole blocks, plus room for wake-up jitter.
	int Control() const {
		const int blocks = largestPush.load(std::memory_order_relaxed) + largestPull.load(std::memory_order_relaxed);
		return std::min(blocks + blocks / 4, TapSize / 2);
	}
};
// One processing chain per zone, all with the same settings, so every output ends in its own limiter.
struct Dsp {
	std::array<std::unique_ptr<vpe::AudioProcessing>, ZoneCount> zones;
	explicit Dsp(int rate) { for (auto& zone : zones) zone = std::make_unique<vpe::AudioProcessing>(rate); }
	bool Set(const float* values, int count) { bool accepted = true; for (auto& zone : zones) accepted = zone->Set(values, count) && accepted; return accepted; }
	void Meters(float* values) const {
		std::array<float, vpe::ProcessingMeters> zone{};
		std::fill_n(values, vpe::ProcessingMeters, 0.f);
		for (const auto& processing : zones) { processing->Meters(zone.data()); for (int i = 0; i < vpe::ProcessingMeters; ++i) values[i] = std::max(values[i], zone[i]); }
	}
	float EqResponse(int route, float hz) const { return zones[0]->EqResponse(route, hz); }
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
	std::array<float, BufferCount * BlockSize * 2> buses{};
	std::array<std::atomic<float>, 12> metrics{};
	std::array<StreamInput, StreamCount> streams{};
	std::atomic<std::uint64_t> routing{0}; // a packed Routing; zero plays every bus on the rendering output's first pair
	std::array<float, ZoneCount * BlockSize * 2> zones{}; // Render thread only: the block of every zone that has a bus.
	std::array<unsigned, ZoneCount> zoneBuses{}; // Render thread only: the buffers of each zone in that block.
	std::array<Tap, TapCount> taps{};
	Dsp* processing = nullptr; // Render thread only.
	Dsp* controlProcessing = nullptr; // Main thread only; latest prepared graph.
	std::atomic<Dsp*> pendingProcessing{nullptr};
	std::array<Dsp*, 8> retired{};
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
	// The user's output level: the control writer publishes the target, the render thread ramps to it. The upper half
	// counts the levels to apply at once, so a renderer always sees a level together with whether to jump to it.
	std::atomic<std::uint64_t> masterRequest{0x3f800000u};
	std::uint32_t masterJumps = 0; // control thread only
	std::uint32_t masterJumpsSeen = 0; // render thread only
	float masterGain = 1;
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
		auto next = std::make_unique<Dsp>(rate);
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
	if (context && bus >= 0 && bus < BufferCount) C(context)->previewRequest.store(enabled ? bus + 1 : 0, std::memory_order_release);
}
int VpePcmPreviewClip(void* context, int clip, int bus) {
	if (!context || clip < 0 || clip >= static_cast<int>(C(context)->clips.size()) || bus < 0 || bus >= BusCount) return 0;
	// One atomic message publishes the immutable clip and destination together, including when replacing a preview.
	C(context)->previewRequest.store((clip + 1) * 16 + bus, std::memory_order_release);
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
std::uint64_t VpePcmPlay(void* context, int clip, int bus, float gain, float pitch, float pan, int loop) { return VpePcmPlayAt(context, clip, bus, gain, pitch, pan, .5f, loop); }
int VpePcmUpdate(void* context, std::uint64_t id, float gain, float pitch, float pan, int paused) { return VpePcmUpdateAt(context, id, gain, pitch, pan, .5f, paused); }
std::uint64_t VpePcmPlayAt(void* context, int clip, int bus, float gain, float pitch, float pan, float depth, int loop) {
	if (!context || clip < 0 || clip >= static_cast<int>(C(context)->clips.size()) || bus < 0 || bus >= BusCount || !Controls(gain, pitch, pan) || !Depth(depth)) return 0;
	auto& c = *C(context);
	for (int i = 0; i < VoiceCount; ++i) {
		const int state = c.slots[i].state.load(std::memory_order_acquire);
		if (state != 0 && state != 3) continue;
		const auto id = (++c.next) * VoiceCount + i;
		c.slots[i].seconds.store(0); c.slots[i].generation.store(id, std::memory_order_release); c.slots[i].stop.store(0); c.slots[i].state.store(1);
		Command command; command.id = id; command.clip = c.clips[clip].get(); command.bus = bus; command.gain = gain; command.pitch = pitch; command.pan = pan; command.depth = depth; command.loop = loop != 0; command.start = true;
		if (c.Push(command)) return id;
		c.slots[i].state.store(3); return 0;
	}
	c.metrics[10].store(c.metrics[10].load() + 1); return 0;
}
int VpePcmUpdateAt(void* context, std::uint64_t id, float gain, float pitch, float pan, float depth, int paused) {
	if (!context || !Current(C(context), id) || !Controls(gain, pitch, pan) || !Depth(depth)) return 0;
	Command command; command.id = id; command.gain = gain; command.pitch = pitch; command.pan = pan; command.depth = depth; command.paused = paused != 0;
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
int VpePcmSetMasterGain(void* context, float gain, int immediate) {
	if (!context || !std::isfinite(gain) || gain < 0 || gain > 1) return 0;
	auto& c = *C(context);
	// the first level a session is given is where it starts, not a change to fade to: a saved mute stays silent
	if (immediate || c.masterJumps == 0) ++c.masterJumps;
	std::uint32_t bits; std::memcpy(&bits, &gain, sizeof(bits));
	c.masterRequest.store((static_cast<std::uint64_t>(c.masterJumps) << 32) | bits, std::memory_order_release);
	return 1;
}
namespace {
// Renders one block of every zone into the context's zone buffers and feeds the taps.
Routing RenderBlock(Context& c, int frames, int rate, int paused) {
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
		else if (voice.active && voice.controls.id == command.id) { voice.controls.gain = command.gain; voice.controls.pitch = command.pitch; voice.controls.pan = command.pan; voice.controls.depth = command.depth; voice.controls.paused = command.paused; }
	}
	c.read.store(r, std::memory_order_release);
	std::array<float, BusCount> gains{}, peaks{};
	for (int bus = 0; bus < BusCount; ++bus) gains[bus] = c.busGain[bus].load(std::memory_order_relaxed);
	for (int buffer = 0; buffer < BufferCount; ++buffer) std::fill_n(c.buses.data() + buffer*BlockSize*2, frames*2, 0.f);
	const auto routing = Routing::Unpack(c.routing.load(std::memory_order_acquire));
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
		// On four speakers a mechanical voice fades from the pair near the player to the one near the backbox, with constant power.
		const bool split = routing.Split(control.bus);
		const float near = split ? std::sqrt(1 - control.depth) : 1.f, far = split ? std::sqrt(control.depth) : 0.f;
		const float targetLeft = left * gain * near, targetRight = right * gain * near, targetBackLeft = left * gain * far, targetBackRight = right * gain * far;
		if (voice.fresh) { voice.left = targetLeft; voice.right = targetRight; voice.backLeft = targetBackLeft; voice.backRight = targetBackRight; voice.fresh = false; }
		const int ramp = std::max(1, rate/200);
		const float leftStep = (targetLeft-voice.left)/ramp, rightStep = (targetRight-voice.right)/ramp;
		const float backLeftStep = (targetBackLeft-voice.backLeft)/ramp, backRightStep = (targetBackRight-voice.backRight)/ramp;
		const bool back = split || voice.backLeft != 0 || voice.backRight != 0; // also while fading out of the back pair
		float* bus = c.buses.data() + control.bus*BlockSize*2;
		float* backBus = c.buses.data() + (BusCount + std::min(control.bus, SurroundBuses - 1))*BlockSize*2;
		for (int f = 0; f < frames; ++f) {
			if (voice.cursor >= clip.frames) { if (control.loop) voice.cursor = std::fmod(voice.cursor, clip.frames); else { End(c, voice); break; } }
			const auto sample = Sample(clip, voice.cursor, control.loop);
			if (f < ramp) { voice.left += leftStep; voice.right += rightStep; voice.backLeft += backLeftStep; voice.backRight += backRightStep; }
			else { voice.left = targetLeft; voice.right = targetRight; voice.backLeft = targetBackLeft; voice.backRight = targetBackRight; }
			bus[f*2] += sample.left * voice.left; bus[f*2+1] += sample.right * voice.right;
			if (back) { backBus[f*2] += sample.left * voice.backLeft; backBus[f*2+1] += sample.right * voice.backRight; }
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
		c.previewClip = preview >= 16 ? c.previewClips[preview / 16 - 1] : nullptr;
		c.previewBus = preview >= 16 ? preview % 16 : preview - 1;
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
	for (int buffer = 0; buffer < BufferCount; ++buffer) {
		// a split bus shows the louder of its two pairs
		const int bus = buffer < BusCount ? buffer : buffer - BusCount;
		const float* samples = c.buses.data() + buffer*BlockSize*2;
		for (int i = 0; i < frames*2; ++i) peaks[bus] = std::max(peaks[bus], std::abs(samples[i]));
	}
	for (int bus = 0; bus < BusCount; ++bus) c.metrics[bus].store(peaks[bus], std::memory_order_relaxed);
	c.metrics[11].store(static_cast<float>(active));
	for (int zone = 0; zone < ZoneCount; ++zone) {
		const unsigned buffers = c.zoneBuses[zone] = routing.Buffers(zone);
		if (!buffers) { if (c.processing) c.processing->zones[zone]->Idle(); continue; }
		// The zone's chain sees the back half of a split bus as that bus: same settings, its own state.
		std::array<const float*, BusCount> sources{};
		for (int bus = 0; bus < BusCount; ++bus) {
			if (buffers >> bus & 1) sources[bus] = c.buses.data() + bus*BlockSize*2;
			else if (bus < SurroundBuses && buffers >> (BusCount + bus) & 1) sources[bus] = c.buses.data() + (BusCount + bus)*BlockSize*2;
		}
		float* out = c.zones.data() + zone*BlockSize*2;
		if (c.processing) { c.processing->zones[zone]->Process(sources.data(), out, frames, paused != 0, previewActive); continue; }
		std::fill_n(out, frames*2, 0.f);
		for (const float* samples : sources) {
			if (!samples) continue;
			for (int i = 0; i < frames*2; ++i) out[i] += samples[i];
		}
	}
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
	const auto master = c.masterRequest.load(std::memory_order_acquire);
	const auto masterBits = static_cast<std::uint32_t>(master);
	float masterTarget; std::memcpy(&masterTarget, &masterBits, sizeof(masterTarget));
	if (static_cast<std::uint32_t>(master >> 32) != c.masterJumpsSeen) { c.masterJumpsSeen = static_cast<std::uint32_t>(master >> 32); c.masterGain = masterTarget; }
	const float masterStep = frames ? (masterTarget - c.masterGain) / frames : 0;
	for (int f = 0; f < frames; ++f) {
		if (c.transitionRemaining && !--c.transitionRemaining) c.transitionGain = c.transitionTarget;
		else if (c.transitionRemaining) c.transitionGain += c.transitionStep;
		// Settings previews are independent of the paused game's temporary master envelope.
		c.masterGain += masterStep;
		const float gain = (paused && previewActive ? 1.f : static_cast<float>(c.transitionGain)) * c.masterGain;
		for (int zone = 0; zone < ZoneCount; ++zone) {
			if (!c.zoneBuses[zone]) continue;
			float* out = c.zones.data() + zone*BlockSize*2 + f*2;
			out[0] = Finite(out[0]) * gain; out[1] = Finite(out[1]) * gain;
		}
	}
	if (frames) c.masterGain = masterTarget;
	for (int id = 0; id < TapCount && frames; ++id) {
		auto& tap = c.taps[id];
		unsigned zones = 0;
		for (int zone = 0; zone < ZoneCount; ++zone) if (routing.zoneTap[zone] == id + 1 && c.zoneBuses[zone]) zones |= 1u << zone;
		if (!zones || !tap.attached.load(std::memory_order_acquire)) continue;
		// The largest recent block: a larger one raises it at once, and it decays back over the next blocks.
		int largest = tap.largestPush.load(std::memory_order_relaxed);
		largest = frames >= largest ? frames : largest - std::max(1, (largest - frames) / 64);
		tap.largestPush.store(std::min(largest, TapSize / 2), std::memory_order_relaxed);
		const auto head = tap.write.load(std::memory_order_relaxed);
		// A reader that stopped pulling: drop the block, and its catch-up trims back to the newest audio.
		if (head - tap.read.load(std::memory_order_acquire) + frames > TapSize) { Bump(tap.overflows); continue; }
		for (int f = 0; f < frames; ++f) {
			float* frame = tap.ring.data() + (head + f) % TapSize * Tap::FrameSize;
			for (int zone = 0; zone < ZoneCount; ++zone) {
				const float* in = c.zones.data() + zone*BlockSize*2 + f*2;
				const bool carried = (zones >> zone & 1) != 0;
				frame[zone*2] = carried ? in[0] : 0.f; frame[zone*2+1] = carried ? in[1] : 0.f;
			}
		}
		tap.write.store(head + frames, std::memory_order_release);
	}
	return routing;
}
// The first channel of a zone's pair, or of the first pair when the output doesn't have that one.
int First(const Routing& routing, int zone, int channels) { const int first = routing.zonePair[zone] * 2; return first + 1 < channels ? first : 0; }
}
int VpePcmRender(void* context, float* output, int frames, int channels, int rate, int paused) {
	if (!output || frames < 0 || channels <= 0 || channels > MaxChannels) return 0;
	std::fill_n(output, static_cast<size_t>(frames) * channels, 0.f);
	if (!context || channels < 2 || rate < 8000 || rate > 192000) return 0;
	auto& c = *C(context);
	int offset = 0;
	do {
		const int count = std::min(BlockSize, frames - offset);
		const auto routing = RenderBlock(c, count, rate, paused);
		float* out = output + static_cast<size_t>(offset) * channels;
		for (int zone = 0; zone < ZoneCount; ++zone) {
			if (!c.zoneBuses[zone] || routing.zoneTap[zone]) continue; // a tapped zone plays on its own device
			const float* in = c.zones.data() + zone*BlockSize*2;
			const int first = First(routing, zone, channels);
			for (int f = 0; f < count; ++f) { out[f*channels + first] += in[f*2]; out[f*channels + first + 1] += in[f*2+1]; }
		}
		offset += count;
	} while (offset < frames);
	return 1;
}
int VpePcmRoutingAbi() { return 1; }
int VpePcmSetRouting(void* context, const int* busZone, const int* zonePair, const int* zoneTap, const int* zoneReserved) {
	if (!context || !busZone || !zonePair || !zoneTap) return 0;
	Routing routing;
	for (int bus = 0; bus < BusCount; ++bus) { if (busZone[bus] < 0 || busZone[bus] >= ZoneCount) return 0; routing.busZone[bus] = busZone[bus]; }
	for (int zone = 0; zone < ZoneCount; ++zone) {
		if (zonePair[zone] < 0 || zonePair[zone] >= MaxChannels / 2 || zoneTap[zone] < 0 || zoneTap[zone] > TapCount) return 0;
		routing.zonePair[zone] = zonePair[zone]; routing.zoneTap[zone] = zoneTap[zone];
		routing.reserved[zone] = zoneReserved && zoneReserved[zone] != 0;
	}
	auto& word = C(context)->routing;
	word.store((routing.Pack() & ~Routing::SurroundBits) | (word.load(std::memory_order_relaxed) & Routing::SurroundBits), std::memory_order_release);
	return 1;
}
int VpePcmSetSurround(void* context, int bus, int enabled, int backZone) {
	if (!context || bus < 0 || bus >= SurroundBuses || backZone < 0 || backZone >= ZoneCount) return 0;
	auto& word = C(context)->routing;
	auto routing = Routing::Unpack(word.load(std::memory_order_relaxed));
	routing.surround[bus] = enabled != 0; routing.backZone[bus] = backZone;
	word.store(routing.Pack(), std::memory_order_release);
	return 1;
}
int VpePcmOutputChannels(void* context, int tap) {
	if (!context || tap < -1 || tap >= TapCount) return 0;
	const auto routing = Routing::Unpack(C(context)->routing.load(std::memory_order_acquire));
	int channels = 2;
	for (int zone = 0; zone < ZoneCount; ++zone) if (routing.zoneTap[zone] == tap + 1 && (routing.Buffers(zone) || routing.reserved[zone])) channels = std::max(channels, routing.zonePair[zone] * 2 + 2);
	return channels;
}
int VpePcmTapAttach(void* context, int tap, int attached) {
	if (!context || tap < 0 || tap >= TapCount) return 0;
	auto& t = C(context)->taps[tap];
	if (attached) {
		// The reader starts at the newest audio and sizes its buffer anew.
		t.read.store(t.write.load(std::memory_order_acquire), std::memory_order_release);
		t.fraction = 0; t.priming = true; t.largestPush.store(0); t.largestPull.store(0);
	}
	t.attached.store(attached ? 1 : 0, std::memory_order_release);
	return 1;
}
int VpePcmTapRender(void* context, int tap, float* output, int frames, int channels) {
	if (!output || frames < 0 || channels <= 0 || channels > MaxChannels) return 0;
	std::fill_n(output, static_cast<size_t>(frames) * channels, 0.f);
	if (!context || tap < 0 || tap >= TapCount || channels < 2) return 0;
	auto& t = C(context)->taps[tap];
	const auto routing = Routing::Unpack(C(context)->routing.load(std::memory_order_acquire));
	{
		// As on the renderer's side: a device's first, larger fill must not size the buffer for good.
		int largest = t.largestPull.load(std::memory_order_relaxed);
		largest = frames >= largest ? frames : largest - std::max(1, (largest - frames) / 64);
		t.largestPull.store(std::min(largest, TapSize / 2), std::memory_order_relaxed);
	}
	auto read = t.read.load(std::memory_order_relaxed); const auto written = t.write.load(std::memory_order_acquire);
	const auto target = static_cast<std::uint64_t>(t.Control());
	if (t.priming) {
		if (written - read < std::max<std::uint64_t>(2, target)) return 1; // still filling the jitter buffer
		t.priming = false;
	} else if (target && written - read > target * 2 + 256) {
		// The renderer ran far ahead (this device stalled): skip to the newest audio instead of staying late.
		read = written - target; t.fraction = 0; Bump(t.drops);
	}
	// The two devices' clocks drift apart: hold the queue a little above the buffer by playing up to 0.3% faster or slower.
	double step = 1;
	if (target) {
		const double setpoint = target * 1.25;
		step += std::clamp((static_cast<double>(written - read) - setpoint) / setpoint * .004, -.003, .003);
	}
	std::array<int, ZoneCount> first{};
	for (int zone = 0; zone < ZoneCount; ++zone) first[zone] = routing.zoneTap[zone] == tap + 1 ? First(routing, zone, channels) : -1;
	bool underflow = false;
	for (int f = 0; f < frames; ++f) {
		if (read + 1 >= written) { underflow = true; t.fraction = 0; break; }
		const float* a = t.ring.data() + read % TapSize * Tap::FrameSize, *b = t.ring.data() + (read + 1) % TapSize * Tap::FrameSize;
		const float mix = static_cast<float>(t.fraction);
		for (int zone = 0; zone < ZoneCount; ++zone) {
			if (first[zone] < 0) continue;
			output[f*channels + first[zone]] += a[zone*2] + (b[zone*2] - a[zone*2]) * mix;
			output[f*channels + first[zone] + 1] += a[zone*2+1] + (b[zone*2+1] - a[zone*2+1]) * mix;
		}
		t.fraction += step; const auto advance = static_cast<std::uint64_t>(t.fraction);
		read = std::min(read + advance, written); t.fraction -= advance;
	}
	t.read.store(read, std::memory_order_release);
	if (underflow) { Bump(t.underflows); t.priming = true; }
	return 1;
}
int VpePcmTapMetrics(void* context, int tap, float* values, int count) {
	if (!context || tap < 0 || tap >= TapCount || !values || count != 5) return 0;
	const auto& t = C(context)->taps[tap];
	values[0] = static_cast<float>(t.write.load() - t.read.load()); values[1] = static_cast<float>(t.Control());
	values[2] = t.underflows.load(); values[3] = t.overflows.load(); values[4] = t.drops.load();
	return 1;
}
int VpePcmMetrics(void* context, float* values, int count) {
	if (!context || !values || count != 12) return 0;
	for (int i = 0; i < count; ++i) values[i] = C(context)->metrics[i].load(std::memory_order_relaxed);
	return 1;
}
