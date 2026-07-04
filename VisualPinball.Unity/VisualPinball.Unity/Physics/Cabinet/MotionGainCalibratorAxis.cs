// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using Unity.Collections;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public struct MotionGainCalibratorAxis
	{
		public struct Config
		{
			public float InitialGain;
			public ulong MinSegmentDurationUs;
			public ulong MaxSegmentDurationUs;
			public int MinSampleCount;
			public float MinVelocityPeakToPeak;
			public float MinIntegratedAccelPeakToPeak;
			public float MinRegressionDenominator;
			public float ForgettingFactor;
			public float MinGain;
			public float MaxGain;
			public byte ExcludeSegmentEndpointsFromFit;
			public byte UseTriangularWeights;
			public float Epsilon;

			public static Config Default => new() {
				InitialGain = 1.0f,
				MinSegmentDurationUs = 30000,
				MaxSegmentDurationUs = 2000000,
				MinSampleCount = 8,
				MinVelocityPeakToPeak = 0.02f,
				MinIntegratedAccelPeakToPeak = 0.002f,
				MinRegressionDenominator = 1.0e-6f,
				ForgettingFactor = 0.995f,
				MinGain = 0.001f,
				MaxGain = 1000.0f,
				ExcludeSegmentEndpointsFromFit = 1,
				UseTriangularWeights = 1,
				Epsilon = 1.0e-9f
			};
		}

		public struct SegmentResult
		{
			public byte Accepted;
			public ulong DurationUs;
			public int SampleCount;
			public float SegmentGain;
			public float SegmentQuality;
			public float SegmentResidualRms;
			public float VelocityPeakToPeak;
			public float IntegratedAccelPeakToPeak;
			public float SegmentNumerator;
			public float SegmentDenominator;
		}

		private struct Sample
		{
			public ulong TimeUs;
			public float Velocity;
			public float Acceleration;
		}

		private Config _config;
		private byte _segmentActive;
		private ulong _segmentStartUs;
		private FixedList4096Bytes<Sample> _samples;
		private float _gain;
		private double _accumulatedNumerator;
		private double _accumulatedDenominator;
		private int _startedSegmentCount;
		private int _acceptedSegmentCount;
		private int _rejectedSegmentCount;
		private ulong _totalAcceptedDurationUs;
		private SegmentResult _lastResult;
		private float _globalConfidence;

		public MotionGainCalibratorAxis(Config config)
		{
			_config = config;
			_segmentActive = 0;
			_segmentStartUs = 0;
			_samples = default;
			_gain = config.InitialGain;
			_accumulatedNumerator = 0.0;
			_accumulatedDenominator = 0.0;
			_startedSegmentCount = 0;
			_acceptedSegmentCount = 0;
			_rejectedSegmentCount = 0;
			_totalAcceptedDurationUs = 0;
			_lastResult = default;
			_globalConfidence = 0f;
		}

		public static MotionGainCalibratorAxis Default => new(Config.Default);
		public bool IsSegmentActive => _segmentActive != 0;
		public float Gain => _gain;
		public int StartedSegmentCount => _startedSegmentCount;
		public int AcceptedSegmentCount => _acceptedSegmentCount;
		public int RejectedSegmentCount => _rejectedSegmentCount;
		public ulong TotalAcceptedDurationUs => _totalAcceptedDurationUs;
		public float GlobalConfidence => _globalConfidence;
		public SegmentResult LastResult => _lastResult;

		public void Configure(Config config)
		{
			_config = config;
			_gain = math.clamp(_gain, _config.MinGain, _config.MaxGain);
		}

		public void Reset()
		{
			_segmentActive = 0;
			_samples.Clear();
			_gain = _config.InitialGain;
			_accumulatedNumerator = 0.0;
			_accumulatedDenominator = 0.0;
			_startedSegmentCount = 0;
			_acceptedSegmentCount = 0;
			_rejectedSegmentCount = 0;
			_totalAcceptedDurationUs = 0;
			_lastResult = default;
			_globalConfidence = 0f;
		}

		public float ScaleAcceleration(float rawAcceleration) => _gain * rawAcceleration;

		public float ScaleVelocityToAccelerationUnits(float rawVelocity)
		{
			return math.abs(_gain) <= _config.Epsilon ? 0f : rawVelocity / _gain;
		}

		public void StartSegment(ulong timeUs)
		{
			_segmentActive = 1;
			_samples.Clear();
			_startedSegmentCount++;
			_segmentStartUs = timeUs;
		}

		public bool AddSample(ulong timeUs, float rawVelocity, float rawAcceleration)
		{
			if (!IsSegmentActive) {
				return false;
			}
			if (_samples.Length > 0 && timeUs <= _samples[_samples.Length - 1].TimeUs) {
				return false;
			}
			if (_samples.Length >= _samples.Capacity) {
				// The regression assumes segments that begin and end at rest (the endpoint
				// detrend removes the baseline between them), so the segment must not be
				// split here. Halve the sample resolution instead: the buffer covers the
				// full 2s max segment duration at progressively coarser (but timestamped,
				// hence still correctly integrated) steps.
				CompactSamples();
			}

			_samples.Add(new Sample {
				TimeUs = timeUs,
				Velocity = rawVelocity,
				Acceleration = rawAcceleration
			});
			return true;
		}

		private void CompactSamples()
		{
			var compacted = 0;
			var i = 0;
			for (; i + 1 < _samples.Length; i += 2) {
				var a = _samples[i];
				var b = _samples[i + 1];
				_samples[compacted++] = new Sample {
					TimeUs = a.TimeUs + (b.TimeUs - a.TimeUs) / 2,
					Velocity = 0.5f * (a.Velocity + b.Velocity),
					Acceleration = 0.5f * (a.Acceleration + b.Acceleration)
				};
			}
			if (i < _samples.Length) {
				_samples[compacted++] = _samples[i];
			}
			_samples.Length = compacted;
		}

		public bool EndSegment()
		{
			if (!IsSegmentActive) {
				_lastResult = default;
				return false;
			}

			_segmentActive = 0;
			_lastResult = EvaluateCurrentSegment();

			if (_lastResult.Accepted == 0) {
				_rejectedSegmentCount++;
				_samples.Clear();
				return false;
			}

			var lambda = (double)math.clamp(_config.ForgettingFactor, 0f, 1f);
			_accumulatedNumerator = lambda * _accumulatedNumerator + _lastResult.SegmentNumerator;
			_accumulatedDenominator = lambda * _accumulatedDenominator + _lastResult.SegmentDenominator;

			if (_accumulatedDenominator > _config.Epsilon) {
				var gain = _accumulatedNumerator / _accumulatedDenominator;
				_gain = math.clamp((float)gain, _config.MinGain, _config.MaxGain);
			}

			_acceptedSegmentCount++;
			_totalAcceptedDurationUs += _lastResult.DurationUs;
			_globalConfidence = ComputeGlobalConfidence();
			_samples.Clear();
			return true;
		}

		private float ComputeGlobalConfidence()
		{
			if (_acceptedSegmentCount == 0) {
				return 0f;
			}

			var segFactor = 1f - math.exp(-0.25f * _acceptedSegmentCount);
			var durationS = _totalAcceptedDurationUs * 1.0e-6f;
			var durationFactor = 1f - math.exp(-durationS * 0.5f);
			var denomFactor = 1f - math.exp(-(float)_accumulatedDenominator * 0.25f);
			return math.clamp(0.40f * segFactor + 0.35f * durationFactor + 0.25f * denomFactor, 0f, 1f);
		}

		private SegmentResult EvaluateCurrentSegment()
		{
			var result = new SegmentResult {
				SegmentGain = 1f
			};
			var n = _samples.Length;
			result.SampleCount = n;
			if (n < _config.MinSampleCount) {
				return result;
			}

			var startUs = _samples[0].TimeUs;
			var endUs = _samples[n - 1].TimeUs;
			var durationUs = endUs - startUs;
			result.DurationUs = durationUs;
			if (durationUs < _config.MinSegmentDurationUs || durationUs > _config.MaxSegmentDurationUs) {
				return result;
			}

			var tNorm = default(FixedList4096Bytes<float>);
			var velocity = default(FixedList4096Bytes<float>);
			var velocityDetrended = default(FixedList4096Bytes<float>);
			var integratedAcceleration = default(FixedList4096Bytes<float>);
			var integratedAccelerationDetrended = default(FixedList4096Bytes<float>);
			tNorm.Length = n;
			velocity.Length = n;
			velocityDetrended.Length = n;
			integratedAcceleration.Length = n;
			integratedAccelerationDetrended.Length = n;

			var totalDurationS = durationUs * 1.0e-6;
			if (totalDurationS <= 0.0) {
				return result;
			}

			for (var i = 0; i < n; i++) {
				var relUs = (double)(_samples[i].TimeUs - startUs);
				tNorm[i] = (float)(relUs / durationUs);
				velocity[i] = _samples[i].Velocity;
			}

			integratedAcceleration[0] = 0f;
			for (var i = 1; i < n; i++) {
				var dt = (_samples[i].TimeUs - _samples[i - 1].TimeUs) * 1.0e-6f;
				var a0 = _samples[i - 1].Acceleration;
				var a1 = _samples[i].Acceleration;
				integratedAcceleration[i] = integratedAcceleration[i - 1] + 0.5f * (a0 + a1) * dt;
			}

			var vel0 = velocity[0];
			var vel1 = velocity[n - 1];
			var int0 = integratedAcceleration[0];
			var int1 = integratedAcceleration[n - 1];
			for (var i = 0; i < n; i++) {
				velocityDetrended[i] = velocity[i] - math.lerp(vel0, vel1, tNorm[i]);
				integratedAccelerationDetrended[i] = integratedAcceleration[i] - math.lerp(int0, int1, tNorm[i]);
			}

			result.VelocityPeakToPeak = PeakToPeak(velocityDetrended, n);
			result.IntegratedAccelPeakToPeak = PeakToPeak(integratedAccelerationDetrended, n);
			if (result.VelocityPeakToPeak < _config.MinVelocityPeakToPeak
				|| result.IntegratedAccelPeakToPeak < _config.MinIntegratedAccelPeakToPeak) {
				return result;
			}

			var numerator = 0.0;
			var denominator = 0.0;
			var fitBegin = _config.ExcludeSegmentEndpointsFromFit != 0 && n >= 3 ? 1 : 0;
			var fitEnd = _config.ExcludeSegmentEndpointsFromFit != 0 && n >= 3 ? n - 1 : n;

			for (var i = fitBegin; i < fitEnd; i++) {
				var x = integratedAccelerationDetrended[i];
				var y = velocityDetrended[i];
				var w = ComputeSampleWeight(tNorm[i]);
				numerator += (double)w * x * y;
				denominator += (double)w * x * x;
			}

			result.SegmentNumerator = (float)numerator;
			result.SegmentDenominator = (float)denominator;
			if (denominator < _config.MinRegressionDenominator) {
				return result;
			}

			var segmentGain = numerator / denominator;
			result.SegmentGain = (float)segmentGain;
			if (!IsFinite(result.SegmentGain)) {
				return result;
			}

			var weightedResidual2 = 0.0;
			var weightedCount = 0.0;
			for (var i = fitBegin; i < fitEnd; i++) {
				var x = integratedAccelerationDetrended[i];
				var y = velocityDetrended[i];
				var w = ComputeSampleWeight(tNorm[i]);
				var r = y - segmentGain * x;
				weightedResidual2 += (double)w * r * r;
				weightedCount += w;
			}
			if (weightedCount <= _config.Epsilon) {
				return result;
			}

			result.SegmentResidualRms = (float)math.sqrt((float)(weightedResidual2 / weightedCount));
			var signalScale = math.max(result.VelocityPeakToPeak, _config.Epsilon);
			var normalizedResidual = result.SegmentResidualRms / signalScale;
			result.SegmentQuality = 1f - math.clamp(normalizedResidual * 2f, 0f, 1f);

			if (!IsFinite(result.SegmentQuality)) {
				return result;
			}
			if (result.SegmentGain < _config.MinGain || result.SegmentGain > _config.MaxGain) {
				return result;
			}
			if (result.SegmentQuality <= 0.05f) {
				return result;
			}

			result.Accepted = 1;
			return result;
		}

		private float ComputeSampleWeight(float tNorm)
		{
			if (_config.UseTriangularWeights == 0) {
				return 1f;
			}
			var d = math.abs(2f * tNorm - 1f);
			return math.max(0f, 1f - d);
		}

		private static float PeakToPeak(FixedList4096Bytes<float> data, int count)
		{
			if (count == 0) {
				return 0f;
			}
			var min = data[0];
			var max = data[0];
			for (var i = 1; i < count; i++) {
				min = math.min(min, data[i]);
				max = math.max(max, data[i]);
			}
			return max - min;
		}

		private static bool IsFinite(float value)
		{
			return !float.IsNaN(value) && !float.IsInfinity(value);
		}
	}
}
