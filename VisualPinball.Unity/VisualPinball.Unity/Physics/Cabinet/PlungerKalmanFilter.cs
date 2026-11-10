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
	/// Two-state (position, velocity) Kalman filter for analog plunger sensors.
	/// </summary>
	/// <remarks>
	/// Port of <c>vpinball/src/input/PlungerKalmanFilter.h</c>. Unobserved
	/// acceleration is modelled as white process noise, prediction runs in
	/// sub-steps of at most one millisecond, and a short history allows samples
	/// that arrive late to be applied at their own timestamp. The covariance is
	/// symmetric, so only three of its four values are stored.
	/// </remarks>
	public unsafe struct PlungerKalmanFilter
	{
		/// <summary>Upper bound of the rewind history, one slot per prediction.</summary>
		public const int MaxHistoryCapacity = 64;

		private const int HistoryStride = 5; // position, velocity, p00, p01, p11

		/// <summary>
		/// Filter noise model and limits.
		/// </summary>
		public struct Config
		{
			/// <summary>Continuous-time acceleration noise intensity. Higher reacts faster but is noisier.</summary>
			public float AccelerationProcessNoise;
			/// <summary>Position measurement variance (not standard deviation).</summary>
			public float PositionMeasurementVariance;
			public float InitialPositionVariance;
			public float InitialVelocityVariance;
			public bool EnablePositionLimits;
			public float MinPosition;
			public float MaxPosition;
			/// <summary>Number of rewind snapshots, clamped to 1..<see cref="MaxHistoryCapacity"/>.</summary>
			public int HistoryCapacity;

			public static Config Default => new() {
				AccelerationProcessNoise = 1.0f,
				PositionMeasurementVariance = 1.0e-5f,
				InitialPositionVariance = 1.0e-4f,
				InitialVelocityVariance = 1.0e-2f,
				EnablePositionLimits = false,
				MinPosition = 0f,
				MaxPosition = 1f,
				HistoryCapacity = MaxHistoryCapacity
			};
		}

		private Config _config;
		private float _position;
		private float _velocity;
		private float _p00;
		private float _p01;
		private float _p11;
		private ulong _timeNs;
		private fixed ulong _historyTimeNs[MaxHistoryCapacity];
		private fixed float _history[MaxHistoryCapacity * HistoryStride];
		private int _historyHead;
		private int _historyCount;

		/// <summary>
		/// Creates a filter at position and velocity zero with no time base.
		/// </summary>
		public PlungerKalmanFilter(Config config)
		{
			this = default;
			_config = config;
			Reset(0f, 0f, 0ul);
		}

		public static PlungerKalmanFilter Default => new(Config.Default);

		public Config Configuration => _config;
		public float Position => _position;
		public float Velocity => _velocity;

		/// <summary>Current filter time in nanoseconds, 0 while no time base is established.</summary>
		public ulong TimeNs => _timeNs;

		/// <summary>
		/// Replaces the noise model without touching the state.
		/// </summary>
		public void SetConfig(Config config)
		{
			_config = config;
		}

		/// <summary>
		/// Resets the state to the given position and velocity at the given time.
		/// </summary>
		/// <param name="timestampNs">Time of the state, or 0 to let the first measurement anchor the time base.</param>
		public void Reset(float position, float velocity = 0f, ulong timestampNs = 0ul)
		{
			_position = position;
			_velocity = velocity;
			_p00 = math.max(_config.InitialPositionVariance, 1.0e-12f);
			_p01 = 0f;
			_p11 = math.max(_config.InitialVelocityVariance, 1.0e-12f);
			ApplyPositionLimits();
			_timeNs = timestampNs;
			_historyHead = 0;
			_historyCount = 0;
			SaveSnapshot();
		}

		/// <summary>
		/// Advances the filter to the given time. Does nothing if that time is not in the future.
		/// </summary>
		public void PredictTo(ulong timestampNs)
		{
			if (timestampNs <= _timeNs) {
				return;
			}
			var dt = (float)(timestampNs - _timeNs) * 1.0e-9f;
			SaveSnapshot();
			PredictInternal(dt);
			_timeNs = timestampNs;
		}

		/// <summary>
		/// Applies a position measurement taken at the given time.
		/// </summary>
		/// <remarks>
		/// A measurement older than the filter time rewinds to the newest snapshot
		/// at or before it, applies it there and predicts back to the present.
		/// </remarks>
		public void UpdatePosition(ulong timestampNs, float measuredPosition)
		{
			if (!math.isfinite(measuredPosition)) {
				return;
			}

			if (_timeNs == 0ul) {
				_timeNs = timestampNs;
				_historyHead = 0;
				_historyCount = 0;
				SaveSnapshot();
			}

			if (timestampNs >= _timeNs) {
				if (timestampNs > _timeNs) {
					SaveSnapshot();
					PredictInternal((float)(timestampNs - _timeNs) * 1.0e-9f);
					_timeNs = timestampNs;
				}
				ApplyMeasurement(measuredPosition);

			} else {
				var presentNs = _timeNs;
				RewindAndUpdate(timestampNs, measuredPosition);
				if (_timeNs < presentNs) {
					SaveSnapshot();
					PredictInternal((float)(presentNs - _timeNs) * 1.0e-9f);
					_timeNs = presentNs;
				}
			}
		}

		/// <summary>
		/// Pseudo-measurement "velocity is zero", used while a rest detector
		/// reports a stationary plunger. Never call it during motion.
		/// </summary>
		public void UpdateVelocityZero(float velocityVariance)
		{
			var r = math.max(velocityVariance, 1.0e-12f);
			var y = -_velocity;
			var s = _p11 + r;
			if (!math.isfinite(s) || s <= 1.0e-20f) {
				return;
			}
			var k0 = _p01 / s;
			var k1 = _p11 / s;
			_position += k0 * y;
			_velocity += k1 * y;
			UpdateCovariance(k0, k1, s);
		}

		private void ApplyMeasurement(float measuredPosition)
		{
			var r = math.max(_config.PositionMeasurementVariance, 1.0e-12f);
			var y = measuredPosition - _position;
			var s = _p00 + r;
			if (!math.isfinite(s) || s <= 1.0e-20f) {
				return;
			}
			var k0 = _p00 / s;
			var k1 = _p01 / s;
			_position += k0 * y;
			_velocity += k1 * y;
			UpdateCovariance(k0, k1, s);
		}

		/// <summary>P = P - K S Kᵀ, then symmetrize and clamp.</summary>
		private void UpdateCovariance(float k0, float k1, float s)
		{
			_p00 -= k0 * s * k0;
			_p01 -= k0 * s * k1;
			_p11 -= k1 * s * k1;
			ClampCovariance();
			ApplyPositionLimits();
		}

		private void PredictInternal(float timeStep)
		{
			if (!math.isfinite(timeStep) || timeStep <= 0f) {
				return;
			}

			var q = math.max(_config.AccelerationProcessNoise, 0f);
			while (timeStep > 0f) {
				var dt = math.min(timeStep, 0.001f);
				timeStep -= dt;

				// x = F x with F = [1 dt; 0 1]
				_position += _velocity * dt;

				// P = F P Fᵀ + Q, Q = q [dt³/3 dt²/2; dt²/2 dt]
				var dt2 = dt * dt;
				var dt3 = dt2 * dt;
				var p00 = _p00 + dt * _p01 + dt * (_p01 + dt * _p11);
				var p01 = _p01 + dt * _p11;
				_p00 = p00 + q * dt3 / 3f;
				_p01 = p01 + q * dt2 / 2f;
				_p11 += q * dt;

				ClampCovariance();
				ApplyPositionLimits();
			}
		}

		private void ClampCovariance()
		{
			_p00 = math.max(_p00, 1.0e-12f);
			_p11 = math.max(_p11, 1.0e-12f);
		}

		private void ApplyPositionLimits()
		{
			if (!_config.EnablePositionLimits) {
				return;
			}
			_position = math.clamp(_position, _config.MinPosition, _config.MaxPosition);

			// Against a physical stop, drop velocity that pushes further out.
			if (_position <= _config.MinPosition && _velocity < 0f) {
				_velocity = 0f;
			}
			if (_position >= _config.MaxPosition && _velocity > 0f) {
				_velocity = 0f;
			}
		}

		private int EffectiveHistoryCapacity()
		{
			return math.clamp(_config.HistoryCapacity, 1, MaxHistoryCapacity);
		}

		/// <summary>Saves the current state before the next prediction.</summary>
		private void SaveSnapshot()
		{
			var cap = EffectiveHistoryCapacity();
			var slot = _historyHead;
			_historyTimeNs[slot] = _timeNs;
			var offset = slot * HistoryStride;
			_history[offset] = _position;
			_history[offset + 1] = _velocity;
			_history[offset + 2] = _p00;
			_history[offset + 3] = _p01;
			_history[offset + 4] = _p11;
			_historyHead = (_historyHead + 1) % cap;
			if (_historyCount < cap) {
				_historyCount++;
			}
		}

		/// <summary>
		/// Restores the newest snapshot at or before the target time, or the
		/// oldest one if all are newer, and truncates the history after it.
		/// </summary>
		private bool RestoreSnapshot(ulong targetNs)
		{
			if (_historyCount == 0) {
				return false;
			}
			var cap = EffectiveHistoryCapacity();

			var bestSlot = -1;
			for (var i = 0; i < _historyCount; i++) {
				var slot = ((_historyHead - 1 - i) % cap + cap) % cap;
				if (_historyTimeNs[slot] <= targetNs) {
					bestSlot = slot;
					break;
				}
			}
			if (bestSlot == -1) {
				bestSlot = ((_historyHead - _historyCount) % cap + cap) % cap;
			}

			var offset = bestSlot * HistoryStride;
			_position = _history[offset];
			_velocity = _history[offset + 1];
			_p00 = _history[offset + 2];
			_p01 = _history[offset + 3];
			_p11 = _history[offset + 4];
			_timeNs = _historyTimeNs[bestSlot];

			var keepCount = 0;
			for (var i = 0; i < _historyCount; i++) {
				var slot = ((_historyHead - _historyCount + i) % cap + cap) % cap;
				keepCount = i + 1;
				if (slot == bestSlot) {
					break;
				}
			}
			_historyCount = keepCount;
			_historyHead = (bestSlot + 1) % cap;
			return true;
		}

		private void RewindAndUpdate(ulong timestampNs, float measuredPosition)
		{
			if (!RestoreSnapshot(timestampNs)) {
				ApplyMeasurement(measuredPosition);
				return;
			}
			if (_timeNs < timestampNs) {
				PredictInternal((float)(timestampNs - _timeNs) * 1.0e-9f);
				_timeNs = timestampNs;
			}
			ApplyMeasurement(measuredPosition);
		}
	}
}
