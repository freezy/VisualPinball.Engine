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
	/// <summary>
	/// One-dimensional Kalman filter for cabinet position, velocity, acceleration,
	/// and slow sensor bias.
	/// </summary>
	/// <remarks>
	/// Adapted from VP's
	/// <c>vpinball/src/physics/cabinet/MotionKalmanAxis.h</c>. Cabinet devices can
	/// provide velocity, acceleration, or both, often with independent drift. The
	/// five-state filter lets VPE fuse those measurements while keeping estimated
	/// sensor bias separate from physical cabinet motion.
	/// </remarks>
	public unsafe struct MotionKalmanAxis
	{
		private const int StatePosition = 0;
		private const int StateVelocity = 1;
		private const int StateAcceleration = 2;
		private const int StateVelocityBias = 3;
		private const int StateAccelerationBias = 4;
		private const int StateCount = 5;
		// Keep the C# port's rewind history modest: nudge state lives inside
		// value-type simulation structs, so a C++-sized buffer makes routine state
		// copies too large for some managed call sites.
		private const int MaxHistoryCapacity = 16;
		private const int HistoryCovarianceStride = StateCount * StateCount;

		/// <summary>
		/// Filter covariance and timing parameters.
		/// </summary>
		public struct Config
		{
			public float ProcessJerkVariance;
			public float VelocityBiasProcessVariance;
			public float AccelerationBiasProcessVariance;
			public float VelocityMeasurementVariance;
			public float AccelerationMeasurementVariance;
			public float ZeroPositionMeasurementVariance;
			public float ZeroVelocityMeasurementVariance;
			public float ZeroAccelerationMeasurementVariance;
			public float InitialPositionVariance;
			public float InitialVelocityVariance;
			public float InitialAccelerationVariance;
			public float InitialVelocityBiasVariance;
			public float InitialAccelerationBiasVariance;
			public float BiasMeanReversionTimeS;
			public float MinDt;
			public float MaxDt;
			public int HistoryCapacity;

			public static Config Default => new() {
				ProcessJerkVariance = 800.0f,
				VelocityBiasProcessVariance = 0.0001f,
				AccelerationBiasProcessVariance = 0.0001f,
				VelocityMeasurementVariance = 0.05f * 0.05f,
				AccelerationMeasurementVariance = 0.40f * 0.40f,
				ZeroPositionMeasurementVariance = 0.0005f * 0.0005f,
				ZeroVelocityMeasurementVariance = 0.01f * 0.01f,
				ZeroAccelerationMeasurementVariance = 0.10f * 0.10f,
				InitialPositionVariance = 1.0e-4f,
				InitialVelocityVariance = 1.0f,
				InitialAccelerationVariance = 25.0f,
				InitialVelocityBiasVariance = 0.01f,
				InitialAccelerationBiasVariance = 0.01f,
				BiasMeanReversionTimeS = 5.0f,
				MinDt = 1.0e-6f,
				MaxDt = 0.001f,
				HistoryCapacity = MaxHistoryCapacity
			};
		}

		private Config _config;
		private byte _initialized;
		private ulong _timeUs;
		private Vector5f _state;
		private Matrix5f _covariance;
		private fixed ulong _historyTimeUs[MaxHistoryCapacity];
		private fixed float _historyState[MaxHistoryCapacity * StateCount];
		private fixed float _historyCovariance[MaxHistoryCapacity * HistoryCovarianceStride];
		private int _historyHead;
		private int _historyCount;

		/// <summary>
		/// Creates an uninitialized filter using the supplied covariance settings.
		/// </summary>
		public MotionKalmanAxis(Config config)
		{
			this = default;
			_config = config;
			_state.SetZero();
			_covariance.SetZero();
		}

		/// <summary>
		/// Filter configured with defaults tuned for nudge sensor fusion.
		/// </summary>
		public static MotionKalmanAxis Default => new(Config.Default);

		public bool IsInitialized => _initialized != 0;
		public ulong TimeUs => _timeUs;
		public float Position => _state[StatePosition];
		public float Velocity => _state[StateVelocity];
		public float Acceleration => _state[StateAcceleration];
		public float VelocityBias => _state[StateVelocityBias];
		public float AccelerationBias => _state[StateAccelerationBias];
		public float BiasedVelocity => Velocity + VelocityBias;
		public float BiasedAcceleration => Acceleration + AccelerationBias;

		/// <summary>
		/// Applies new covariance settings without altering the current state.
		/// </summary>
		public void Configure(Config config)
		{
			_config = config;
		}

		/// <summary>
		/// Initializes or reinitializes the filter at a known state and timestamp.
		/// </summary>
		public void Reset(ulong timeUs, float position = 0f, float velocity = 0f, float acceleration = 0f,
			float velocityBias = 0f, float accelerationBias = 0f)
		{
			_initialized = 1;
			_timeUs = timeUs;
			_state.Set(position, velocity, acceleration, velocityBias, accelerationBias);
			_covariance.SetZero();
			_covariance[StatePosition, StatePosition] = _config.InitialPositionVariance;
			_covariance[StateVelocity, StateVelocity] = _config.InitialVelocityVariance;
			_covariance[StateAcceleration, StateAcceleration] = _config.InitialAccelerationVariance;
			_covariance[StateVelocityBias, StateVelocityBias] = _config.InitialVelocityBiasVariance;
			_covariance[StateAccelerationBias, StateAccelerationBias] = _config.InitialAccelerationBiasVariance;
			_historyHead = 0;
			_historyCount = 0;
			SaveSnapshot();
		}

		/// <summary>
		/// Replaces the state vector while preserving timestamp and covariance.
		/// </summary>
		public void SetState(float position, float velocity, float acceleration, float velocityBias, float accelerationBias)
		{
			_state.Set(position, velocity, acceleration, velocityBias, accelerationBias);
		}

		/// <summary>
		/// Advances the prediction model to the requested timestamp.
		/// </summary>
		/// <remarks>
		/// Prediction is split into small steps so unusually sparse or delayed input
		/// samples do not create a single large covariance jump.
		/// </remarks>
		public void PredictTo(ulong timeUs)
		{
			if (!IsInitialized || timeUs <= _timeUs) {
				return;
			}

			SaveSnapshot();
			PredictInternal(timeUs);
		}

		/// <summary>
		/// Advances the prediction model without recording a rewind snapshot.
		/// </summary>
		private void PredictInternal(ulong timeUs)
		{
			if (timeUs <= _timeUs) {
				return;
			}

			var remainingDt = (timeUs - _timeUs) * 1.0e-6f;
			while (remainingDt > 0f) {
				var dt = math.min(remainingDt, _config.MaxDt);
				PredictStep(dt);
				remainingDt -= dt;
			}
			_timeUs = timeUs;
		}

		/// <summary>
		/// Fuses a measured velocity sample.
		/// </summary>
		/// <remarks>
		/// The measurement model observes velocity plus velocity bias; keeping the
		/// bias term explicit lets the filter learn slow sensor drift instead of
		/// feeding it into cabinet displacement.
		/// </remarks>
		public void UpdateVelocity(ulong timeUs, float velocity)
		{
			if (!IsInitialized) {
				Reset(timeUs, 0f, 0f, 0f, velocity, 0f);
				_covariance[StateVelocityBias, StateVelocityBias] = _config.VelocityMeasurementVariance;
				UpdateLatestSnapshot();
				return;
			}

			var h = Vector5f.Zero;
			h[StateVelocity] = 1f;
			h[StateVelocityBias] = 1f;
			UpdateScalarMeasurementAt(timeUs, h, velocity, _config.VelocityMeasurementVariance);
		}

		/// <summary>
		/// Fuses a measured acceleration sample.
		/// </summary>
		/// <remarks>
		/// The measurement observes acceleration plus acceleration bias for the same
		/// reason as velocity: real cabinet accelerometers tend to have small DC
		/// offsets that should not become permanent cabinet force.
		/// </remarks>
		public void UpdateAcceleration(ulong timeUs, float acceleration)
		{
			if (!IsInitialized) {
				Reset(timeUs, 0f, 0f, 0f, 0f, acceleration);
				_covariance[StateAccelerationBias, StateAccelerationBias] = _config.AccelerationMeasurementVariance;
				UpdateLatestSnapshot();
				return;
			}

			var h = Vector5f.Zero;
			h[StateAcceleration] = 1f;
			h[StateAccelerationBias] = 1f;
			UpdateScalarMeasurementAt(timeUs, h, acceleration, _config.AccelerationMeasurementVariance);
		}

		/// <summary>
		/// Pulls the filter back toward rest when the synchronized sensor channels
		/// indicate the cabinet has settled.
		/// </summary>
		/// <remarks>
		/// These pseudo-measurements are what keep long-running tables from slowly
		/// drifting across the playfield after many tiny integrated errors.
		/// </remarks>
		public void UpdateRestConstraints(ulong timeUs, bool applyPositionConstraint = true,
			bool applyVelocityConstraint = true, bool applyAccelerationConstraint = true)
		{
			if (!IsInitialized) {
				return;
			}

			if (applyVelocityConstraint) {
				var h = Vector5f.Zero;
				h[StateVelocity] = 1f;
				UpdateScalarMeasurementAt(timeUs, h, 0f, _config.ZeroVelocityMeasurementVariance);
			}
			if (applyAccelerationConstraint) {
				var h = Vector5f.Zero;
				h[StateAcceleration] = 1f;
				UpdateScalarMeasurementAt(timeUs, h, 0f, _config.ZeroAccelerationMeasurementVariance);
			}
			if (applyPositionConstraint) {
				var h = Vector5f.Zero;
				h[StatePosition] = 1f;
				UpdateScalarMeasurementAt(timeUs, h, 0f, _config.ZeroPositionMeasurementVariance);
			}
		}

		/// <summary>
		/// Applies a measurement at its acquisition timestamp, rewinding when a
		/// delayed sample arrives after the filter has already predicted forward.
		/// </summary>
		/// <remarks>
		/// Ported from VP's rewind support in <c>MotionKalmanAxis.h</c>. Native
		/// input is sampled on a different thread than physics, so samples can reach
		/// the simulation thread slightly out of order. Replaying from the nearest
		/// saved snapshot keeps late samples from being applied to the wrong state.
		/// </remarks>
		private void UpdateScalarMeasurementAt(ulong timeUs, Vector5f h, float measurement, float measurementVariance)
		{
			if (timeUs >= _timeUs) {
				if (timeUs > _timeUs) {
					PredictTo(timeUs);
				}
				UpdateScalarMeasurement(h, measurement, measurementVariance);
				return;
			}

			var presentUs = _timeUs;
			if (!RestoreSnapshot(timeUs)) {
				UpdateScalarMeasurement(h, measurement, measurementVariance);
			} else {
				if (_timeUs < timeUs) {
					PredictInternal(timeUs);
				}
				UpdateScalarMeasurement(h, measurement, measurementVariance);
			}

			if (_timeUs < presentUs) {
				SaveSnapshot();
				PredictInternal(presentUs);
			}
		}

		/// <summary>
		/// Runs one bounded prediction step.
		/// </summary>
		private void PredictStep(float dt)
		{
			dt = math.max(dt, _config.MinDt);
			var f = BuildTransitionMatrix(dt);
			var ft = Transposed(f);
			var q = BuildProcessNoiseMatrix(dt);
			_state = Mul(f, _state);
			_covariance = Add(Mul(Mul(f, _covariance), ft), q);
			Symmetrize(ref _covariance);
		}

		/// <summary>
		/// Builds the constant-acceleration state transition matrix.
		/// </summary>
		/// <remarks>
		/// Bias terms decay toward zero with a configurable mean-reversion time so
		/// stale bias estimates fade if the sensor behavior changes.
		/// </remarks>
		private Matrix5f BuildTransitionMatrix(float dt)
		{
			var dt2 = dt * dt;
			var f = Matrix5f.Identity;
			f[StatePosition, StateVelocity] = dt;
			f[StatePosition, StateAcceleration] = 0.5f * dt2;
			f[StateVelocity, StateAcceleration] = dt;

			var tau = _config.BiasMeanReversionTimeS;
			var alpha = tau > 0f ? math.exp(-dt / tau) : 1f;
			f[StateVelocityBias, StateVelocityBias] = alpha;
			f[StateAccelerationBias, StateAccelerationBias] = alpha;
			return f;
		}

		/// <summary>
		/// Builds process noise for jerk-driven motion plus independent bias drift.
		/// </summary>
		private Matrix5f BuildProcessNoiseMatrix(float dt)
		{
			var qj = _config.ProcessJerkVariance;
			var qbv = _config.VelocityBiasProcessVariance;
			var qba = _config.AccelerationBiasProcessVariance;
			var dt2 = dt * dt;
			var dt3 = dt2 * dt;
			var dt4 = dt3 * dt;
			var dt5 = dt4 * dt;

			var q = Matrix5f.Zero;
			q[StatePosition, StatePosition] = qj * (dt5 / 20f);
			q[StatePosition, StateVelocity] = qj * (dt4 / 8f);
			q[StatePosition, StateAcceleration] = qj * (dt3 / 6f);
			q[StateVelocity, StatePosition] = qj * (dt4 / 8f);
			q[StateVelocity, StateVelocity] = qj * (dt3 / 3f);
			q[StateVelocity, StateAcceleration] = qj * (dt2 / 2f);
			q[StateAcceleration, StatePosition] = qj * (dt3 / 6f);
			q[StateAcceleration, StateVelocity] = qj * (dt2 / 2f);
			q[StateAcceleration, StateAcceleration] = qj * dt;
			q[StateVelocityBias, StateVelocityBias] = qbv * dt;
			q[StateAccelerationBias, StateAccelerationBias] = qba * dt;
			return q;
		}

		/// <summary>
		/// Applies one scalar Kalman measurement update.
		/// </summary>
		/// <remarks>
		/// Uses the Joseph covariance form, which costs a little more math but keeps
		/// the covariance matrix better behaved after many updates in single
		/// precision.
		/// </remarks>
		private void UpdateScalarMeasurement(Vector5f h, float measurement, float measurementVariance)
		{
			var predictedMeasurement = Dot(h, _state);
			var innovation = measurement - predictedMeasurement;

			var pht = Vector5f.Zero;
			for (var i = 0; i < StateCount; i++) {
				var sum = 0f;
				for (var j = 0; j < StateCount; j++) {
					sum += _covariance[i, j] * h[j];
				}
				pht[i] = sum;
			}

			var s = Dot(h, pht) + measurementVariance;
			if (s <= 0f) {
				return;
			}

			var invS = 1f / s;
			var k = Vector5f.Zero;
			for (var i = 0; i < StateCount; i++) {
				k[i] = pht[i] * invS;
				_state[i] += k[i] * innovation;
			}

			var a = Matrix5f.Identity;
			for (var i = 0; i < StateCount; i++) {
				for (var j = 0; j < StateCount; j++) {
					a[i, j] -= k[i] * h[j];
				}
			}

			var at = Transposed(a);
			var apAt = Mul(Mul(a, _covariance), at);
			var krKt = OuterProduct(k, k, measurementVariance);
			_covariance = Add(apAt, krKt);
			Symmetrize(ref _covariance);
		}

		private int EffectiveHistoryCapacity()
		{
			if (_config.HistoryCapacity <= 0) {
				return MaxHistoryCapacity;
			}
			return _config.HistoryCapacity > MaxHistoryCapacity ? MaxHistoryCapacity : _config.HistoryCapacity;
		}

		/// <summary>
		/// Saves the current state before predicting forward, giving late samples a
		/// point from which the filter can replay.
		/// </summary>
		private void SaveSnapshot()
		{
			var cap = EffectiveHistoryCapacity();
			if (_historyHead >= cap || _historyCount > cap) {
				_historyHead = 0;
				_historyCount = 0;
			}

			var slot = _historyHead;
			fixed (ulong* historyTimeUs = _historyTimeUs)
			fixed (float* historyState = _historyState)
			fixed (float* historyCovariance = _historyCovariance) {
				historyTimeUs[slot] = _timeUs;
				var stateOffset = slot * StateCount;
				for (var i = 0; i < StateCount; i++) {
					historyState[stateOffset + i] = _state[i];
				}

				var covarianceOffset = slot * HistoryCovarianceStride;
				for (var row = 0; row < StateCount; row++) {
					for (var column = 0; column < StateCount; column++) {
						historyCovariance[covarianceOffset + row * StateCount + column] = _covariance[row, column];
					}
				}
			}

			_historyHead = (_historyHead + 1) % cap;
			if (_historyCount < cap) {
				_historyCount++;
			}
		}

		/// <summary>
		/// Updates the snapshot created by <see cref="Reset"/> after initialization
		/// has tightened a bias covariance from the first measured sample.
		/// </summary>
		private void UpdateLatestSnapshot()
		{
			if (_historyCount == 0) {
				return;
			}

			var cap = EffectiveHistoryCapacity();
			if (_historyHead >= cap) {
				return;
			}

			var slot = PositiveMod(_historyHead - 1, cap);
			fixed (float* historyState = _historyState)
			fixed (float* historyCovariance = _historyCovariance) {
				var stateOffset = slot * StateCount;
				for (var i = 0; i < StateCount; i++) {
					historyState[stateOffset + i] = _state[i];
				}

				var covarianceOffset = slot * HistoryCovarianceStride;
				for (var row = 0; row < StateCount; row++) {
					for (var column = 0; column < StateCount; column++) {
						historyCovariance[covarianceOffset + row * StateCount + column] = _covariance[row, column];
					}
				}
			}
		}

		/// <summary>
		/// Restores the latest saved state at or before the requested timestamp.
		/// </summary>
		private bool RestoreSnapshot(ulong targetUs)
		{
			var cap = EffectiveHistoryCapacity();
			if (_historyCount <= 0 || _historyHead >= cap) {
				return false;
			}

			var count = _historyCount > cap ? cap : _historyCount;
			var bestSlot = -1;
			var bestTimeUs = 0UL;

			fixed (ulong* historyTimeUs = _historyTimeUs)
			fixed (float* historyState = _historyState)
			fixed (float* historyCovariance = _historyCovariance) {
				for (var i = 0; i < count; i++) {
					var slot = PositiveMod(_historyHead - 1 - i, cap);
					var snapshotTimeUs = historyTimeUs[slot];
					if (snapshotTimeUs <= targetUs) {
						bestSlot = slot;
						bestTimeUs = snapshotTimeUs;
						break;
					}
				}

				if (bestSlot == -1) {
					bestSlot = PositiveMod(_historyHead - count, cap);
					bestTimeUs = historyTimeUs[bestSlot];
				}

				var stateOffset = bestSlot * StateCount;
				_state.Set(
					historyState[stateOffset + StatePosition],
					historyState[stateOffset + StateVelocity],
					historyState[stateOffset + StateAcceleration],
					historyState[stateOffset + StateVelocityBias],
					historyState[stateOffset + StateAccelerationBias]);

				var covarianceOffset = bestSlot * HistoryCovarianceStride;
				for (var row = 0; row < StateCount; row++) {
					for (var column = 0; column < StateCount; column++) {
						_covariance[row, column] = historyCovariance[covarianceOffset + row * StateCount + column];
					}
				}
			}

			_timeUs = bestTimeUs;

			var keepCount = 0;
			for (var i = 0; i < count; i++) {
				var slot = PositiveMod(_historyHead - count + i, cap);
				keepCount = i + 1;
				if (slot == bestSlot) {
					break;
				}
			}

			_historyCount = keepCount;
			_historyHead = (bestSlot + 1) % cap;
			return true;
		}

		private static int PositiveMod(int value, int modulo)
		{
			var result = value % modulo;
			return result < 0 ? result + modulo : result;
		}

		private struct Vector5f
		{
			private FixedList32Bytes<float> _values;

			public static Vector5f Zero
			{
				get {
					var value = new Vector5f();
					value.SetZero();
					return value;
				}
			}

			public float this[int index]
			{
				get {
					Ensure();
					return _values[index];
				}
				set {
					Ensure();
					_values[index] = value;
				}
			}

			public void Set(float position, float velocity, float acceleration, float velocityBias, float accelerationBias)
			{
				Ensure();
				_values[StatePosition] = position;
				_values[StateVelocity] = velocity;
				_values[StateAcceleration] = acceleration;
				_values[StateVelocityBias] = velocityBias;
				_values[StateAccelerationBias] = accelerationBias;
			}

			public void SetZero()
			{
				Ensure();
				for (var i = 0; i < StateCount; i++) {
					_values[i] = 0f;
				}
			}

			private void Ensure()
			{
				if (_values.Length != StateCount) {
					_values.Length = StateCount;
				}
			}
		}

		private struct Matrix5f
		{
			private FixedList128Bytes<float> _values;

			public static Matrix5f Zero
			{
				get {
					var value = new Matrix5f();
					value.SetZero();
					return value;
				}
			}

			public static Matrix5f Identity
			{
				get {
					var value = Zero;
					for (var i = 0; i < StateCount; i++) {
						value[i, i] = 1f;
					}
					return value;
				}
			}

			public float this[int row, int column]
			{
				get {
					Ensure();
					return _values[row * StateCount + column];
				}
				set {
					Ensure();
					_values[row * StateCount + column] = value;
				}
			}

			public void SetZero()
			{
				Ensure();
				for (var i = 0; i < StateCount * StateCount; i++) {
					_values[i] = 0f;
				}
			}

			private void Ensure()
			{
				if (_values.Length != StateCount * StateCount) {
					_values.Length = StateCount * StateCount;
				}
			}
		}

		private static Matrix5f Transposed(Matrix5f m)
		{
			var t = Matrix5f.Zero;
			for (var i = 0; i < StateCount; i++) {
				for (var j = 0; j < StateCount; j++) {
					t[i, j] = m[j, i];
				}
			}
			return t;
		}

		private static Matrix5f Add(Matrix5f a, Matrix5f b)
		{
			var r = Matrix5f.Zero;
			for (var i = 0; i < StateCount; i++) {
				for (var j = 0; j < StateCount; j++) {
					r[i, j] = a[i, j] + b[i, j];
				}
			}
			return r;
		}

		private static Matrix5f Mul(Matrix5f a, Matrix5f b)
		{
			var r = Matrix5f.Zero;
			for (var i = 0; i < StateCount; i++) {
				for (var j = 0; j < StateCount; j++) {
					var sum = 0f;
					for (var k = 0; k < StateCount; k++) {
						sum += a[i, k] * b[k, j];
					}
					r[i, j] = sum;
				}
			}
			return r;
		}

		private static Vector5f Mul(Matrix5f a, Vector5f v)
		{
			var r = Vector5f.Zero;
			for (var i = 0; i < StateCount; i++) {
				var sum = 0f;
				for (var j = 0; j < StateCount; j++) {
					sum += a[i, j] * v[j];
				}
				r[i] = sum;
			}
			return r;
		}

		private static Matrix5f OuterProduct(Vector5f a, Vector5f b, float scale)
		{
			var r = Matrix5f.Zero;
			for (var i = 0; i < StateCount; i++) {
				for (var j = 0; j < StateCount; j++) {
					r[i, j] = scale * a[i] * b[j];
				}
			}
			return r;
		}

		private static float Dot(Vector5f a, Vector5f b)
		{
			var sum = 0f;
			for (var i = 0; i < StateCount; i++) {
				sum += a[i] * b[i];
			}
			return sum;
		}

		private static void Symmetrize(ref Matrix5f m)
		{
			for (var i = 0; i < StateCount; i++) {
				for (var j = i + 1; j < StateCount; j++) {
					var value = 0.5f * (m[i, j] + m[j, i]);
					m[i, j] = value;
					m[j, i] = value;
				}
			}
		}
	}
}
