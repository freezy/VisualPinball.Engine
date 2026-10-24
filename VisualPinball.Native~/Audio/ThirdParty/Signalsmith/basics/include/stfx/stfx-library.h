/*	Signalsmith's Templated FX

	Copyright 2021-2022 Geraint Luff / Signalsmith Audio Ltd
	Released under the Boost Software License (see LICENSE.txt)

	The main thing you need is `stfx::LibraryEffect<Sample, EffectTemplate>`
	which produces a simple effect class from an STFX effect template.
*/

#ifndef STFX_LIBRARY_H
#define STFX_LIBRARY_H

#include <vector>
#include <string>
#include <cmath>
#include <atomic>

#if defined(__SSE__) || defined(_M_X64)
#	define STFX_USE_XMMINTRIN_STOPDENORMALS
#	include <xmmintrin.h>
#endif

namespace stfx {
	// Convenient units for range parameters - not really part of the main STFX API
	namespace units {
		static inline double dbToGain(double db) {
			return std::pow(10, db*0.05);
		}
		static inline double gainToDb(double gain) {
			return std::log10(std::max<double>(gain, 1e-10))*20;
		}
		static inline double dbToEnergy(double db) {
			return std::pow(10, db*0.1);
		}
		static inline double energyToDb(double gain) {
			return std::log10(std::max<double>(gain, 1e-10))*10;
		}
		static inline double pcToRatio(double percent) {
			return percent*0.01;
		}
		static inline double ratioToPc(double linear) {
			return linear*100;
		}
		static inline double kHzToHz(double kHz) {
			return kHz*1000;
		}
		static inline double hzToKHz(double hz) {
			return hz*0.001;
		}
		static inline double sToMs(double sec) {
			return sec*1000;
		}
		static inline double msToS(double ms) {
			return ms*0.001;
		}

		// Templates to add commonly-used units to range parameters
		template<class RangeParam>
		RangeParam & rangeGain(RangeParam &param) {
			return param
				.unit("dB", 1, dbToGain, gainToDb) // default display is dB
				.unit("%", 0, pcToRatio, ratioToPc)
				.exact(0, "off")
				.unit("x"); // Allow things like "2x" (~6dB) for text-input
		}
		template<class RangeParam>
		RangeParam & rangeHz(RangeParam &param) {
			return param
				.unit("Hz", 2, -1, 1)
				.unit("Hz", 1, -10, 10)
				.unit("Hz", 0, -1000, 1000)
				.unit("kHz", 2, kHzToHz, hzToKHz, -1e4, 1e4)
				.unit("kHz", 1, kHzToHz, hzToKHz);
		}
		template<class RangeParam>
		RangeParam & rangePercent(RangeParam &param) {
			return param
				.unit("%", 0, pcToRatio, ratioToPc)
				.unit("x");
		}
		template<class RangeParam>
		RangeParam & rangeMs(RangeParam &param) {
			return param
				.unit("ms", 2, -1, 1)
				.unit("ms", 1, -10, 10)
				.unit("ms", 0, -1000, 1000)
				.unit(" sec", 2, sToMs, msToS, -1e3, 1e3)
				.unit(" sec", 1, sToMs, msToS, -1e4, 1e4)
				.unit(" sec", 0, sToMs, msToS);
		}
		template<class RangeParam>
		RangeParam & rangeSec(RangeParam &param) {
			return param
				.unit("ms", 2, msToS, sToMs, -1e-3, 1e-3)
				.unit("ms", 1, msToS, sToMs, -1e-2, 1e-2)
				.unit("ms", 0, msToS, sToMs, -1e-1, 1e-1)
				.unit(" sec", 2, -1, 1)
				.unit(" sec", 1, -10, 10)
				.unit(" sec", 0);
		}
	}

