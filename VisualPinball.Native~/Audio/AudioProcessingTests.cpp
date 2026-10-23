// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
#include "AudioProcessing.h"
#include "PcmRenderer.h"
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdlib>
#include <iostream>
#include <new>
#include <thread>
#include <vector>

namespace { thread_local bool rendering = false; std::atomic<int> allocations{0}; int checks = 0; }
void* operator new(std::size_t size) { if (rendering) ++allocations; if (auto* p = std::malloc(size)) return p; throw std::bad_alloc(); }
void operator delete(void* p) noexcept { std::free(p); }
void operator delete(void* p, std::size_t) noexcept { std::free(p); }
void* operator new[](std::size_t size) { return ::operator new(size); }
void operator delete[](void* p) noexcept { std::free(p); }
void operator delete[](void* p, std::size_t) noexcept { std::free(p); }
namespace {
void Check(bool valid, const char* message) { ++checks; if (!valid) { std::cerr << message << '\n'; std::exit(1); } }
constexpr int Frames = 256;
using Parameters = std::array<float, vpe::ProcessingValues>;
Parameters Defaults() {
	Parameters p{}; p[2] = 1; p[5] = .1f; p[6] = 30; p[7] = .45f; p[9] = 8000; p[10] = 1; p[12] = -1; p[13] = 80;
	for (int i = 0; i < 8; ++i) {
		auto* r = p.data()+vpe::ProcessingHeader+i*vpe::ProcessingRoute;
		for (int b = 0; b < 8; ++b) { r[2+b*4] = 1000; r[4+b*4] = .7071068f; }
		r[34] = -18; r[35] = 2; r[36] = 6; r[37] = 10; r[38] = 120;
	}
	return p;
}
struct Harness {
	vpe::AudioProcessing dsp;
	std::array<float, 7*Frames*2> buses{};
	std::array<float, Frames*2> out{};
	explicit Harness(int rate = 48000) : dsp(rate) {}
	void Set(const Parameters& p) { Check(dsp.Set(p.data(), int(p.size())), "settings accepted"); }
	void Process(bool paused = false, bool preview = false) {
		rendering = true; dsp.Process(buses.data(), Frames*2, out.data(), Frames, paused, preview); rendering = false;
		for (auto value : out) Check(std::isfinite(value), "finite DSP output");
	}
};
void DryAndLinking() {
	Harness h; auto p = Defaults(); h.Set(p);
	for (int f = 0; f < Frames; ++f) { h.buses[f*2] = .1f; h.buses[f*2+1] = -.3f; }
	h.Process();
	for (int f = 0; f < Frames; ++f) { Check(std::abs(h.out[f*2] - (f<48 ? 0.f : .1f)) < 1e-6f, "dry left and fixed 1 ms delay"); Check(std::abs(h.out[f*2+1] - (f<48 ? 0.f : -.3f)) < 1e-6f, "dry stereo preserved"); }
	p[0] = 1; auto* route = p.data()+vpe::ProcessingHeader; route[33] = 1; route[34] = -24; route[35] = 4; route[36] = 0; h.Set(p);
	for (int n = 0; n < 500; ++n) h.Process();
	Check(h.out[400] < .04f, "compressor reduces loud signal"); Check(std::abs(h.out[401]/h.out[400]+3) < 1e-4f, "compressor links stereo position");
	std::array<float, vpe::ProcessingMeters> meters{}; h.dsp.Meters(meters.data()); Check(meters[0] > 10 && meters[8] == 0, "separate compressor and limiter meters");
	p[1] = 1; h.Set(p); for (int n=0; n<100; ++n) h.Process(); Check(std::abs(h.out[400]-.1f) < 1e-5f, "global bypass restores dry level");
	p[2] = .25f; h.Set(p); for (int n=0; n<100; ++n) h.Process(); Check(std::abs(h.out[400]-.025f) < 1e-5f, "master gain applied once in bypass");
}
void Limiting() {
	for (int rate : { 8000, 44100, 48000, 96000, 192000 }) {
		Harness h(rate); auto p = Defaults(); p[0] = p[11] = 1; h.Set(p);
		for (int n=0; n<100; ++n) h.Process();
		unsigned noise = 42;
		for (int n=0; n<120; ++n) {
			for (int f=0; f<Frames; ++f) { noise = noise*1664525u+1013904223u; const float value = (float(noise>>8)/8388608.f-1)*4; h.buses[f*2] = value; h.buses[f*2+1] = value*.25f; }
			h.Process();
			for (int f=0; f<Frames; ++f) { if(std::abs(h.out[f*2]) > .89126f) std::cerr << "rate=" << rate << " peak=" << h.out[f*2] << " block=" << n << " frame=" << f << "\n"; Check(std::abs(h.out[f*2]) <= .89126f, "limiter ceiling at every supported rate"); Check(std::abs(h.out[f*2+1]-h.out[f*2]*.25f) < 1e-5f, "limiter fully links stereo"); }
		}
	}
}
void EqAndReverb() {
	Harness h; auto p = Defaults(); p[0] = 1; auto* route = p.data()+vpe::ProcessingHeader;
	route[0] = route[1+4*4] = 1; route[2+4*4] = 1000; route[3+4*4] = 6; route[4+4*4] = 1; h.Set(p);
	int cursor=0; double inEnergy=0, outEnergy=0;
	for (int n=0; n<150; ++n) {
		for (int f=0; f<Frames; ++f) h.buses[f*2] = h.buses[f*2+1] = .05f*std::sin(float(cursor++)*6.28318530718f*1000/48000);
		h.Process(); if (n>100) for (int f=0; f<Frames; ++f) { inEnergy += h.buses[f*2]*h.buses[f*2]; outEnergy += h.out[f*2]*h.out[f*2]; }
	}
	Check(std::abs(10*std::log10(outEnergy/inEnergy)-6)<.05, "EQ bell produces measured 6 dB gain");
	Harness room; p = Defaults(); p[0] = p[4] = 1; p[5] = 1; p[vpe::ProcessingHeader+40] = 1; room.Set(p);
	for (int n=0; n<100; ++n) { room.Process(); }
	room.buses[0] = room.buses[1] = 1; room.Process(); room.buses.fill(0);
	double tail=0; for (int n=0; n<40; ++n) { room.Process(); for (auto value : room.out) tail += value*value; }
	Check(tail > .00001, "FDN reverb produces a tail");
	room.Process(true); for (auto value : room.out) Check(value==0, "pause silences output");
	double after=0; for (int n=0; n<150; ++n) { room.Process(); for (auto value : room.out) after += value*value; }
	Check(after==0, "pause clears all reverb and delay state");
	room.buses[0] = room.buses[1] = 1; room.Process(); room.buses.fill(0); p[3] = 1; room.Set(p); room.Process();
	p[3] = 0; room.Set(p); after=0; for (int n=0; n<100; ++n) { room.Process(); for (auto value : room.out) after += value*value; } Check(after==0, "unmute cannot resurrect a tail");
	p[7] = NAN; Check(!room.dsp.Set(p.data(), int(p.size())), "nonfinite parameters rejected atomically");
}
void GraphLifetime() {
	void* context = VpePcmCreate(); Check(context!=nullptr, "context created"); auto p=Defaults(); p[0]=p[11]=1;
	Check(VpePcmPrepareDsp(context,48000)==1 && VpePcmSetProcessing(context,p.data(),int(p.size()))==1, "extension prepared");
	std::atomic<bool> stop{false};
	std::thread audio([&] { std::array<float,Frames*2> out{}; while(!stop.load()) { rendering=true; VpePcmRender(context,out.data(),Frames,2,48000,1); rendering=false; } });
	for(int n=0;n<100;++n) { VpePcmPrepareDsp(context,n%2 ? 48000 : 44100); VpePcmSetProcessing(context,p.data(),int(p.size())); std::array<float,16> meters{}; VpePcmProcessingMeters(context,meters.data(),16); }
	stop.store(true); audio.join(); VpePcmDestroy(context);
	Check(allocations.load()==0,"render callback allocates nothing, including first use and graph adoption");
}
void RomGain() {
	void* context = VpePcmCreate(); auto p = Defaults(); p[2] = .5f;
	Check(VpePcmPrepareDsp(context,48000)==1 && VpePcmSetProcessing(context,p.data(),int(p.size()))==1,"ROM DSP prepared");
	VpePcmSetStreamGain(context,.25f); VpePcmSetBusGain(context,2,.5f);
	std::array<float,Frames*2> input{}, output{};
	for(int f=0;f<Frames;++f) { input[f*2]=.4f; input[f*2+1]=.8f; }
	for(int n=0;n<100;++n) { VpePcmStreamPush(context,input.data(),Frames,2,48000,1,n+1); VpePcmRender(context,output.data(),Frames,2,48000,0); }
	Check(std::abs(output[400]-.025f)<.00001f && std::abs(output[401]-.05f)<.00001f,"integration gain, ROM trim and master each applied once");
	VpePcmDestroy(context);
}
void InitialMasterAndReset() {
	void* context = VpePcmCreate(); auto p = Defaults(); p[2] = .1f;
	Check(VpePcmPrepareDsp(context,48000)==1 && VpePcmSetProcessing(context,p.data(),int(p.size()))==1,"initial saved volume prepared");
	std::array<float,2048> samples{}; for(int f=0;f<1024;++f) { samples[f*2]=.2f; samples[f*2+1]=-.1f; }
	const int clip = VpePcmRegisterClip(context,samples.data(),1024,2,48000); VpePcmPlay(context,clip,0,1,1,0,1);
	std::array<float,Frames*2> output{}; VpePcmRender(context,output.data(),Frames,2,48000,0);
	Check(std::abs(output[96]-.02f)<1e-6f,"saved master applies to the very first audible sample");
	p[2]=.2f; VpePcmSetProcessing(context,p.data(),int(p.size())); VpePcmRender(context,output.data(),Frames,2,48000,0);
	Check(output[96]<.039f,"subsequent live master changes remain smoothed");
	Check(VpePcmPrepareDsp(context,44100)==1,"replacement graph prepared"); VpePcmRender(context,output.data(),Frames,2,44100,0);
	Check(std::abs(output[90]-.04f)<1e-6f,"rate reset starts at saved master rather than full gain");
	VpePcmDestroy(context);
}
void RapidEqChanges() {
	Harness h; auto p=Defaults(); p[0]=1; auto* route=p.data()+vpe::ProcessingHeader; route[0]=route[9]=1; route[10]=300; route[11]=-12; h.Set(p);
	int cursor=0; float previous=0, maxJump=0;
	for(int n=0;n<500;++n) {
		for(int f=0;f<16;++f) h.buses[f*2]=h.buses[f*2+1]=.005f*std::sin(float(cursor++)*6.28318530718f*100/48000);
		if(n>100) { route[11]=n%2 ? 12.f : -12.f; h.Set(p); }
		rendering=true; h.dsp.Process(h.buses.data(),Frames*2,h.out.data(),16,false,false); rendering=false;
		for(int f=0;f<16;++f) { if(n>100) maxJump=std::max(maxJump,std::abs(h.out[f*2]-previous)); previous=h.out[f*2]; }
	}
	Check(maxJump<.001f,"EQ edits faster than the fade do not jump to an unfinished target filter");
}
}
int main() { DryAndLinking(); Limiting(); EqAndReverb(); GraphLifetime(); RomGain(); InitialMasterAndReset(); RapidEqChanges(); std::cout << checks << " DSP checks passed; no callback allocations\n"; }
