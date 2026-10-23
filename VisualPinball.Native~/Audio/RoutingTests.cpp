// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <limits>
#include "ThirdParty/Unity/AudioPluginInterface.h"

extern "C" int AUDIO_CALLING_CONVENTION VpeRoutingGetMetrics(int, float*, int);
extern "C" int AUDIO_CALLING_CONVENTION VpeRoutingClearMetrics(int);
extern "C" int AUDIO_CALLING_CONVENTION VpeRoutingSetGenerator(int, int, float);
namespace {
int checks = 0;
void Check(bool value, const char* message) {
	++checks;
	if (!value) { std::fprintf(stderr, "FAIL: %s\n", message); std::exit(1); }
}
bool Near(float a, float b) { return std::abs(a-b) < .00001f; }
}
int main() {
	UnityAudioEffectDefinition** effects = nullptr;
	Check(UnityGetAudioEffectDefinitions(&effects) == 3, "three plugin definitions");
	Check((effects[0]->flags & UnityAudioEffectDefinitionFlags_IsSpatializer) != 0, "panner owns spatialization");
	Check(effects[1]->flags == 0, "meter is a mixer effect");
	UnityAudioEffectState state{};
	state.structsize = sizeof(state); state.samplerate = 48000; state.hostapiversion = 0x010402;
	UnityAudioSpatializerData spatial{}; state.spatializerdata = &spatial;
	Check(effects[0]->create(&state) == UNITY_AUDIODSP_OK, "create");
	float attenuation = 0;
	spatial.distanceattenuationcallback(&state, 100000.f, 0.f, &attenuation);
	Check(Near(attenuation, 1), "distance does not attenuate SSF");
	for (int channels : {1,2,8}) {
		std::array<float, 64> input{}, output{};
		for (int i=0;i<channels*8;++i) input[i] = .01f*(i+1);
		effects[0]->setfloatparameter(&state,2,0);
		Check(effects[0]->process(&state,input.data(),output.data(),8,channels,channels)==UNITY_AUDIODSP_OK,"passthrough status");
		for(int i=0;i<channels*8;++i) Check(Near(input[i],output[i]),"passthrough preserves samples");
	}
	for (int rate : {22050,44100,48000,96000}) for(int frames : {1,7,64,257}) {
		state.samplerate=rate;
		for(int corner=0;corner<5;++corner) {
			const float x=corner==4?.5f:static_cast<float>(corner%2), y=corner==4?.5f:static_cast<float>(corner/2);
			effects[0]->setfloatparameter(&state,0,x); effects[0]->setfloatparameter(&state,1,y); effects[0]->setfloatparameter(&state,2,1);
			std::array<float,257*8> input{},output{};
			for(int frame=0;frame<frames;++frame){input[frame*8]=.02f/std::sqrt(2.f);input[frame*8+1]=.02f/std::sqrt(2.f);}
			effects[0]->process(&state,input.data(),output.data(),frames,8,8);
			for(int frame=0;frame<frames;++frame) {
				float power=0;
				for(int c=0;c<4;++c)Check(output[frame*8+c]==0,"cabinet does not leak to backbox/center/LFE");
				for(int c=0;c<4;++c){float expected=corner==4?.01f:(corner==c?.02f:0);Check(Near(output[frame*8+c+4],expected),"corner placement");power+=output[frame*8+c+4]*output[frame*8+c+4];}
				Check(Near(power,.0004f),"electrical power preserved");
			}
		}
	}
	std::array<float,16> input{},output{};input[0]=.02f;input[1]=-.01f;
	effects[0]->setfloatparameter(&state,2,2);
	effects[0]->process(&state,input.data(),output.data(),1,8,8);
	Check(Near(output[0],.02f)&&Near(output[1],-.01f),"backbox stereo preserved");
	for(int i=2;i<8;++i)Check(output[i]==0,"program does not leak to cabinet");
	effects[0]->setfloatparameter(&state,2,1);
	std::fill(output.begin(),output.end(),9.f);
	effects[0]->process(&state,input.data(),output.data(),2,2,2);
	for(int i=0;i<4;++i)Check(output[i]==0,"unsupported cabinet layout mutes instead of downmixing");
	Check(effects[0]->setfloatparameter(&state,0,std::numeric_limits<float>::quiet_NaN())!=UNITY_AUDIODSP_OK,"reject NaN parameter");
	Check(effects[0]->setfloatparameter(&state,3,0)!=UNITY_AUDIODSP_OK,"master slot reserved");
	Check(effects[0]->setfloatparameter(&state,3,16)!=UNITY_AUDIODSP_OK,"reject invalid slot");
	effects[0]->setfloatparameter(&state,2,0);
	input[0]=std::numeric_limits<float>::quiet_NaN(); input[1]=std::numeric_limits<float>::infinity(); input[2]=-.03f;
	effects[0]->process(&state,input.data(),input.data(),2,8,8);
	Check(input[0]==0 && input[1]==0 && Near(input[2],-.03f),"in-place passthrough sanitizes invalid samples and preserves finite samples");
	input.fill(0); input[0]=.02f/std::sqrt(2.f); input[1]=input[0];
	effects[0]->setfloatparameter(&state,0,0); effects[0]->setfloatparameter(&state,1,0); effects[0]->setfloatparameter(&state,2,1);
	effects[0]->process(&state,input.data(),input.data(),1,8,8);
	Check(Near(input[4],.02f) && input[0]==0 && input[1]==0,"in-place cabinet routing preserves the captured input");
	std::array<float,18> oversized; oversized.fill(9.f);
	Check(effects[0]->process(&state,input.data(),oversized.data(),2,8,9)!=UNITY_AUDIODSP_OK,"reject oversized output layout");
	for(float sample:oversized)Check(sample==0,"unsupported output layout is silent");
	std::array<float,24> metrics{};
	Check(VpeRoutingGetMetrics(1,metrics.data(),24)==1,"read diagnostics");
	Check(metrics[3]==1,"unsupported layout recorded");
	Check(VpeRoutingClearMetrics(1)==1,"clear diagnostics");
	Check(VpeRoutingGetMetrics(16,metrics.data(),24)==0,"reject metric slot");
	float parameter=0;
	Check(effects[0]->getfloatparameter(&state,0,&parameter,nullptr)==UNITY_AUDIODSP_OK && parameter==0,"read panner parameter");
	effects[0]->release(&state);
	Check(state.effectdata==nullptr,"release");
	Check(effects[1]->create(&state)==UNITY_AUDIODSP_OK,"meter create");
	for(int channels:{2,8}) {
		input.fill(.013f); output.fill(0);
		effects[1]->process(&state,input.data(),output.data(),2,channels,channels);
		for(int i=0;i<2*channels;++i)Check(Near(input[i],output[i]),"meter preserves finite samples");
		VpeRoutingGetMetrics(0,metrics.data(),24);
		Check(metrics[0]==channels && metrics[1]==channels && metrics[16]>.012f,"meter publishes its own slot");
	}
	effects[1]->release(&state);
	Check(effects[2]->channels==8 && effects[2]->flags==0,"ordinary eight-channel generator");
	Check(effects[2]->create(&state)==UNITY_AUDIODSP_OK,"generator create");
	state.samplerate=48000;
	std::array<float,48*8> generated{};
	for(int channel: {0,1,4,5,6,7}) {
		Check(VpeRoutingSetGenerator(channel,1000,.02f)==1,"set generator snapshot");
		effects[2]->process(&state,nullptr,generated.data(),48,0,8);
		float peak=0;
		for(int frame=0;frame<48;++frame) for(int c=0;c<8;++c) {
			if(c==channel) peak=std::max(peak,std::abs(generated[frame*8+c]));
			else Check(generated[frame*8+c]==0,"generator isolates the selected output");
		}
		Check(Near(peak,.02f),"generator amplitude");
	}
	Check(VpeRoutingSetGenerator(8,1000,.02f)==0 && VpeRoutingSetGenerator(0,1000,.03f)==0,"reject invalid generator controls");
	Check(VpeRoutingSetGenerator(0,1000,0)==1,"mute generator");
	effects[2]->process(&state,nullptr,generated.data(),48,0,8);
	for(float sample:generated) Check(sample==0,"generator stops immediately");
	effects[2]->release(&state);
	std::printf("Passed %d routing checks\n",checks);
	return 0;
}
