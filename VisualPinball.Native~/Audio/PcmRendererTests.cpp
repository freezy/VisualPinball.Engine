// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
#include "PcmRenderer.h"
#include <algorithm>
#include <atomic>
#include <array>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <limits>
#include <thread>
#include <vector>
namespace {
int checks = 0;
void Check(bool value, const char* message) { ++checks; if (!value) { std::fprintf(stderr,"FAIL: %s\n",message); std::exit(1); } }
bool Near(float a,float b) { return std::abs(a-b)<.00001f; }
struct Session { void* c=VpePcmCreate(); ~Session(){VpePcmDestroy(c);} };
}
int main() {
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
	std::printf("Passed %d PCM renderer checks\n",checks);
}