	// When we want to ignore parameter info (covers both range and stepped)
	struct RangeParamIgnore {
		RangeParamIgnore & info(const char*, const char*) {
			return *this;
		}
		RangeParamIgnore & range(double low, double high) {
			return *this;
		}
		RangeParamIgnore & range(double low, double mid, double high) {
			return *this;
		}
		template<class V2C, class C2V>
		RangeParamIgnore & range(double pLow, double pHigh, V2C &&valueToControl, C2V &&controlToValue) {
			return *this;
		}
		/* Always takes a suffix.  Units registered first are preferred when formatting values
		Optional arguments (any can be omitted if the order is correct):
			precision:
				int: decimalDigits
			display mapping (e.g. for value in Hz, display in kHz):
				double displayToValue(double)
				double valueToDisplay(double)
			valid range:
				double low
				double high
		*/
		template<class ...Args>
		RangeParamIgnore & unit(const char *suffix, Args...) {
			return *this;
		}
		RangeParamIgnore & exact(double, const char *) {
			return *this;
		}
	};
	struct SteppedParamIgnore {
		SteppedParamIgnore & info(const char*, const char*) {
			return *this;
		}
		SteppedParamIgnore & range(int low, int high) {
			return *this;
		}
		template<typename ...Args>
		SteppedParamIgnore & label(Args ...) {
			return *this;
		}
	};
	namespace {
		// A value which changes linearly within a given index range (e.g. a block)
		class LinearSegment {
			double offset, gradient;
		public:
			LinearSegment(double offset, double gradient) : offset(offset), gradient(gradient) {}

			double at(double i) {
				return offset + i*gradient;
			}
			bool changing() {
				return gradient != 0;
			}
		};

		/** A processing block (not including IO).

		There are two fades happening within each block:
			* automation: changes are smoothed linearly across the block
			* A/B fade: a slower transition for parameters which need to change slowly or do cross-fades
		*/
		class Block {
			// slow fade start/rate
			double fadeStart, fadeStep;
			// per-block automation gradient rate
			double blockFade;
			// we might need some additional setup on the fiand/or after a directly
			bool firstBlockAfterReset;
			bool metersRequested;
			bool &metersChecked;

			template<class Param>
			bool paramsChanging(Param &param) const {
				return param.from() != param.to();
			}
 			template<class Param, class ...Others>
			bool paramsChanging(Param &param, Others &...others) const {
				return paramsChanging(param) || paramsChanging(others...);
			}
		public:
			// Block length in samples
			const size_t length;
			
			Block(size_t length, double fadeStart, double fadeStep, bool firstBlockAfterReset, bool wantsMeters, bool &metersChecked) : fadeStart(fadeStart), fadeStep(fadeStep), blockFade(1.0/length), firstBlockAfterReset(firstBlockAfterReset), metersRequested(wantsMeters), metersChecked(metersChecked), length(length) {}
			// Not copyable, because that's probably a mistake
			Block(const Block &) = delete;
			Block & operator =(const Block&) = delete;

			/// Fade ratio at a given sample index
			double fade(double i) const {
				return fadeStart + i*fadeStep;
			}
			/// Mix two values according to the fade ratio
			template<class Value=double>
			Value fade(double i, Value from, Value to) const {
				return from + (to - from)*fade(i);
			}

			/// Is there a fade currently active?
			bool fading() const {
				return fadeStep != 0;
			}
			/// Is a fade happening, and are any of the parameters included in it?
			template<class Param, class ...Others>
			bool fading(Param &param, Others &...others) const {
				return fading() && paramsChanging(param, others...);
			}

			/// Set up an A/B fade.  Executes once at the beginning of a fade, and also once directly after a `.reset()`.
			template<class Fn>
			void setupFade(Fn fn) const {
				setupFade(fading(), fn);
			}
			/// Same as above, but only run on fade-start if `fading` is true.  Mostly useful when used with `.fading(params...)`
			template<class Fn>
			void setupFade(bool fading, Fn fn) const {
				if (firstBlockAfterReset) fn();
				if (fading && fadeStart == 0) fn();
			}

