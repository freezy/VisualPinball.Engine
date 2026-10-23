/* Copyright 2022+ Signalsmith Audio Ltd. / Geraint Luff
MIT License - see LICENSE.txt and SUPPORT.txt */
#pragma once

#include "signalsmith-dsp/delay.h"
#include "signalsmith-dsp/mix.h"
#include "signalsmith-dsp/filters.h"
SIGNALSMITH_DSP_VERSION_CHECK(1, 3, 3)

#include "../stfx/stfx-library.h"

#include <cmath>
#include <random>

namespace signalsmith { namespace basics {

template<class BaseEffect>
struct ReverbSTFX;

using ReverbFloat = stfx::LibraryEffect<float, ReverbSTFX>;
using ReverbDouble = stfx::LibraryEffect<double, ReverbSTFX>;

template<class BaseEffect>
struct ReverbSTFX : public BaseEffect {
	using typename BaseEffect::Sample;
	using typename BaseEffect::ParamRange;
	using typename BaseEffect::ParamStepped;
	using Array = std::array<Sample, 8>;
	using Array3 = std::array<Sample, 3>;

	ParamRange dry{1}, wet{0.5};
	ParamRange roomMs{80};
	ParamRange rt20{1};
	ParamRange early{1.5};
	ParamRange detune{2};

	ParamRange lowCutHz{80}, highCutHz{12000};
	ParamRange lowDampRate{1.5}, highDampRate{2.5};

	ReverbSTFX(double maxRoomMs=200, double detuneDepthMs=2) : maxRoomMs(maxRoomMs), detuneDepthMs(detuneDepthMs) {}

	template<class Storage>
	void state(Storage &storage) {

		storage.info("Reverb", "An FDN reverb");
		int version = storage.version(5);
		if (version != 5) return;

		using stfx::units::dbToGain;
		stfx::units::rangeGain(storage.range("dry", dry)
			.info("dry", "dry signal gain")
			.range(0, 1, 4));

		stfx::units::rangeGain(storage.range("wet", wet)
			.info("wet", "reverb tail gain")
			.range(0, 1, 4));

		stfx::units::rangeMs(storage.range("roomMs", roomMs)
			.info("room", "room size (1ms ~ 1 foot)")
			.range(10, 100, maxRoomMs));

		stfx::units::rangeSec(storage.range("rt20", rt20)
			.info("decay", "RT20: decay time to -20dB")
			.range(0.01, 2, 30));

		stfx::units::rangePercent(storage.range("early", early)
			.info("early", "Early reflections")
			.range(0, 1, 2.5));
		
		storage.range("detune", detune)
			.info("detune", "Detuning rate (inside feedback loop)")
			.range(0, 5, 50)
			.unit("", 1);

		stfx::units::rangeHz(storage.range("lowCutHz", lowCutHz)
			.info("low cut", "Removes low frequencies")
			.range(10, 80, 500));
		storage.range("lowDampRate", lowDampRate)
			.info("low damp", "Reduce low frequencies over time")
			.range(1, 2, 10)
			.unit("", 1);

		stfx::units::rangeHz(storage.range("highCutHz", highCutHz)
			.info("high cut", "Removes high frequencies")
			.range(1000, 5000, 20000));
		storage.range("highDampRate", highDampRate)
			.info("high damp", "Reduce high frequencies over time")
			.range(1, 2, 10)
			.unit("", 1);
	}

	template<class Preset>
	void presets(Preset &preset) {
		if (preset("ambient")) {
			wet = 0.85;
			roomMs = 80;
			rt20 = 8.5;
			early = 0.55;
			detune = 8.5;
			lowCutHz = 50;
			lowDampRate = 1.5;
			highCutHz = 7200;
			highDampRate = 2;
		}
	}

	template<class Config>
	void configureSTFX(Config &config) {
		sampleRate = config.sampleRate;
		config.outputChannels = config.inputChannels = 2; // stereo effect only
		config.auxInputs.resize(0);
		config.auxOutputs.resize(0);

		detuneDepthSamples = detuneDepthMs*0.001*config.sampleRate;
		double maxRoomSamples = maxRoomMs*0.001*config.sampleRate;

		delay1.configure(maxRoomSamples, 0.125);
		delay2.configure(maxRoomSamples, 1);
		delay3.configure(maxRoomSamples, 0.5);
		delay4.configure(maxRoomSamples, 0.25);
		delayFeedback.configure(maxRoomSamples*1.6 + detuneDepthSamples, 1);
		delayEarly.configure(maxRoomSamples, 0.25);
	}
	
