/* Copyright 2022+ Signalsmith Audio Ltd. / Geraint Luff
MIT License - see LICENSE.txt and SUPPORT.txt */
#pragma once

#include "signalsmith-dsp/delay.h"
#include "signalsmith-dsp/envelopes.h"
SIGNALSMITH_DSP_VERSION_CHECK(1, 1, 0)

#include "../stfx/stfx-library.h"

#include <cmath>

namespace signalsmith { namespace basics {

template<class BaseEffect>
class LimiterSTFX;

using LimiterFloat = stfx::LibraryEffect<float, LimiterSTFX>;
using LimiterDouble = stfx::LibraryEffect<double, LimiterSTFX>;

template<class BaseEffect>
struct LimiterSTFX : public BaseEffect {
	using typename BaseEffect::Sample;
	using typename BaseEffect::ParamRange;
	using typename BaseEffect::ParamStepped;

	ParamRange inputGain{1};
	ParamRange outputLimit{stfx::units::dbToGain(-3)};
	ParamRange attackMs{20}, holdMs{0}, releaseMs{0};

	ParamStepped smoothingStages{1};
	ParamRange linkChannels{0.5};
	
	LimiterSTFX(double maxDelayMs=100) : maxDelayMs(maxDelayMs) {}

	template<class Storage>
	void state(Storage &storage) {
		storage.info("Limiter", "A simple lookahead limiter");
		int version = storage.version(4);
		if (version != 4) return;

		using stfx::units::dbToGain;
		stfx::units::rangeGain(storage.range("inputGain", inputGain)
			.info("pre-gain", "amplifies the input before limiting")
			.range(dbToGain(-12), 1, dbToGain(24)));
		stfx::units::rangeGain(storage.range("outputLimit", outputLimit)
			.info("limit", "maximum output amplitude")
			.range(dbToGain(-24), dbToGain(-12), 1));
		stfx::units::rangeMs(storage.range("attackMs", attackMs)
			.info("attack", "envelope smoothing time")
			.range(1, 10, maxDelayMs/2));
		stfx::units::rangeMs(storage.range("holdMs", holdMs)
			.info("hold", "hold constant after peaks")
			.range(0, 10, maxDelayMs/2));
		stfx::units::rangeMs(storage.range("releaseMs", releaseMs)
			.info("release", "extra release time (in addition to attack + hold)")
			.range(0, 10, 250));

		storage.stepped("smoothingStages", smoothingStages)
			.info("smoothing", "smoothing filter(s) used for attack-smoothing")
			.label(1, "rect", "double");
		stfx::units::rangePercent(storage.range("linkChannels", linkChannels)
			.info("link", "link channel gains together")
			.range(0, 0.5, 1));
	}
	
	// Gain envelopes are calculated per-channel
	struct ChannelEnvelope {
		signalsmith::envelopes::PeakHold<Sample> peakHold{0};
		signalsmith::envelopes::BoxFilter<Sample> smoother1{1}, smoother2{1}; // VPE: positive initial length; configure() supplies the real size.
		Sample released = 1;
		Sample releaseSlew = 1;
		
		void reset(Sample value=1) {
			peakHold.reset(-value);
			smoother1.reset(value);
			smoother2.reset(value);
			released = value;
		}
		
		void configure(int maxDelaySamples) {
			peakHold.resize(maxDelaySamples);
			smoother1.resize(maxDelaySamples);
			smoother2.resize(maxDelaySamples/2 + 1);
		}
		
		Sample operator ()(Sample maxGain) {
			// Moving minimum
			Sample gain = -peakHold(-maxGain);
			// Exponential release curve
			released += (gain - released)*releaseSlew;
			released = std::min(gain, released);
			// Smoothing (attack)
			return smoother1(smoother2(released));
		}
	};
	std::vector<ChannelEnvelope> channelEnvelopes;
	