			/// Produce a linear segment corresponding to A/B fading a parameter
			template<class Param>
			LinearSegment smooth(Param &param) const {
				return smooth(param.from(), param.to());
			}
			/// Same as above, but the parameter's values can be mapped
			template<class Param, class Map>
			LinearSegment smooth(Param &param, Map map) const {
				return smooth(map(param.from()), map(param.to()));
			}
			/// Produce a linear segment corresponding to A/B fading two values.
			/// These values should _not_ generally change every block, but only when a new fade starts.  They should probably be derived from `param.from()` and `param.to()`, and this method is mostly useful for combining multiple parameters.
			LinearSegment smooth(double from, double to) const {
				double diff = to - from;
				return {from + diff*fadeStart, diff*fadeStep};
			}

			/// Automation provides a curve for a parameter, and is also an event list where the events update the curve.
			/// This pattern allows sample-accurate automation in environments where that's supported.
			struct BlockAutomation : public LinearSegment {
				BlockAutomation(const LinearSegment &smoothed) : LinearSegment(smoothed) {}

				// For this implementation, we just provide a linear segment and no update events.
				static constexpr size_t size() {
					return 0;
				}
				struct DoNothingEvent {
					size_t offset = 0;
					void operator()() {}
				};
				DoNothingEvent operator [](size_t) {
					return DoNothingEvent();
				}
			};
			/// Get an automation curve for a parameter
			template<class Param>
			BlockAutomation automation(Param &param) const {
				double start = param._libPrevBlock();
				auto diff = param._libCurrent() - start;
				return LinearSegment{start, diff*blockFade};
			}
			
			/// Blocks can be processed in sub-blocks, which are split up by events.
			/// This method may return a different sub-block type (which will also have `.split()` and `.forEach()` methods).
			template<class EventList>
			const Block & split(EventList &&list, size_t count) const {
				for (size_t i = 0; i < count; ++i) list[i]();
				return *this;
			}
			template<class EventList, class ...Others>
			const Block & split(EventList &&list, Others &&...others) const {
				return split(list, list.size()).split(std::forward<Others>(others)...);
			}
			/// Base-case for templated recursion
			const Block & split() const {
				return *this;
			}
			/// Execute the callback once per sub-block, with sample-index arguments: `callback(int start, int end)`, calling events in between
			template<class Callback>
			void forEach(Callback callback) const {
				callback(0, length);
			}
			
			bool wantsMeters() const {
				metersChecked = true;
				return metersRequested;
			}
		};
		
		// Parameters can be assigned using `=`, and store their own history for transitions
		template<typename Value>
		class LibraryParam {
			std::atomic<Value> current;
			Value prevBlock; // inter-block fade
			// Used for the 20ms parameter fade
			Value _from, _to;
		public:
			LibraryParam(const Value &initial, const Value &) : LibraryParam(initial) {}
			LibraryParam(const Value &initial) : current(initial), prevBlock(initial), _from(initial), _to(initial) {}
			operator Value () const {
				return current.load(std::memory_order_relaxed);
			}
			LibraryParam & operator =(const Value &v) {
				current.store(v, std::memory_order_relaxed);
				return *this;
			}
			// Return the current fade
			Value from() const {
				return _from;
			}
			Value to() const {
				return _to;
			}
			
			template<class Storage>
			void state(Storage &storage) {
				auto v = current.load(std::memory_order_relaxed);
				auto vPrev = v;
				storage("value", v);
				if (v != vPrev) current.store(v, std::memory_order_relaxed);
			}
			