	void reset() {
		delay1.reset();
		delay2.reset();
		delay3.reset();
		delay4.reset(); // VPE: reset every diffuser so pause/mute cannot resurrect a tail.
		delayFeedback.reset();
		delayEarly.reset();
		
		for (auto &f : lowCutFilters) f.reset();
		for (auto &f : highCutFilters) f.reset();
		for (auto &f : lowDampFilters) f.reset();
		for (auto &f : highDampFilters) f.reset();
		
		detuneLfoPhase = 0;
	}
	
	int latencySamples() const {
		return 0;
	}

	int tailSamples() {
		return std::round(sampleRate*rt20*3); // decay to -60dB
	}

	template<class Io, class Config, class Block>
	void processSTFX(Io &io, Config &config, Block &block) {
		using Hadamard = signalsmith::mix::Hadamard<Sample, 8>;
		using Householder = signalsmith::mix::Householder<Sample, 8>;
		
		auto &&inputLeft = io.input[0];
		auto &&inputRight = io.input[1];
		auto &&outputLeft = io.output[0];
		auto &&outputRight = io.output[1];

		block.setupFade([&](){
			updateDelays(roomMs.to()*0.001*config.sampleRate);
		});
		bool fading = block.fading();

		auto smoothedDryGain = block.smooth(dry);
		Sample scalingFactor = stereoMixer.scalingFactor2()*0.015625; // 4 Hadamard mixes
		auto smoothedWetGain = block.smooth(wet.from(), wet.to());

		using stfx::units::dbToGain;
		double decayGainFrom = dbToGain(getDecayDb(rt20.from(), roomMs.from()));
		double decayGainTo = dbToGain(getDecayDb(rt20.to(), roomMs.to()));
		auto smoothedDecayGain = block.smooth(decayGainFrom, decayGainTo);
		auto smoothedInputGain = block.smooth( // scale according to the number of expected echoes in the first 100ms
			2*scalingFactor*std::sqrt((1 - decayGainFrom)/(1 - std::pow(decayGainFrom, 100/roomMs.from()))),
			2*scalingFactor*std::sqrt((1 - decayGainTo)/(1 - std::pow(decayGainTo, 100/roomMs.to())))
		);
		auto smoothedEarlyGain = block.smooth(early, [&](double g) {
			return g*0.35; // tuned by ear
		});

		updateFilters(decayGainTo);
		
		// Detuning LFO rate
		double detuneCentsPerLoop = detune*std::sqrt(roomMs*0.001);
		double detuneLfoRate = (detuneCentsPerLoop*0.0004)/detuneDepthSamples; // tuned by ear, assuming 3/8 channels are detuned
		
		for (int i = 0; i < block.length; ++i) {
			Sample inputGain = smoothedInputGain.at(i);
			Sample decayGain = smoothedDecayGain.at(i);
			Sample earlyGain = smoothedEarlyGain.at(i);

			std::array<Sample, 2> stereoIn = {Sample(inputLeft[i]), Sample(inputRight[i])};

			Array samples;
			std::array<Sample, 2> stereoInScaled = {stereoIn[0]*inputGain, stereoIn[1]*inputGain};
			stereoMixer.stereoToMulti(stereoInScaled, samples);
			
			double lfoCos = std::cos(detuneLfoPhase*2*M_PI), lfoSin = std::sin(detuneLfoPhase*2*M_PI);
			Array3 lfoArray = {
				Sample((0.5 + lfoCos*0.5)*detuneDepthSamples),
				Sample((0.5 + lfoCos*-0.25 + lfoSin*0.43301270189)*detuneDepthSamples),
				Sample((0.5 + lfoCos*-0.25 + lfoSin*-0.43301270189)*detuneDepthSamples)
			};
			detuneLfoPhase += detuneLfoRate;
			
			if (fading) {
				Sample fade = block.fade(i);
				samples = delay1.write(samples).read(fade);
				Hadamard::unscaledInPlace(samples);
				samples = delay2.write(samples).read(fade);
				Hadamard::unscaledInPlace(samples);

				Array feedback = delayFeedback.readDetuned(lfoArray, fade);
				Householder::inPlace(feedback);
				for (int c = 0; c < 8; ++c) {
					feedback[c] = highDampFilters[c](lowDampFilters[c](feedback[c]));
				}
				Array feedbackInput;
				for (int c = 0; c < 8; ++c) {
					int c2 = (c + 3)&7;
					feedbackInput[c2] = samples[c] + feedback[c]*decayGain;
				}
				delayFeedback.write(feedbackInput);

				Array earlyReflections = delayEarly.write(samples).read(fade);
				Hadamard::unscaledInPlace(earlyReflections);
				for (int c = 0; c < 8; ++c) samples[c] = feedback[c] + earlyReflections[c]*earlyGain;

				samples = delay3.write(samples).read(fade);
				Hadamard::unscaledInPlace(samples);
				samples = delay4.write(samples).read(fade);
			} else {
				samples = delay1.write(samples).read();
				Hadamard::unscaledInPlace(samples);
				samples = delay2.write(samples).read();
				Hadamard::unscaledInPlace(samples);

				Array feedback = delayFeedback.readDetuned(lfoArray);
				Householder::inPlace(feedback);
				for (int c = 0; c < 8; ++c) {
					feedback[c] = highDampFilters[c](lowDampFilters[c](feedback[c]));
				}
				Array feedbackInput;
				for (int c = 0; c < 8; ++c) {
					int c2 = (c + 3)&7;
					feedbackInput[c2] = samples[c] + feedback[c]*decayGain;
				}
				delayFeedback.write(feedbackInput);

				Array earlyReflections = delayEarly.write(samples).read();
				Hadamard::unscaledInPlace(earlyReflections);
				for (int c = 0; c < 8; ++c) samples[c] = feedback[c] + earlyReflections[c]*earlyGain;

				samples = delay3.write(samples).read();
				Hadamard::unscaledInPlace(samples);
				samples = delay4.write(samples).read();
			}

			std::array<Sample, 2> stereoOut;
			stereoMixer.multiToStereo(samples, stereoOut);
			
			for (int c = 0; c < 2; ++c) {
				stereoOut[c] = highCutFilters[c](lowCutFilters[c](stereoOut[c]));
			}

			Sample dryGain = smoothedDryGain.at(i);
			Sample wetGain = smoothedWetGain.at(i);
			outputLeft[i] = stereoIn[0]*dryGain + stereoOut[0]*wetGain;
			outputRight[i] = stereoIn[1]*dryGain + stereoOut[1]*wetGain;
		}
		
		detuneLfoPhase -= std::floor(detuneLfoPhase);
	}

private:
	int channels = 0;
	double sampleRate = 1;
	double maxRoomMs, detuneDepthMs;
	double detuneLfoPhase = 0;
	double detuneDepthSamples = 0;
	