	Sample sampleRate;
	std::vector<Sample> channelGains;
	template<class Config>
	void configureSTFX(Config &config) {
		channels = config.outputChannels = config.inputChannels;
		config.auxInputs.resize(0);
		config.auxOutputs.resize(0);
		sampleRate = config.sampleRate;
		
		int maxDelaySamples = std::ceil(maxDelayMs*0.001*sampleRate);
		multiBuffer.resize(int(channels), maxDelaySamples + 1);
		channelEnvelopes.resize(channels);
		for (auto &e : channelEnvelopes) e.configure(maxDelaySamples);
		channelGains.resize(channels);
	}
	
	void reset() {
		multiBuffer.reset();
		for (auto &e : channelEnvelopes) e.reset();
		sqrtWet = (smoothingStages > 1);
	}
	
	int latencySamples() const {
		int attackSamples = std::ceil(attackMs*0.001*sampleRate);
		return attackSamples;
	}
	
	template <class Io, class Config, class Block>
	void processSTFX(Io &io, Config &, Block &block) {
		Sample thresholdAmp = outputLimit;
		auto smoothedPreGain = block.smooth(inputGain);
		
		// If we change the attack, we want to fade between the two delay times
		int delaySamplesFrom = std::ceil(attackMs.from()*0.001*sampleRate);
		int delaySamplesTo = std::ceil(attackMs.to()*0.001*sampleRate);

		int attackSamples = delaySamplesTo;
		int holdSamples = std::ceil(holdMs*0.001*sampleRate);
		Sample releaseSamples = releaseMs*0.001*sampleRate*smoothingStages;
		int stages = smoothingStages;
		sqrtWetSlew = 1/(Sample(attackSamples)*2 + 1);

		for (auto &envelope : channelEnvelopes) {
			envelope.peakHold.set(attackSamples + holdSamples);
			if (stages == 2) {
				// Split into two (non-equal) box filters
				int split = std::round(attackSamples*0.5822419);
				envelope.smoother1.set(split + 1);
				envelope.smoother2.set(attackSamples - split + 1);
			} else {
				envelope.smoother1.set(attackSamples + 1);
				envelope.smoother2.set(1);
			}
			// Reasonable approximation for release curve: https://www.desmos.com/calculator/wbxakdgw1o
			constexpr Sample ln2{0.69314718056};
			envelope.releaseSlew = ln2/(releaseSamples + ln2);
		}

		for (int i = 0; i < int(block.length); ++i) {
			Sample minChannelGain = 1;
			for (size_t c = 0; c < channels; ++c) {
				Sample value = io.input[c][i]*smoothedPreGain.at(i);
				multiBuffer[int(c)][i] = value;
				
				// maximum gain (clips output to threshold)
				Sample gain = thresholdAmp/std::max(thresholdAmp, std::abs(value));
				channelGains[c] = gain;
				minChannelGain = std::min(minChannelGain, gain);
			}
			for (size_t c = 0; c < channels; ++c) {
				Sample gain = channelGains[c];
				// blend between individual/minimum gain
				gain += (minChannelGain - gain)*linkChannels;
				if (stages == 2) {
					gain *= gain;
					sqrtWet += (1 - sqrtWet)*sqrtWetSlew;
				} else {
					sqrtWet *= 1 - sqrtWetSlew;
				}
				// smooth envelope gain
				auto &envelope = channelEnvelopes[c];
				gain = envelope(gain);
				if (sqrtWet > Sample(1e-30)) {
					gain += (std::sqrt(gain) - gain)*sqrtWet;
					gain = std::min(gain, channelGains[c]);
				}

				Sample delayed = block.fade(i,
					multiBuffer[int(c)][i - delaySamplesFrom],
					multiBuffer[int(c)][i - delaySamplesTo]
				);
				io.output[c][i] = delayed*gain;
			}
		}
		multiBuffer += int(block.length);
	}

private:
	size_t channels = 0;
	double maxDelayMs = 0;
	Sample sqrtWet = 0, sqrtWetSlew = 1;
	signalsmith::delay::MultiBuffer<Sample> multiBuffer;
};

}} // namespace