			// The following are only called from `.process()`
			// Shuffle the internal values along to start a new fade, return whether it's actually changing
			bool _libStartFade() {
				_from = _to;
				_to = current.load(std::memory_order_relaxed);
				return (_to != _from);
			}
			// Store previous value for block-level automation
			void _libEndBlock() {
				prevBlock = current.load(std::memory_order_relaxed);
			}
			Value _libCurrent() {
				return current.load(std::memory_order_relaxed);
			}
			Value _libPrevBlock() {
				return prevBlock;
			}
		};
	}

#if defined(STFX_USE_XMMINTRIN_STOPDENORMALS)
		class StopDenormals {
			unsigned int controlStatusRegister;
		public:
			StopDenormals() : controlStatusRegister(_mm_getcsr()) {
				_mm_setcsr(controlStatusRegister|0x8040); // Flush-to-Zero and Denormals-Are-Zero
			}
			~StopDenormals() {
				_mm_setcsr(controlStatusRegister);
			}
		};
#elif (defined (__ARM_NEON) || defined (__ARM_NEON__))
		class StopDenormals {
			uintptr_t status;
		public:
			StopDenormals() {
				uintptr_t asmStatus;
				asm volatile("mrs %0, fpcr" : "=r"(asmStatus));
				status = asmStatus = asmStatus|0x01000000U; // Flush to Zero
				asm volatile("msr fpcr, %0" : : "ri"(asmStatus));
			}
			~StopDenormals() {
				uintptr_t asmStatus = status;
				asm volatile("msr fpcr, %0" : : "ri"(asmStatus));
			}
		};
#else
#	if __cplusplus >= 202302L
# 		warning "The `StopDenormals` class doesn't do anything for this architecture"
#	endif
		class StopDenormals {}; // FIXME: add for other architectures
#endif

	/// Base class for our effect to inherit from.   Provides parameter classes and some default config.
	template<typename SampleType>
	class LibraryEffectBase {
	public:
		using ParamRange = LibraryParam<double>;
		using ParamStepped = LibraryParam<int>;
		using Sample = SampleType;
		
		ParamRange bpm{120};
		
		double paramFadeMs() {
			return 20;
		}
		int latencySamples() {
			return 0;
		}
		int tailSamples() {
			return 0;
		}

		size_t polyphony() {
			return 0;
		}
		// Call these note-event methods before `.process()` to load up events for a block
		// Assume 69 = 440Hz. if you want a different tuning base, then add an appropriate offset.
		bool noteStart(size_t blockOffset, size_t noteId, Sample pitch, Sample velocity) {
			return false;
		}
		bool noteStop(size_t blockOffset, size_t noteId, Sample velocity=0) {
			return false;
		}
		// Is the current note ID still active?
		bool noteCheck(size_t noteId) {
			return false;
		}
		// This is an updated MIDI note, *not* pitch-bend
		bool notePitch(size_t blockOffset, size_t noteId, Sample pitch) {
			return false;
		}
		// use MIDI-CC numbers for note expression
		// volume=7, pan=10, vibrato=21, expression=11, brightness=74, pressure=2
		bool noteCC(size_t blockOffset, size_t noteId, size_t midiCC, Sample value) {
			return false;
		}
		
		template<class Presets>
		void presets(Presets &) {}

		// passes ownership of any meter values back to the audio thread
		void wantsMeters(bool meters=true) {
			metersReady = false;
			metersRequested = meters;
		}
		// whether the meter values can be read
		bool hasMeters() const {
			return metersReady;
		}
	protected:
		std::atomic<bool> metersRequested = false, metersReady = false;
	};

	struct LibraryConfig {
		double sampleRate = 48000;
		size_t inputChannels = 2, outputChannels = 2;
		std::vector<size_t> auxInputs, auxOutputs;
		size_t maxBlockSize = 256;
		
		bool operator ==(const LibraryConfig &other) const {
			return sampleRate == other.sampleRate
				&& inputChannels == other.inputChannels
				&& outputChannels == other.outputChannels
				&& auxInputs == other.auxInputs
				&& auxOutputs == other.auxOutputs
				&& maxBlockSize == other.maxBlockSize;
		}
	};