	using Filter = signalsmith::filters::BiquadStatic<Sample>;
	std::array<Filter, 2> lowCutFilters, highCutFilters;
	std::array<Filter, 8> lowDampFilters, highDampFilters;
	
	void updateFilters(double feedbackGain) {
		for (auto &f : lowCutFilters) f.highpassQ(lowCutHz/sampleRate, 0.5);
		for (auto &f : highCutFilters) f.lowpassQ(highCutHz/sampleRate, 0.5);

		Sample lowDampHz = lowCutHz + 100;
		Sample highDampHz = highCutHz*0.5;
		Sample lowDampGain = std::max(std::pow(feedbackGain, lowDampRate), 1e-3);
		Sample highDampGain = std::max(std::pow(feedbackGain, highDampRate), 1e-3);
		for (auto &f : lowDampFilters) f.lowShelfQ(lowDampHz/sampleRate, lowDampGain, 0.5);
		for (auto &f : highDampFilters) f.highShelf(highDampHz/sampleRate, highDampGain);
	}
	
	static Sample getDecayDb(Sample rt20, Sample loopMs) {
		Sample dbPerSecond = -20/rt20;
		Sample secondsPerLoop = loopMs*Sample(0.001);
		return dbPerSecond*secondsPerLoop;
	}
	
	signalsmith::mix::StereoMultiMixer<Sample, 8> stereoMixer;
	
