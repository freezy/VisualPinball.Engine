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

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The two channels an analog plunger sensor can report.
	/// </summary>
	public enum PlungerSensorChannel : byte
	{
		/// <summary>Plunger position, 0 at rest, +1 fully retracted, negative forward of rest.</summary>
		Position = 0,
		/// <summary>Plunger velocity in position units per second, measured by the device.</summary>
		Velocity = 1
	}

	/// <summary>
	/// Exponential moving average with a fixed time constant. The first update
	/// sets the value.
	/// </summary>
	/// <remarks>Port of <c>vpinball/src/math/ExponentialMovingAverage.h</c>.</remarks>
	public struct PlungerExponentialMovingAverage
	{
		public float Tau;
		public float Value;
		private byte _initialized;

		public PlungerExponentialMovingAverage(float tauSeconds)
		{
			Tau = tauSeconds;
			Value = 0f;
			_initialized = 0;
		}

		public bool IsInitialized => _initialized != 0;

		public void Reset(float value = 0f)
		{
			_initialized = 0;
			Value = value;
		}

		/// <summary>Sets the value as if it had been the first sample.</summary>
		public void Seed(float value)
		{
			_initialized = 1;
			Value = value;
		}

		public float Update(float input, float dtSeconds)
		{
			if (_initialized == 0) {
				Value = input;
				_initialized = 1;
				return Value;
			}
			var alpha = dtSeconds / (Tau + dtSeconds);
			Value += alpha * (input - Value);
			return Value;
		}
	}

	/// <summary>
	/// One analog plunger sensor: a position channel and an optional velocity
	/// channel, filtered into a continuous one-millisecond position estimate.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Port of <c>PlungerSensor</c> in <c>vpinball/src/input/PlungerHandler.cpp</c>.
	/// Units are p.u.: 0 at rest, +1 fully retracted, forward of rest negative
	/// (the filter limits it to -1/3).
	/// </para>
	/// <para>
	/// Unlike VP, which polls the latest value of a device every millisecond,
	/// samples reach the physics loop in batches, once per frame. They are staged
	/// here with their host timestamps translated to simulation time, and each
	/// step consumes every sample up to its own time, one Kalman update per
	/// sample. A release that happens within one frame therefore keeps its
	/// shape. The host-to-simulation offset is chosen so the first sample of a
	/// clock segment lands at the end of the batch that stages it, and it is
	/// corrected after every batch so the newest sample is never in the future
	/// and never more than <see cref="MaxLatencyUsec"/> behind.
	/// </para>
	/// </remarks>
	public unsafe struct PlungerSensorState
	{
		/// <summary>Length of a default fully retracted plunger, 3", in meters. The filter works in meters.</summary>
		public const float KalmanUnitScale = 0.0762f;
		/// <summary>Staged samples per sensor. A full buffer resets the sensor clock.</summary>
		public const int PendingCapacity = 128;
		/// <summary>Position history used to find the release apex, one entry per millisecond.</summary>
		public const int PositionHistoryLength = 100;
		/// <summary>The newest staged sample is moved forward if it would lag the batch end by more than this.</summary>
		public const long MaxLatencyUsec = 40000;
		/// <summary>
		/// If host time advanced this much more than simulation time between two
		/// staged samples, the simulation was paused or stalled and the clock is reset.
		/// </summary>
		public const long ClockGapUsec = 100000;

		private const int ActivationDelayMs = 10000;
		private const int InitialRestSamples = 1000;
		private const float RestThreshold = 0.01f;
		private const float EmaTau = 0.004f;
		private const ulong RepeatAfterNs = 10000000ul;
		private const ulong RepeatBackdateNs = 5000000ul;

		private byte _positionMapped;
		private byte _velocityMapped;

		private PlungerKalmanFilter _kalman;
		private PlungerExponentialMovingAverage _emaPosition;
		private PlungerExponentialMovingAverage _emaVelocity;

		private float _position;
		private fixed float _prevPosition[PositionHistoryLength];
		private int _prevPositionIndex;

		private ulong _lastTimestampNs;
		private int _nRestSamples;
		private int _deactivationDelay;

		private byte _hasPositionValue;
		private float _positionValue;
		private float _velocityValue;
		private byte _needsReseed;
		private byte _reseedFromDropped;

		private byte _hasClockOffset;
		private long _hostToSimOffsetUsec;
		private fixed long _pendingTimeUsec[PendingCapacity];
		private fixed float _pendingValue[PendingCapacity];
		private fixed byte _pendingChannel[PendingCapacity];
		private int _pendingStart;
		private int _pendingCount;
		private int _overflowCount;
		private int _clockResetCount;
		private byte _hasLastStaged;
		private long _lastStagedHostUsec;
		private long _lastStagedBatchEndUsec;

		/// <summary>
		/// Creates a sensor with the given channels mapped.
		/// </summary>
		public PlungerSensorState(bool positionMapped, bool velocityMapped)
		{
			this = default;
			Configure(positionMapped, velocityMapped);
		}

		public bool IsPositionMapped => _positionMapped != 0;
		public bool IsVelocityMapped => _velocityMapped != 0;

		/// <summary>True for ten seconds after the plunger last left its rest position.</summary>
		public bool IsActive => _deactivationDelay > 0;

		/// <summary>Filtered position in p.u.</summary>
		public float RawPosition => _position;

		/// <summary>Filtered velocity in p.u./s.</summary>
		public float RawVelocity => _emaVelocity.Value;

		/// <summary>Latest position reading, as mapped, before filtering.</summary>
		public float PositionReading => _positionValue;

		/// <summary>Latest velocity reading, as mapped.</summary>
		public float VelocityReading => _velocityValue;

		public bool HasPositionReading => _hasPositionValue != 0;
		public int PendingCount => _pendingCount;

		/// <summary>Number of times staging overflowed and reset the clock.</summary>
		public int OverflowCount => _overflowCount;

		/// <summary>Number of clock resets, for any reason.</summary>
		public int ClockResetCount => _clockResetCount;

		/// <summary>
		/// Sets the mapped channels and clears all state, including activation.
		/// </summary>
		public void Configure(bool positionMapped, bool velocityMapped)
		{
			_positionMapped = positionMapped ? (byte)1 : (byte)0;
			_velocityMapped = velocityMapped ? (byte)1 : (byte)0;
			_kalman = new PlungerKalmanFilter(CreateKalmanConfig());
			_emaPosition = new PlungerExponentialMovingAverage(EmaTau);
			_emaVelocity = new PlungerExponentialMovingAverage(EmaTau);
			_nRestSamples = InitialRestSamples;
			_deactivationDelay = 0;
			_hasPositionValue = 0;
			_positionValue = 0f;
			_position = 0f;
			_prevPositionIndex = 0;
			for (var i = 0; i < PositionHistoryLength; i++) {
				_prevPosition[i] = 0f;
			}
			_overflowCount = 0;
			_clockResetCount = 0;
			ResetClock();
		}

		/// <summary>
		/// Drops all staged samples and the filter state, keeping activation.
		/// </summary>
		/// <remarks>
		/// Use it when the relation between host and simulation time breaks
		/// (pause, focus loss, skipped physics time, overflow). The filter
		/// restarts with zero velocity from the next fresh position reading, so
		/// the gap cannot look like a release. If the reset drops queued
		/// readings, it restarts from the newest of them instead: joysticks only
		/// report changes, so a plunger that came to rest in a skipped frame may
		/// never send another one.
		/// </remarks>
		public void ResetClock()
		{
			for (var i = _pendingCount - 1; i >= 0; i--) {
				var slot = PendingSlot(i);
				if (_pendingChannel[slot] == (byte)PlungerSensorChannel.Position) {
					_positionValue = _pendingValue[slot];
					_hasPositionValue = 1;
					_reseedFromDropped = 1;
					break;
				}
			}
			// A release in progress belongs to the time before the gap.
			for (var i = 0; i < PositionHistoryLength; i++) {
				_prevPosition[i] = _position;
			}
			_pendingStart = 0;
			_pendingCount = 0;
			_hasClockOffset = 0;
			_hostToSimOffsetUsec = 0;
			_lastTimestampNs = 0;
			_velocityValue = 0f;
			_kalman.Reset(_position * KalmanUnitScale);
			_emaVelocity.Seed(0f);
			_needsReseed = 1;
			_hasLastStaged = 0;
			_clockResetCount++;
		}

		/// <summary>
		/// Drops all state and deactivates the sensor, e.g. after the device was unplugged.
		/// </summary>
		public void Deactivate()
		{
			Configure(IsPositionMapped, IsVelocityMapped);
		}

		/// <summary>
		/// Stages one mapped reading for the steps of the batch that ends at
		/// <paramref name="batchEndUsec"/>.
		/// </summary>
		/// <returns>False if the staging buffer overflowed and the sensor clock was reset.</returns>
		public bool Stage(PlungerSensorChannel channel, float value, long hostTimeUsec, ulong batchEndUsec)
		{
			if (!math.isfinite(value)) {
				return true;
			}
			if (channel == PlungerSensorChannel.Position ? _positionMapped == 0 : _velocityMapped == 0) {
				return true;
			}

			if (_hasLastStaged != 0) {
				var hostAdvance = hostTimeUsec - _lastStagedHostUsec;
				var simAdvance = (long)batchEndUsec - _lastStagedBatchEndUsec;
				if (hostAdvance - simAdvance > ClockGapUsec) {
					ResetClock();
				}
			}

			var overflowed = false;
			if (_pendingCount >= PendingCapacity) {
				_overflowCount++;
				ResetClock();
				overflowed = true;
			}
			_hasLastStaged = 1;
			_lastStagedHostUsec = hostTimeUsec;
			_lastStagedBatchEndUsec = (long)batchEndUsec;
			if (_hasClockOffset == 0) {
				_hostToSimOffsetUsec = (long)batchEndUsec - hostTimeUsec;
				_hasClockOffset = 1;
			}

			var simTimeUsec = hostTimeUsec + _hostToSimOffsetUsec;
			if (_pendingCount > 0) {
				// Keep the queue ordered, devices may interleave channels with equal or jittered stamps.
				simTimeUsec = math.max(simTimeUsec, _pendingTimeUsec[PendingSlot(_pendingCount - 1)]);
			}
			var slot = PendingSlot(_pendingCount);
			_pendingTimeUsec[slot] = simTimeUsec;
			_pendingValue[slot] = value;
			_pendingChannel[slot] = (byte)channel;
			_pendingCount++;
			return !overflowed;
		}

		/// <summary>
		/// Corrects the clock offset after a batch has been staged, so the newest
		/// staged sample is neither after the batch end nor too far behind it.
		/// </summary>
		public void EndStaging(ulong batchEndUsec)
		{
			if (_pendingCount == 0) {
				return;
			}
			var newest = _pendingTimeUsec[PendingSlot(_pendingCount - 1)];
			var end = (long)batchEndUsec;
			long shift = 0;
			if (newest > end) {
				shift = end - newest;
			} else if (newest < end - MaxLatencyUsec) {
				shift = end - newest;
			}
			if (shift == 0) {
				return;
			}
			_hostToSimOffsetUsec += shift;
			for (var i = 0; i < _pendingCount; i++) {
				_pendingTimeUsec[PendingSlot(i)] += shift;
			}
		}

		/// <summary>
		/// Advances the sensor to <paramref name="nowUsec"/>, the simulation time
		/// at the end of this millisecond.
		/// </summary>
		public void StepOneMillisecond(ulong nowUsec)
		{
			if (_deactivationDelay > 0) {
				_deactivationDelay--;
			}

			var nowNs = nowUsec * 1000ul;
			var consumedPosition = false;
			while (_pendingCount > 0) {
				var slot = PendingSlot(0);
				var sampleTimeUsec = _pendingTimeUsec[slot];
				if (sampleTimeUsec > (long)nowUsec) {
					break;
				}
				var value = _pendingValue[slot];
				var channel = (PlungerSensorChannel)_pendingChannel[slot];
				_pendingStart = (_pendingStart + 1) % PendingCapacity;
				_pendingCount--;

				if (channel == PlungerSensorChannel.Velocity) {
					_velocityValue = value;
					continue;
				}

				var timestampNs = sampleTimeUsec > 0 ? (ulong)sampleTimeUsec * 1000ul : 1ul;
				_positionValue = value;
				_hasPositionValue = 1;
				consumedPosition = true;
				if (_needsReseed != 0) {
					Reseed(value, timestampNs);
				} else {
					_kalman.UpdatePosition(timestampNs, value * KalmanUnitScale);
					_lastTimestampNs = math.max(_lastTimestampNs, timestampNs);
				}
			}

			if (_positionMapped == 0 || _hasPositionValue == 0) {
				return;
			}

			if (math.abs(_positionValue) < RestThreshold) {
				_nRestSamples++;
			} else {
				_nRestSamples = 0;
			}
			if (_nRestSamples < 10) {
				_deactivationDelay = ActivationDelayMs;
			}
			var isAtRest = _nRestSamples > 30;

			if (_needsReseed != 0) {
				if (_reseedFromDropped == 0 || HasPendingPosition()) {
					// Hold the last estimate until a fresh reading is due.
					return;
				}
				Reseed(_positionValue, nowNs);
			}

			if (!consumedPosition && nowNs > _lastTimestampNs + RepeatAfterNs) {
				// Don't let the estimator run freely for long, repeat the last reading.
				_lastTimestampNs = nowNs - RepeatBackdateNs;
				_kalman.UpdatePosition(_lastTimestampNs, _positionValue * KalmanUnitScale);
			}
			_kalman.PredictTo(nowNs);
			if (isAtRest) {
				_kalman.UpdateVelocityZero(1.0e-4f);
			}

			_emaVelocity.Update(_kalman.Velocity / KalmanUnitScale, 0.001f);
			_position = _emaPosition.Update(_kalman.Position / KalmanUnitScale, 0.001f);
			_prevPosition[_prevPositionIndex] = _position;
			_prevPositionIndex = (_prevPositionIndex + 1) % PositionHistoryLength;
		}

		/// <summary>
		/// Velocity at which the plunger hits a ball, in t.u./s.
		/// </summary>
		/// <param name="restPos">Rest position of the table's plunger in t.u.</param>
		/// <remarks>
		/// A measured velocity is used as is. With position only, a release from
		/// the highest position of the last 100 ms is assumed, which yields the
		/// same speed a keyboard plunger pulled that far would.
		/// </remarks>
		public float GetHitVelocity(float restPos)
		{
			if (_velocityMapped != 0) {
				return _velocityValue * (1f - restPos);
			}

			var releaseApex = _prevPosition[0];
			for (var i = 1; i < PositionHistoryLength; i++) {
				releaseApex = math.max(releaseApex, _prevPosition[i]);
			}
			if (_position > releaseApex - 0.1f) {
				return 0f;
			}
			var dx = math.lerp(restPos, 1f, releaseApex) - restPos;
			return -math.max(0f, dx) * (100f / 13f);
		}

		/// <summary>Highest filtered position of the last 100 ms, in p.u.</summary>
		public float ReleaseApex
		{
			get {
				var apex = _prevPosition[0];
				for (var i = 1; i < PositionHistoryLength; i++) {
					apex = math.max(apex, _prevPosition[i]);
				}
				return apex;
			}
		}

		private void Reseed(float value, ulong timestampNs)
		{
			_kalman.Reset(value * KalmanUnitScale, 0f, timestampNs);
			_emaPosition.Seed(_kalman.Position / KalmanUnitScale);
			_emaVelocity.Seed(0f);
			_position = _emaPosition.Value;
			for (var i = 0; i < PositionHistoryLength; i++) {
				_prevPosition[i] = _position;
			}
			_lastTimestampNs = timestampNs;
			_needsReseed = 0;
			_reseedFromDropped = 0;
		}

		private bool HasPendingPosition()
		{
			for (var i = 0; i < _pendingCount; i++) {
				if (_pendingChannel[PendingSlot(i)] == (byte)PlungerSensorChannel.Position) {
					return true;
				}
			}
			return false;
		}

		private int PendingSlot(int index)
		{
			return (_pendingStart + index) % PendingCapacity;
		}

		private static PlungerKalmanFilter.Config CreateKalmanConfig()
		{
			return new PlungerKalmanFilter.Config {
				PositionMeasurementVariance = 1.0e-5f,
				AccelerationProcessNoise = 0.5f,
				InitialPositionVariance = 1.0e-4f,
				InitialVelocityVariance = 1.0e-2f,
				EnablePositionLimits = true,
				MinPosition = -KalmanUnitScale / 3f,
				MaxPosition = KalmanUnitScale,
				HistoryCapacity = PlungerKalmanFilter.MaxHistoryCapacity
			};
		}
	}
}