	/// Creates an effect class from an effect template, with optional extra config.
	/// The effect template takes `EffectTemplate<BaseClass, ...ExtraConfig>`
	template<typename Sample, template <class, class...> class EffectTemplate, class ...ExtraConfig>
	class LibraryEffect : public EffectTemplate<stfx::LibraryEffectBase<Sample>, ExtraConfig...> {
		using EffectClass = EffectTemplate<stfx::LibraryEffectBase<Sample>, ExtraConfig...>;
		
		// This is passed to the effect's `.state()` method during initialisation, and collects pointers to the effect's parameters
		class CollectParams {
		public:
			std::vector<LibraryParam<double> *> rangeParams;
			std::vector<LibraryParam<int> *> stepParams;

			// Add registered parameters to the list
			RangeParamIgnore range(const char *, LibraryParam<double> &param, const char *codeExpr=nullptr) {
				(void)codeExpr;
				rangeParams.push_back(&param);
				return {};
			}
			SteppedParamIgnore stepped(const char *, LibraryParam<int> &param, const char *codeExpr=nullptr) {
				(void)codeExpr;
				stepParams.push_back(&param);
				return {};
			}

			// Drop any name/description we're given
			template<class ...Args>
			void info(Args...) {}
			// The effect might ask us to store/fetch the serialisation version, we just echo it back
			int version(int v) {return v;}
			// Ignore the UI/synchronisation stuff
			bool extra() {return false;}
			template<class V>
			void extra(const char *, V &&) {}
			void invalidate(const char *) {}
			// This storage only reads values, never changes them
			template<class T>
			bool changed(const char *, T &v) {return false;}

			// We ignore any basic type
			void operator()(const char *, bool) {}
			void operator()(const char *, int) {}
			void operator()(const char *, long) {}
			void operator()(const char *, double) {}
			void operator()(const char *, float) {}
			// And strings
			void operator()(const char *, std::string &) {}
			
			// Iterate through vectors
			template<class Item>
			void operator()(const char *label, std::vector<Item> &vector) {
				for (auto &item : vector) (*this)(label, item);
			}
			
			// Assume all other arguments have a `.state()`, and recurse into it
			template<class OtherObject>
			void operator()(const char *, OtherObject &obj) {
				obj.state(*this);
			}
		} params;
		
		bool justHadReset = true;
		// Keep track of the A/B fade state
		double fadeRatio = 0;
		
		std::vector<size_t> auxInputOffsets, auxOutputOffsets;
		template<class Main>
		class Aux {
			Main &main;
			const size_t *offsets;
		public:
			Aux(Main &main, const size_t *offsets) : main(main), offsets(offsets) {}
			
			class Bus {
				Main &main;
				size_t offset;
			public:
				Bus(Main &main, size_t offset) : main(main), offset(offset) {}
				
				auto operator[](size_t c) -> decltype(this->main[c + this->offset]) {
					return main[c + offset];
				}
			};
			
			Bus operator[](int c) const {
				return Bus{main, offsets[c]};
			}
		};
	public:
		template<class ...Args>
		LibraryEffect(Args &&...args) : EffectClass(std::forward<Args>(args)...) {
			params.rangeParams.push_back(&this->bpm);
			EffectClass::state(params);
		}