	struct MultiDelay {
		signalsmith::delay::MultiBuffer<Sample> buffer;
		double delayScale = 1;
		std::array<int, 8> delayOffsets, delayOffsetsPrev;
		
		void configure(double maxRangeSamples, double scale) {
			delayScale = scale;
			buffer.resize(8, std::ceil(maxRangeSamples*delayScale) + 1);
		}
		
		void reset() {
			buffer.reset();
		}
		
		void updateLengths(int seed, double rangeSamples, bool minimise=true) {
			rangeSamples *= delayScale;
			delayOffsetsPrev = delayOffsets;
			std::mt19937 engine(seed);
			std::uniform_real_distribution<float> unitDist(0, 1);
			for (int i = 0; i < 8; ++i) {
				float unit = unitDist(engine);
				delayOffsets[i] = int(-std::floor(rangeSamples*(unit + i)/8));
				std::uniform_int_distribution<int> indexDist(0, i);
				int swapIndex = indexDist(engine);
				std::swap(delayOffsets[i], delayOffsets[swapIndex]);
			}
			if (minimise) { // Moves things along so the shortest delay is always 0
				int maximumDelay = delayOffsets[0];
				for (auto &d : delayOffsets) maximumDelay = std::max(d, maximumDelay);
				for (auto &d : delayOffsets) d -= maximumDelay;
			}
		}
		
		void updateLengthsExponential(double rangeSamples) {
			rangeSamples *= delayScale;
			delayOffsetsPrev = delayOffsets;
			constexpr double ratios[8] = {0.0625, -0.0625, 0.1875, -0.1875, 0.3125, -0.3125, 0.4375, -0.4375};
			for (int i = 0; i < 8; ++i) {
				delayOffsets[i] = int(-std::floor(rangeSamples*std::pow(2, ratios[i])));
			}
		}
		
		MultiDelay & write(const Array &arr) {
			++buffer;
			for (int i = 0; i < 8; ++i) {
				buffer[i][0] = arr[i];
			}
			return *this;
		}

		Array read() {
			Array result;
			for (int i = 0; i < 8; ++i) {
				result[i] = buffer[i][delayOffsets[i]];
			}
			return result;
		}
		Array read(Sample fade) {
			Array result;
			for (int i = 0; i < 8; ++i) {
				Sample to = buffer[i][delayOffsets[i]];
				Sample from = buffer[i][delayOffsetsPrev[i]];
				result[i] = from + (to - from)*fade;
			}
			return result;
		}
		
		signalsmith::delay::Reader<Sample, signalsmith::delay::InterpolatorLagrange7> fractionalReader;
		Array readDetuned(Array3 lfoDepths) {
			Array result;
			for (int i = 0; i < 3; ++i) {
				result[i] = fractionalReader.read(buffer[i], lfoDepths[i] - delayOffsets[i]);
			}
			for (int i = 3; i < 8; ++i) {
				result[i] = buffer[i][delayOffsets[i]];
			}
			return result;
		}
		Array readDetuned(Array3 lfoDepths, Sample fade) {
			Array result;
			for (int i = 0; i < 3; ++i) {
				Sample to = fractionalReader.read(buffer[i], lfoDepths[i] - delayOffsets[i]);
				Sample from = fractionalReader.read(buffer[i], lfoDepths[i] - delayOffsetsPrev[i]);
				result[i] = from + (to - from)*fade;
			}
			for (int i = 3; i < 8; ++i) {
				Sample to = buffer[i][delayOffsets[i]];
				Sample from = buffer[i][delayOffsetsPrev[i]];
				result[i] = from + (to - from)*fade;
			}
			return result;
		}
	};
	
	MultiDelay delay1, delay2, delay3, delay4, delayFeedback, delayEarly;
	void updateDelays(double roomSamples) {
		delay1.updateLengths(0x6DD09EE5, roomSamples, false);
		delay2.updateLengths(0x876753A5, roomSamples);
		delay3.updateLengths(0x5974DF44, roomSamples);
		delay4.updateLengths(0x8CDBF7E6, roomSamples);
		delayFeedback.updateLengthsExponential(roomSamples);
		delayEarly.updateLengths(0x0BDDE171, roomSamples);
	}
};

}} // namespace
