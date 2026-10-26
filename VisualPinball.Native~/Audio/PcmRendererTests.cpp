// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
#include "PcmRenderer.h"
#include <algorithm>
#include <atomic>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <limits>
#include <string>
#include <thread>
#include <vector>
namespace {
int checks = 0;
void Check(bool value, const char* message) { ++checks; if (!value) { std::fprintf(stderr,"FAIL: %s\n",message); std::exit(1); } }
bool Near(float a,float b) { return std::abs(a-b)<.00001f; }
struct Session { void* c=VpePcmCreate(); ~Session(){VpePcmDestroy(c);} };
}
int main(int argc, char** argv) {
	for (int rate : {22050,44100,48000,96000}) {
		Session s; std::array<float,8> clip{{.1f,.2f,.3f,.4f,.5f,.6f,.7f,.8f}};
		int bank=VpePcmRegisterClip(s.c,clip.data(),4,2,rate);
		auto id=VpePcmPlay(s.c,bank,3,1,1,0,0);
		std::array<float,8> output{};
		Check(VpePcmRender(s.c,output.data(),4,2,rate,0)==1,"stereo render");
		for(int i=0;i<8;++i)Check(Near(output[i],clip[i]),"centered stereo preserves every sample and channel");
		Check(!VpePcmActive(s.c,id),"one-shot completes at exact end");
		id=VpePcmPlay(s.c,bank,3,1,1,0,1);
		VpePcmRender(s.c,output.data(),4,2,rate,1);
		for(float v:output)Check(v==0,"global pause is silent");
		VpePcmRender(s.c,output.data(),4,2,rate,0);
		Check(Near(output[0],clip[0]),"global pause preserves cursor");
		VpePcmStop(s.c,id); Check(!VpePcmActive(s.c,id),"stop visible before next block");
		VpePcmRender(s.c,output.data(),4,2,rate,0); for(float v:output)Check(v==0,"stop silences looping voice");
	}
	{
		Session s; std::array<float,4> mono{{1,1,1,1}}; int clip=VpePcmRegisterClip(s.c,mono.data(),4,1,48000);
		VpePcmSetBusGain(s.c,1,.5f); auto id=VpePcmPlay(s.c,clip,1,.5f,1,-1,1);
		std::array<float,2048> output{}; VpePcmRender(s.c,output.data(),1024,2,48000,0);
		Check(Near(output[0],.25f) && output[1]==0,"voice and bus gain applied once, hard-left mono");
		VpePcmUpdate(s.c,id,.5f,1,1,0); VpePcmRender(s.c,output.data(),1024,2,48000,0);
		Check(Near(output[2047],.25f) && output[2046]==0,"moving pan reaches target without restarting loop");
		VpePcmUpdate(s.c,id,.5f,1,1,1); VpePcmRender(s.c,output.data(),1024,2,48000,0);
		for(float v:output)Check(v==0,"individual voice pause");
		VpePcmUpdate(s.c,id,.5f,1,1,0); VpePcmRender(s.c,output.data(),1024,2,48000,0);
		Check(Near(output[2047],.25f),"individual resume");
		Check(!VpePcmPlay(s.c,clip,0,std::numeric_limits<float>::quiet_NaN(),1,0,0),"reject invalid controls");
	}
	{
		Session s; std::array<float,8> ramp{{0,0,1,1,2,2,3,3}}; int clip=VpePcmRegisterClip(s.c,ramp.data(),4,2,24000);
		VpePcmPlay(s.c,clip,0,1,1,0,0); std::array<float,16> out{}; VpePcmRender(s.c,out.data(),8,2,48000,0);
		Check(Near(out[2],.5f) && Near(out[10],2.5f),"sample-rate conversion preserves fractional phase");
	}
	{
		Session s; std::array<float,4> data{{.25f,.5f,.75f,1}};
		std::array<float,2> preroll{}; VpePcmRender(s.c,preroll.data(),1,2,48000,0);
		VpePcmSetStreamGain(s.c,0); Check(VpePcmStreamPush(s.c,data.data(),2,2,48000,1,1)==2,"borrowed stereo copied"); data.fill(9);
		std::array<float,2> out{}; VpePcmRender(s.c,out.data(),1,2,48000,0);
		Check(out[0]==0 && out[1]==0,"initial integration mute applies to first block");
	}
	{
		Session s; std::array<std::int16_t,8> mono{{16384,16384,16384,16384,16384,16384,16384,16384}};
		VpePcmStreamPush(s.c,mono.data(),8,1,24000,0,1); mono.fill(0);
		std::array<float,16> out{}; VpePcmRender(s.c,out.data(),8,2,48000,0);
		for(float v:out)Check(Near(v,.5f),"PCM16 mono copied, duplicated and resampled");
		VpePcmFlushStream(s.c); VpePcmRender(s.c,out.data(),8,2,48000,0);
		for(float v:out)Check(v==0,"flush discards stale ROM samples");
	}
	{
		Session s; std::array<float,2> data{{.25f,.5f}}; int clip=VpePcmRegisterClip(s.c,data.data(),1,2,48000);
		VpePcmPlay(s.c,clip,5,1,1,0,1); VpePcmPlay(s.c,clip,5,1,1,0,1);
		std::array<float,4> out{}; VpePcmRender(s.c,out.data(),2,2,48000,0); std::array<float,12> meters{}; VpePcmMetrics(s.c,meters.data(),12);
		Check(Near(out[1],1) && Near(meters[5],1),"bus meter measures summed voices");
		std::uint64_t stale=0;
		for(int i=0;i<6000;++i) { auto id=VpePcmPlay(s.c,clip,0,1,1,0,0); if(i==0)stale=id; VpePcmRender(s.c,out.data(),2,2,48000,0); }
		Check(!VpePcmActive(s.c,stale),"generations prevent stale handle reuse");
	}
	{
		Session s; std::array<float,2> data{{1,1}}; int clip=VpePcmRegisterClip(s.c,data.data(),1,2,48000);
		auto id=VpePcmPlay(s.c,clip,0,1,1,0,1);
		for(int i=0;i<5000;++i) VpePcmUpdate(s.c,id,1,1,0,0);
		VpePcmStop(s.c,id); std::array<float,4> out{}; VpePcmRender(s.c,out.data(),2,2,48000,0);
		for(float v:out)Check(v==0,"stop cannot be lost to a full control queue");
	}
	{
		Session s; std::vector<float> data(16000,.1f); int clip=VpePcmRegisterClip(s.c,data.data(),8000,2,48000);
		auto id=VpePcmPlay(s.c,clip,0,1,1,0,1); std::vector<float> out(24000);
		VpePcmRender(s.c,out.data(),12000,2,48000,0);
		Check(Near(VpePcmPosition(s.c,id),4000.f/48000),"large blocks preserve loop cursor for output-change fallback");
		VpePcmRender(s.c,out.data(),1000,2,48000,1);
		Check(Near(VpePcmPosition(s.c,id),4000.f/48000),"global pause preserves fallback position");
		Check(!VpePcmRender(s.c,out.data(),100,8,48000,0),"unproven discrete output fails closed");
		for(int i=0;i<800;++i)Check(out[i]==0,"unsupported channels are silent");
	}
	{
		Session s; std::vector<float> data(16384,.125f); std::array<float,2048> out{}, meters{};
		Check(VpePcmStreamPush(s.c,data.data(),8192,2,48000,1,1)==8192,"bounded stream accepts its full capacity");
		Check(!VpePcmStreamPush(s.c,data.data(),1024,2,48000,1,2),"overflow rejects the complete incoming block");
		VpePcmRender(s.c,out.data(),1024,2,48000,0);
		Check(out[0]==0,"overflow flushes stale backlog");
		VpePcmStreamPush(s.c,data.data(),1024,2,48000,1,4);VpePcmRender(s.c,out.data(),1024,2,48000,0);
		Check(out[0]==0,"sequence gap discards stale audio");
		VpePcmMetrics(s.c,meters.data(),12);Check(meters[8]==1 && meters[9]==1,"overflow and discontinuity diagnostics");
	}
	{
		Session s; std::array<float,256> data{}; data.fill(.1f); int clip=VpePcmRegisterClip(s.c,data.data(),128,2,48000);
		std::atomic<bool> done{false}, finite{true};
		std::thread audio([&]{std::array<float,512> out{};while(!done.load()){VpePcmRender(s.c,out.data(),256,2,48000,0);for(float v:out)if(!std::isfinite(v))finite.store(false);}});
		for(int i=1;i<=10000;++i) {
			auto id=VpePcmPlay(s.c,clip,i%7,.5f,1,0,1);VpePcmUpdate(s.c,id,.25f,1,.5f,0);VpePcmStop(s.c,id);
			VpePcmStreamPush(s.c,data.data(),128,2,48000,1,i);
		}
		done.store(true);audio.join();Check(finite.load(),"concurrent controls, producer and render stay finite");
	}
	{
		Session s; std::array<float, 512> data{};
		for (int i = 0; i < 256; ++i) { data[i*2] = .2f; data[i*2+1] = .1f; }
		int clip = VpePcmRegisterClip(s.c, data.data(), 256, 2, 24000);
		auto gameVoice = VpePcmPlay(s.c, clip, 0, 1, 1, 0, 1);
		Check(!VpePcmPreviewClip(s.c, -1, 0), "preview rejects unknown clips");
		Check(!VpePcmPreviewClip(s.c, clip, 7), "preview rejects unknown buses");
		Check(VpePcmPreviewClip(s.c, clip, 1) == 1, "recorded preview starts");
		std::array<float, 1024> output{};
		VpePcmRender(s.c, output.data(), 512, 2, 48000, 1);
		Check(Near(output[0], .106f) && Near(output[1], .053f), "paused preview resamples the recording and preserves stereo");
		Check(Near(VpePcmPosition(s.c, gameVoice), 0), "preview never advances paused game voices");
		std::array<float, 12> meters{}; VpePcmMetrics(s.c, meters.data(), 12);
		Check(Near(meters[1], .106f) && meters[0] == 0, "preview reaches only its selected sound group");
		VpePcmPreview(s.c, 0, 0); VpePcmRender(s.c, output.data(), 512, 2, 48000, 1);
		Check(std::all_of(output.begin(), output.end(), [](float value) { return value == 0; }), "stop cancels recorded preview without unpausing the game");
		VpePcmPreviewClip(s.c, clip, 0);
		for (int i = 0; i < 284; ++i) VpePcmRender(s.c, output.data(), 512, 2, 48000, 1);
		Check(std::all_of(output.begin(), output.end(), [](float value) { return value == 0; }), "recorded preview ends within three seconds");
		VpePcmPreviewClip(s.c, clip, 0); VpePcmRender(s.c, output.data(), 512, 2, 48000, 1);
		VpePcmRender(s.c, output.data(), 512, 2, 24000, 1);
		Check(std::all_of(output.begin(), output.end(), [](float value) { return value == 0; }), "device sample-rate changes cancel preview safely");
	}
	{
		Session s; std::array<float,12> meters{};
		std::vector<int> ids; for (int i = 0; i < 7; ++i) ids.push_back(VpePcmStreamOpen(s.c, 5));
		Check(ids.front() == 1 && ids.back() == 7, "seven extra streams besides the ROM stream");
		Check(VpePcmStreamOpen(s.c, 5) == -1, "stream slots are bounded");
		Check(VpePcmStreamOpen(s.c, 9) == -1, "invalid bus refused");
		for (int id : ids) VpePcmStreamClose(s.c, id);
		VpePcmStreamClose(s.c, 0); { std::array<float, 6> rom{}; Check(VpePcmStreamMetrics(s.c, 0, rom.data(), 6) == 1, "the ROM stream cannot be closed"); }
		Check(VpePcmStreamOpen(s.c, 5) == -1, "a closed slot is not reused before the renderer let go of it");
		{ std::array<float, 2> idle{}; VpePcmRender(s.c, idle.data(), 1, 2, 48000, 0); }
		const int id = VpePcmStreamOpen(s.c, 5);
		Check(id == 1, "closed slots are reused once the renderer saw them closed");
		std::vector<float> block(480 * 2, .25f);
		VpePcmStreamPushTo(s.c, id, block.data(), 480, 2, 48000, 1, 1);
		std::array<float, 256 * 2> out{};
		VpePcmRender(s.c, out.data(), 256, 2, 48000, 0);
		Check(out[0] == 0, "an opened stream waits for its jitter buffer (both sides' largest blocks plus a quarter)");
		VpePcmStreamPushTo(s.c, id, block.data(), 480, 2, 48000, 1, 2);
		VpePcmRender(s.c, out.data(), 256, 2, 48000, 0);
		Check(Near(out[0], .25f) && Near(out[511], .25f), "a filled jitter buffer plays");
		VpePcmMetrics(s.c, meters.data(), 12); Check(Near(meters[5], .25f) && meters[2] == 0, "an opened stream mixes into its own bus");
		std::array<float, 6> m{};
		for (int i = 0; i < 4; ++i) VpePcmRender(s.c, out.data(), 256, 2, 48000, 0);
		VpePcmStreamMetrics(s.c, id, m.data(), 6); Check(m[2] >= 1, "running dry counts an underflow");
		VpePcmStreamPushTo(s.c, id, block.data(), 480, 2, 48000, 1, 3);
		VpePcmRender(s.c, out.data(), 256, 2, 48000, 0);
		Check(out[0] == 0, "after an underflow the jitter buffer refills before playing again");
		VpePcmStreamSetBuffer(s.c, id, 0); VpePcmRender(s.c, out.data(), 256, 2, 48000, 0);
		Check(Near(out[0], .25f), "a zero buffer plays as soon as data arrives");
		VpePcmStreamSetBuffer(s.c, id, 128);
		for (int i = 0; i < 12; ++i) VpePcmStreamPushTo(s.c, id, block.data(), 480, 2, 48000, 1, 4 + i);
		VpePcmRender(s.c, out.data(), 256, 2, 48000, 0);
		VpePcmStreamMetrics(s.c, id, m.data(), 6); Check(m[5] == 1 && m[0] <= 128, "a producer far ahead is pulled back to the buffer target");
		std::array<float, 2> rom{{.5f, .5f}}; VpePcmStreamPush(s.c, rom.data(), 1, 2, 48000, 1, 1);
		VpePcmStreamPush(s.c, rom.data(), 1, 2, 48000, 1, 2);
		VpePcmStreamPushTo(s.c, id, block.data(), 480, 2, 48000, 1, 16);
		VpePcmRender(s.c, out.data(), 1, 2, 48000, 0);
		Check(Near(out[0], .75f), "the ROM stream and an opened stream mix together");
		VpePcmStreamClose(s.c, id); VpePcmRender(s.c, out.data(), 256, 2, 48000, 0);
		Check(Near(out[2], 0) || out[2] == 0, "a closed stream stops");
		Check(VpePcmStreamPushTo(s.c, id, block.data(), 480, 2, 48000, 1, 17) == 0, "pushing to a closed stream is refused");
		Check(VpePcmStreamMetrics(s.c, 0, m.data(), 6) == 1 && m[1] == 0, "the ROM stream keeps playing as soon as data arrives");
		Check(VpePcmStreamSetBuffer(s.c, 0, -2) == 1 && VpePcmStreamMetrics(s.c, 0, m.data(), 6) == 1, "adaptive buffer accepted");
		Check(m[2] == 0 ? m[1] == 0 : m[1] > 0, "adaptive buffer adds latency only after an underflow");
	}
	{
		// A producer running 0.1% fast against the device clock for a minute: drift compensation holds the queue.
		Session s; const int id = VpePcmStreamOpen(s.c, 5); VpePcmStreamSetBuffer(s.c, id, 960);
		std::vector<float> block(480 * 2, .1f); std::array<float, 480 * 2> out{}; std::array<float, 6> m{};
		double produced = 0; long long sequence = 0;
		for (int period = 0; period < 6000; ++period) {
			for (produced += 480 * 1.001; produced >= 480; produced -= 480) VpePcmStreamPushTo(s.c, id, block.data(), 480, 2, 48000, 1, ++sequence);
			VpePcmRender(s.c, out.data(), 480, 2, 48000, 0);
		}
		VpePcmStreamMetrics(s.c, id, m.data(), 6);
		Check(m[5] == 0 && m[3] == 0 && m[0] < 960 * 2.5, "drift compensation keeps a fast producer without drops or overflows");
		Check(m[2] <= 1, "drift compensation keeps a fast producer without underflows after the start");
	}
	{
		Session s; const int id = VpePcmStreamOpen(s.c, 5); VpePcmStreamSetBuffer(s.c, id, 960);
		std::vector<float> block(480 * 2, .1f); std::array<float, 480 * 2> out{}; std::array<float, 6> m{};
		double produced = 0; long long sequence = 0;
		for (int period = 0; period < 6000; ++period) {
			for (produced += 480 * .999; produced >= 480; produced -= 480) VpePcmStreamPushTo(s.c, id, block.data(), 480, 2, 48000, 1, ++sequence);
			VpePcmRender(s.c, out.data(), 480, 2, 48000, 0);
		}
		VpePcmStreamMetrics(s.c, id, m.data(), 6);
		Check(m[2] <= 1 && m[5] == 0, "drift compensation keeps a slow producer from running dry");
	}
	{
		// The ROM stream with PinMAME's shape: 800-frame blocks every 16.7 ms, 480-frame pulls, and a 4000-frame catch-up
		// burst at start. The adaptive buffer must bound the queue instead of carrying the burst as latency.
		Session s; VpePcmStreamSetBuffer(s.c, 0, -2); VpePcmEnableStream(s.c, 1);
		std::vector<float> block(800 * 2, .1f); std::array<float, 480 * 2> out{}; std::array<float, 6> m{};
		long long sequence = 0; double due = 0;
		for (int i = 0; i < 5; ++i) VpePcmStreamPush(s.c, block.data(), 800, 2, 48000, 1, ++sequence);
		float largest = 0;
		for (int period = 0; period < 3000; ++period) {
			for (due += 480; due >= 800; due -= 800) VpePcmStreamPush(s.c, block.data(), 800, 2, 48000, 1, ++sequence);
			VpePcmRender(s.c, out.data(), 480, 2, 48000, 0);
			VpePcmStreamMetrics(s.c, 0, m.data(), 6);
			if (period > 300) largest = std::max(largest, m[0]);
		}
		Check(m[3] == 0, "the ROM stream's catch-up burst does not overflow");
		Check(largest <= m[1] * 2 + 256 + 800, "the ROM stream's queue stays bounded after a burst");
		Check(m[1] > 0 && m[1] <= 1700, "the ROM stream is steered to a block-sized buffer");
	}
	{
		Check(VpePcmOutputCreate(nullptr, 48000, 0) == nullptr, "device output needs a graph");
		Session s; Check(VpePcmOutputCreate(s.c, 1000, 0) == nullptr, "device output rejects invalid rates");
		Check(VpePcmOutputMetrics(nullptr, nullptr, 13) == 0, "metrics of a missing output are refused");
		VpePcmOutputDestroy(nullptr); VpePcmOutputSetPaused(nullptr, 1);
		// Needs a real playback device, so it only runs when asked to (--live-output).
		if (argc > 1 && std::string(argv[1]) == "--live-output") {
			std::array<float, 960> tick{}; tick[0] = .001f; int clip = VpePcmRegisterClip(s.c, tick.data(), 960, 1, 48000);
			void* output = VpePcmOutputCreate(s.c, 48000, 0);
			Check(output != nullptr, "live device output opens the default endpoint");
			auto id = VpePcmPlay(s.c, clip, 0, 1, 1, 0, 0);
			std::this_thread::sleep_for(std::chrono::milliseconds(300));
			std::array<float, 13> metrics{}; Check(VpePcmOutputMetrics(output, metrics.data(), 13) == 1, "live metrics");
			std::printf("live output: state %.0f, callbacks %.0f, period %.2f ms, buffer %.2f ms, queued %.2f ms, margin %.2f ms, underruns %.0f, max wake gap %.2f ms\n",
				metrics[0], metrics[1], metrics[2], metrics[3], metrics[4], metrics[5], metrics[6], metrics[12]);
			Check(metrics[0] == 1 && metrics[1] > 10, "live output keeps rendering");
			Check(!VpePcmActive(s.c, id), "live output advances one-shot voices");
			VpePcmOutputDestroy(output);
		}
	}
	for (int rate : {44100, 48000, 96000}) {
		Session s; const std::array<float, 2> constant{{.4f, .2f}};
		int clip = VpePcmRegisterClip(s.c, constant.data(), 1, 2, rate);
		VpePcmPlay(s.c, clip, 0, 1, 1, 0, 1);
		std::array<float, 2> sample{};
		VpePcmRender(s.c, sample.data(), 1, 2, rate, 0);
		Check(VpePcmSetTransitionGain(s.c, 0, 100) == 1, "master fade accepts a bounded duration");
		float previous = sample[0]; bool smooth = true, stereo = true;
		for (int i = 0; i < rate/10; ++i) {
			VpePcmRender(s.c, sample.data(), 1, 2, rate, 0);
			smooth = smooth && sample[0] <= previous && previous - sample[0] < .001f;
			stereo = stereo && Near(sample[0], sample[1] * 2); previous = sample[0];
		}
		Check(smooth && sample[0] == 0, "master reaches silence smoothly in exactly 100 ms");
		Check(stereo, "master fade preserves the stereo image");
		VpePcmRender(s.c, sample.data(), 1, 2, rate, 1);
		Check(sample[0] == 0, "the hard pause remains silent after fading");
		VpePcmSetTransitionGain(s.c, 1, 100); previous = 0; smooth = true;
		for (int i = 0; i < rate/10; ++i) {
			VpePcmRender(s.c, sample.data(), 1, 2, rate, 0);
			smooth = smooth && sample[0] >= previous && sample[0] - previous < .001f; previous = sample[0];
		}
		Check(smooth && Near(sample[0], .4f), "resume fades smoothly back to the authored level");
		VpePcmSetTransitionGain(s.c, 0, 100);
		std::vector<float> half(rate/20*2); VpePcmRender(s.c, half.data(), rate/20, 2, rate, 0);
		Check(Near(half.back(), .1f), "the fade duration is independent of callback block size");
		VpePcmSetTransitionGain(s.c, 1, 100); VpePcmRender(s.c, sample.data(), 1, 2, rate, 0);
		Check(sample[0] > .19f && sample[0] < .21f, "a reversed transition starts at the current envelope value");
		Check(!VpePcmSetTransitionGain(s.c, std::numeric_limits<float>::quiet_NaN(), 100) && !VpePcmSetTransitionGain(s.c, 1, -1), "invalid transition controls are rejected");
		VpePcmSetTransitionGain(s.c, 1, 0); VpePcmRender(s.c, sample.data(), 1, 2, rate, 0);
		Check(Near(sample[0], .4f), "teardown can reset the transient gain immediately");
	}
	{
		Session s; std::array<float, 2> recording{{.2f, .1f}};
		int clip = VpePcmRegisterClip(s.c, recording.data(), 1, 2, 48000);
		VpePcmSetTransitionGain(s.c, 0, 0); VpePcmPreviewClip(s.c, clip, 0);
		std::array<float, 2> sample{}; VpePcmRender(s.c, sample.data(), 1, 2, 48000, 1);
		Check(Near(sample[0], .106f), "settings preview bypasses the paused game's master envelope");
	}
	std::printf("Passed %d PCM renderer checks\n",checks);
}