		using Config = LibraryConfig;
		/// The current (proposed) effect configuration
		Config config;
		/// Returns `true` if the current `.config` was accepted.  Otherwise, you can check how `.config` was modified, make your own adjustments (if needed) and try again.
		bool configure() {
			Config prevConfig = config;
			EffectClass::configureSTFX(config);
			if (config == prevConfig) {
				reset();
				auxInputOffsets.resize(0);
				size_t offset = config.inputChannels;
				for (auto &aux : config.auxInputs) {
					auxInputOffsets.push_back(offset);
					offset += aux;
				}
				offset = config.outputChannels;
				for (auto &aux : config.auxOutputs) {
					auxOutputOffsets.push_back(offset);
					offset += aux;
				}
				return true;
			}
			return false;
		}
		/// Attempts to find a valid configuration by iteration
		bool configurePersistent(size_t attempts=10) {
			for (size_t i = 0; i < attempts; ++i) {
				if (configure()) return true;
			}
			return false;
		}
		/// Returns true if the effect was successfully configured with _exactly_ these parameters
		bool configure(double sampleRate, size_t maxBlockSize, size_t channels=2) {
			return configure(sampleRate, maxBlockSize, channels, channels);
		}
		bool configure(double sampleRate, size_t maxBlockSize, size_t channels, size_t outputChannels, size_t auxInChannels=0, size_t auxOutChannels=0) {
			config.sampleRate = sampleRate;
			config.inputChannels = channels;
			config.outputChannels = outputChannels;
			config.maxBlockSize = maxBlockSize;

			config.auxInputs.resize(0);
			if (auxInChannels > 0) config.auxInputs.push_back(auxInChannels);
			config.auxOutputs.resize(0);
			if (auxOutChannels > 0) config.auxOutputs.push_back(auxOutChannels);

			return configure();
		}
		
		/// Clears effect buffers and resets parameters
		void reset() {
			for (auto param : params.rangeParams) {
				param->_libStartFade();
				param->_libEndBlock();
			}
			for (auto param : params.stepParams) {
				param->_libStartFade();
				param->_libEndBlock();
			}
			EffectClass::reset();
			justHadReset = true;
		}

		template<class Buffers>
		void process(Buffers &&buffers, size_t blockLength) {
			process(buffers, buffers, blockLength);
		}

		/// Wraps the common `process(float** inputs, float** outputs, size_t length)` call into the `.processSTFX(io, config, block)`.
		/// It actually accepts any objects which support `inputs[channel][index]`, so you could write adapters for interleaved buffers etc.
		template<class Inputs, class Outputs>
		void process(Inputs &&inputs, Outputs &&outputs, size_t blockLength) {
			StopDenormals scoped;
			// How long should the parameter fade take?
			double fadeSamples = EffectClass::paramFadeMs()*0.001*config.sampleRate;
			// Fade position at the end of the block
			double fadeRatioEnd = fadeRatio + blockLength/fadeSamples;
			// If the fade will finish this block, get there exactly
			double fadeRatioStep = (fadeRatioEnd >= 1) ? (1 - fadeRatio)/blockLength : 1/fadeSamples;

			// If we're just starting a new fade, move all the parameter values along
			if (fadeRatio == 0) {
				bool needsFade = false;
				for (auto param : params.rangeParams) {
					if (param->_libStartFade()) needsFade = true;
				}
				for (auto param : params.stepParams) {
					if (param->_libStartFade()) needsFade = true;
				}
				// None of our parameters are actually fading, just skip it
				if (!needsFade) fadeRatioStep = fadeRatioEnd = 0;
			}

			struct Io {
				Inputs input;
				Outputs output;
				Aux<Inputs> auxInput;
				Aux<Outputs> auxOutput;
			};
			Io io{inputs, outputs, {inputs, auxInputOffsets.data()}, {outputs, auxOutputOffsets.data()}};
			bool metersChecked = false;
			Block block(blockLength, fadeRatio, fadeRatioStep, justHadReset, this->metersRequested, metersChecked);
			
			((EffectClass *)this)->processSTFX(io, (const Config &)config, (const Block &)block);

			if (fadeRatioEnd >= 1) {
				// Fade just finished, so we reset
				fadeRatio = 0;
			} else {
				fadeRatio = fadeRatioEnd;
			}
			justHadReset = false;
			for (auto param : params.rangeParams) param->_libEndBlock();
			for (auto param : params.stepParams) param->_libEndBlock();
			
			// Meters are filled - pass ownership of meter values to the main thread
			if (this->metersRequested && metersChecked) {
				this->metersRequested = false;
				this->metersReady = true;
			}
		}
	};

} // stfx::

#endif // include guard
