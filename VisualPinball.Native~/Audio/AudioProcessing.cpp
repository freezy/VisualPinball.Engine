// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
#include "AudioProcessing.h"
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <stdexcept>
#if defined(__SSE__) || defined(_M_X64) || defined(_M_IX86)
#include <xmmintrin.h>
#endif
#include "signalsmith-basics/reverb.h"
#include "signalsmith-basics/limiter.h"

namespace vpe {
namespace {
struct ScopedFloatState {
#if defined(__SSE__) || defined(_M_X64) || defined(_M_IX86)
	unsigned state = _mm_getcsr();
	ScopedFloatState() { _mm_setcsr(state | 0x8040); }
	~ScopedFloatState() { _mm_setcsr(state); }
#elif defined(__aarch64__)
	std::uint64_t state;
	ScopedFloatState() { asm volatile("mrs %0, fpcr" : "=r"(state)); auto next = state | (std::uint64_t(1) << 24); asm volatile("msr fpcr, %0" : : "r"(next)); }
	~ScopedFloatState() { asm volatile("msr fpcr, %0" : : "r"(state)); }
#endif
};
constexpr int Block = 4096, Bands = 8, Queue = 8, MaxDelay = 19201;
using Filter = signalsmith::filters::BiquadStatic<double>;
float Db(float value) { return std::pow(10.f, value / 20.f); }
float Clean(float value) { return std::isfinite(value) && std::abs(value) > 1e-25f ? value : 0.f; }
float Bound(float value, float low, float high) { return std::clamp(value, low, high); }
struct Route {
	std::array<Filter, Bands> eq{};
	bool compress = false;
	float threshold = -18, ratio = 2, knee = 6, attack = .002f, release = .0002f, makeup = 1, send = 0;
};
struct Snapshot {
	std::array<Route, ProcessingRoutes> routes{};
	bool enabled = false, bypass = false, mute = false, reverb = false, limiter = false;
	float master = 1, amount = .1f, room = 30, decay = .15f, predelay = 0, damping = 8000, width = 1, ceiling = .891251f, release = 80;
};
struct Eq {
	std::array<std::array<Filter, Bands>, 2> old{}, next{};
	std::array<Filter, Bands> pending{};
	bool dirty = false;
	int remaining = 0, length = 480;
	void StartFade() {
		old = next;
		for (int ch = 0; ch < 2; ++ch) for (int b = 0; b < Bands; ++b) next[ch][b].copyFrom(pending[b]);
		remaining = length; dirty = false;
	}
	void Change(const Route& route, int rate, bool initial) {
		pending = route.eq; length = std::max(1, rate / 100); dirty = true;
		if (initial) {
			for (int ch = 0; ch < 2; ++ch) for (int b = 0; b < Bands; ++b) next[ch][b].copyFrom(pending[b]);
			old = next; remaining = 0; dirty = false;
		} else if (!remaining) StartFade();
	}
	void Advance() { if (remaining) --remaining; if (!remaining && dirty) StartFade(); }
	float Sample(float value, int ch) {
		double a = value, b = value;
		for (auto& filter : next[ch]) b = filter(b);
		if (remaining) {
			for (auto& filter : old[ch]) a = filter(a);
			b = a + (b - a) * (1 - double(remaining) / length);
		}
		return Clean(static_cast<float>(b));
	}
	void Reset() { for (auto* bank : { &old, &next }) for (auto& channel : *bank) for (auto& filter : channel) filter.reset(); }
};
struct Compressor {
	float reduction = 0, mix = 0, makeup = 1;
	void Process(float& left, float& right, const Route& route, float slew) {
		const float level = 20 * std::log10(std::max(1e-20f, std::max(std::abs(left), std::abs(right))));
		const float over = level - route.threshold, slope = 1 - 1 / route.ratio;
		float target = slope * std::max(0.f, over);
		if (route.knee > 0 && over > -route.knee * .5f && over < route.knee * .5f) target = slope * (over + route.knee*.5f) * (over + route.knee*.5f) / (2*route.knee);
		reduction += (target - reduction) * (target > reduction ? route.attack : route.release);
		mix += ((route.compress ? 1.f : 0.f) - mix) * slew;
		makeup += (route.makeup - makeup) * slew;
		const float gain = 1 + (Db(-reduction)*makeup - 1)*mix;
		left *= gain; right *= gain;
	}
};
}

struct AudioProcessing::Impl {
	const int rate, latency;
	std::array<Snapshot, Queue> queue{};
	std::atomic<unsigned> write{0}, read{0};
	Snapshot current{};
	Snapshot proposed{}; // Control thread only.
	std::array<Eq, ProcessingRoutes> eq{};
	std::array<Compressor, ProcessingRoutes> compressors{};
	std::array<std::atomic<float>, ProcessingMeters> meters{};
	signalsmith::basics::ReverbFloat reverb{100};
	signalsmith::basics::LimiterFloat limiter{10};
	std::array<std::array<float, Block>, 2> roomIn{}, roomOut{}, limitIn{}, limitOut{};
	std::array<std::array<float, MaxDelay>, 2> predelay{}, dryDelay{};
	int delayCursor = 0, dryCursor = 0;
	float master = 1, wet = 0, predelaySamples = 0, width = 1, processingMix = 0, limiterMix = 0;
	bool silent = false;
	bool initialized = false;
	std::uint32_t denormalState = 0;
	explicit Impl(int sampleRate) : rate(sampleRate), latency((sampleRate + 999) / 1000) {
		if (!reverb.configure(rate, Block, 2) || !limiter.configure(rate, Block, 2)) throw std::runtime_error("DSP configuration rejected");
		reverb.dry = 0; reverb.wet = 1; reverb.roomMs = 30; reverb.rt20 = .15;
		limiter.attackMs = 1; limiter.holdMs = 0; limiter.releaseMs = 80; limiter.linkChannels = 1; limiter.outputLimit = Db(-1);
		reverb.reset(); limiter.reset();
		for (auto& meter : meters) meter.store(0);
	}
	void Reset() {
		reverb.reset(); limiter.reset();
		for (auto& filter : eq) filter.Reset();
		for (auto& comp : compressors) comp = Compressor{};
		for (auto& ch : predelay) ch.fill(0);
		for (auto& ch : dryDelay) ch.fill(0);
		delayCursor = dryCursor = 0;
		master = 0; wet = 0;
	}
};
AudioProcessing::AudioProcessing(int rate) : impl(std::make_unique<Impl>(rate)) {}
AudioProcessing::~AudioProcessing() = default;

bool AudioProcessing::Set(const float* values, int count) {
	if (!values || count != ProcessingValues) return false;
	for (int i = 0; i < count; ++i) if (!std::isfinite(values[i])) return false;
	auto& p = *impl;
	const auto w = p.write.load(std::memory_order_relaxed);
	if (w - p.read.load(std::memory_order_acquire) >= Queue) return false;
	Snapshot s;
	s.enabled = values[0] != 0; s.bypass = values[1] != 0; s.master = Bound(values[2], 0, 1); s.mute = values[3] != 0;
	s.reverb = values[4] != 0; s.amount = Bound(values[5], 0, 1); s.room = Bound(values[6], 10, 100);
	s.decay = Bound(values[7], .1f, 3.f) / 3; // UI uses RT60; Signalsmith's parameter is RT20.
	s.predelay = Bound(values[8], 0, 100) * p.rate * .001f; s.damping = Bound(values[9], 1000, std::min(20000.f, p.rate*.45f));
	s.width = Bound(values[10], 0, 1.5f); s.limiter = values[11] != 0; s.ceiling = Db(Bound(values[12], -12, 0)); s.release = Bound(values[13], 10, 250);
	for (int route = 0; route < ProcessingRoutes; ++route) {
		const float* v = values + ProcessingHeader + route * ProcessingRoute;
		auto& r = s.routes[route];
		if (v[0] != 0) for (int band = 0; band < Bands; ++band) if (v[1+band*4] != 0) {
			const auto f = Bound(v[2+band*4], 20, std::min(20000.f, p.rate*.45f)) / p.rate;
			const auto gain = Bound(v[3+band*4], -12, 12), q = Bound(v[4+band*4], .1f, 10);
			auto& filter = r.eq[band];
			if (band == 0) filter.highpassQ(f, q);
			else if (band == 1) filter.lowpassQ(f, q);
			else if (band == 2) filter.lowShelfDbQ(f, gain, q);
			else if (band == 3) filter.highShelfDbQ(f, gain, q);
			else filter.peakDbQ(f, gain, q);
		}
		r.compress = v[33] != 0; r.threshold = Bound(v[34], -60, 0); r.ratio = Bound(v[35], 1, 20); r.knee = Bound(v[36], 0, 24);
		r.attack = 1 - std::exp(-1 / (p.rate * Bound(v[37], .1f, 100) * .001f));
		r.release = 1 - std::exp(-1 / (p.rate * Bound(v[38], 10, 1000) * .001f)); r.makeup = Db(Bound(v[39], 0, 12)); r.send = Bound(v[40], 0, 1);
	}
	p.proposed = s; p.queue[w % Queue] = s; p.write.store(w + 1, std::memory_order_release); return true;
}

void AudioProcessing::Process(float* buses, int stride, float* output, int frames, bool paused, bool preview) {
	auto& p = *impl;
	// STFX also guards SSE/x64; VPE covers the full chain on x86 and ARM64 as well.
	[[maybe_unused]] ScopedFloatState denormals;
	const auto w = p.write.load(std::memory_order_acquire); const auto r = p.read.load(std::memory_order_relaxed);
	if (w != r) {
		p.current = p.queue[(w-1) % Queue]; p.read.store(w, std::memory_order_release);
		for (int route = 0; route < ProcessingRoutes; ++route) p.eq[route].Change(p.current.routes[route], p.rate, !p.initialized);
		p.reverb.roomMs = p.current.room; p.reverb.rt20 = p.current.decay; p.reverb.highCutHz = p.current.damping;
		p.limiter.outputLimit = p.current.ceiling; p.limiter.releaseMs = p.current.release;
		if (!p.initialized) {
			// A saved low volume must apply from the first sample, including after a rate reset.
			p.master = p.current.master; p.width = p.current.width; p.predelaySamples = p.current.predelay;
			p.processingMix = p.current.enabled && !p.current.bypass ? 1.f : 0.f;
			p.limiterMix = p.processingMix && p.current.limiter ? 1.f : 0.f;
			p.initialized = true;
		}
	}
	const auto& s = p.current;
	if (s.mute || s.master == 0 || (paused && !preview)) {
		if (!p.silent) { p.Reset(); p.silent = true; }
		std::fill_n(output, frames*2, 0.f);
		for (auto& meter : p.meters) meter.store(0, std::memory_order_relaxed);
		p.meters[11].store(1000.f*p.latency/p.rate); return;
	}
	p.silent = false;
	const bool enabled = s.enabled && !s.bypass;
	const float slew = 1 - std::exp(-1.f / (p.rate * .01f));
	std::array<float, ProcessingMeters> meters{};
	for (int f = 0; f < frames; ++f) {
		float left = 0, right = 0, dryLeft = 0, dryRight = 0, roomLeft = 0, roomRight = 0;
		for (int bus = 0; bus < 7; ++bus) {
			float l = Clean(buses[bus*stride+f*2]), rr = Clean(buses[bus*stride+f*2+1]);
			dryLeft += l; dryRight += rr;
			l = p.eq[bus].Sample(l, 0); rr = p.eq[bus].Sample(rr, 1); p.eq[bus].Advance();
			p.compressors[bus].Process(l, rr, s.routes[bus], slew);
			meters[bus] = std::max(meters[bus], p.compressors[bus].reduction * p.compressors[bus].mix);
			roomLeft += l*s.routes[bus].send; roomRight += rr*s.routes[bus].send; left += l; right += rr;
		}
		meters[13] = std::max(meters[13], std::abs(dryLeft)); meters[14] = std::max(meters[14], std::abs(dryRight));
		p.predelaySamples += (s.predelay - p.predelaySamples)*slew;
		p.predelay[0][p.delayCursor] = enabled && s.reverb ? roomLeft : 0; p.predelay[1][p.delayCursor] = enabled && s.reverb ? roomRight : 0;
		const double position = p.delayCursor + MaxDelay - p.predelaySamples;
		const int a = static_cast<int>(position) % MaxDelay, b = (a + 1) % MaxDelay; const float mix = static_cast<float>(position - std::floor(position));
		for (int ch = 0; ch < 2; ++ch) p.roomIn[ch][f] = p.predelay[ch][a] + (p.predelay[ch][b]-p.predelay[ch][a])*mix;
		p.delayCursor = (p.delayCursor + 1) % MaxDelay;
		p.limitIn[0][f] = left; p.limitIn[1][f] = right;
		// Preserve the pre-effects sum for a latency-matched bypass comparison.
		output[f*2] = dryLeft; output[f*2+1] = dryRight;
	}
	float* roomInput[] = {p.roomIn[0].data(), p.roomIn[1].data()}, *roomOutput[] = {p.roomOut[0].data(), p.roomOut[1].data()};
	p.reverb.process(roomInput, roomOutput, frames);
	for (int f = 0; f < frames; ++f) {
		p.wet += ((enabled && s.reverb ? s.amount : 0) - p.wet)*slew; p.width += (s.width - p.width)*slew;
		const float mid = (p.roomOut[0][f] + p.roomOut[1][f])*.5f*p.wet, side = (p.roomOut[0][f] - p.roomOut[1][f])*.5f*p.wet*p.width;
		meters[12] = std::max(meters[12], std::max(std::abs(mid+side), std::abs(mid-side)));
		float l = p.eq[7].Sample(p.limitIn[0][f] + mid + side, 0), rr = p.eq[7].Sample(p.limitIn[1][f] + mid - side, 1);
		p.eq[7].Advance();
		p.compressors[7].Process(l, rr, s.routes[7], slew); meters[7] = std::max(meters[7], p.compressors[7].reduction*p.compressors[7].mix);
		p.processingMix += ((enabled ? 1.f : 0.f) - p.processingMix)*slew; p.master += (s.master - p.master)*slew;
		p.limitIn[0][f] = Clean((output[f*2] + (l-output[f*2])*p.processingMix)*p.master);
		p.limitIn[1][f] = Clean((output[f*2+1] + (rr-output[f*2+1])*p.processingMix)*p.master);
	}
	float* limitInput[] = {p.limitIn[0].data(), p.limitIn[1].data()}, *limitOutput[] = {p.limitOut[0].data(), p.limitOut[1].data()};
	p.limiter.process(limitInput, limitOutput, frames);
	for (int f = 0; f < frames; ++f) {
		const float limiterTarget = enabled && s.limiter ? 1.f : 0.f;
		p.limiterMix += (limiterTarget-p.limiterMix)*slew;
		if (std::abs(limiterTarget-p.limiterMix) < .0001f) p.limiterMix = limiterTarget;
		for (int ch = 0; ch < 2; ++ch) {
			const float dry = p.dryDelay[ch][p.dryCursor]; p.dryDelay[ch][p.dryCursor] = p.limitIn[ch][f];
			const float limited = p.limitOut[ch][f];
			if (std::abs(dry) > 1e-10f) meters[8] = std::max(meters[8], -20*std::log10(std::max(1e-10f, std::abs(limited/dry)))*p.limiterMix);
			output[f*2+ch] = Clean(dry + (limited - dry)*p.limiterMix);
		}
		// Float envelope roundoff and a live enable transition must not breach the promised
		// sample-peak ceiling. One shared correction retains the stereo image.
		const float peak = std::max(std::abs(output[f*2]), std::abs(output[f*2+1]));
		if (enabled && s.limiter && peak > s.ceiling) {
			const float guard = std::nextafter(s.ceiling, 0.f)/peak;
			output[f*2] *= guard; output[f*2+1] *= guard;
			meters[8] = std::max(meters[8], -20*std::log10(guard));
		}
		for (int ch = 0; ch < 2; ++ch) meters[9+ch] = std::max(meters[9+ch], std::abs(output[f*2+ch]));
		p.dryCursor = (p.dryCursor + 1) % p.latency;
	}
	meters[11] = 1000.f*p.latency/p.rate; meters[15] = enabled ? 1.f : 0.f;
	for (int i = 0; i < 8; ++i) meters[i] *= p.processingMix;
	for (int i = 0; i < ProcessingMeters; ++i) p.meters[i].store(meters[i], std::memory_order_relaxed);
	// On ARM this bounds persistent filter/envelope subnormals independently of FPCR availability.
	if (++p.denormalState % 64 == 0) for (auto& compressor : p.compressors) if (compressor.reduction < 1e-20f) compressor.reduction = 0;
}
void AudioProcessing::Meters(float* values) const { for (int i = 0; i < ProcessingMeters; ++i) values[i] = impl->meters[i].load(std::memory_order_relaxed); }
float AudioProcessing::EqResponse(int route, float hz) const {
	if (route < 0 || route >= ProcessingRoutes || !std::isfinite(hz)) return 0;
	double db = 0;
	for (const auto& filter : impl->proposed.routes[route].eq) db += filter.responseDb(std::clamp(double(hz)/impl->rate, .000001, .4999));
	return static_cast<float>(std::clamp(db, -120., 120.));
}
}
