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
	public struct NudgeSensorRuntimeConfig
	{
		public NudgeSensorType Type;
		public float Strength;
		public float CabinetMassKg;
		public NudgeSensorMountRotation MountRotation;
		public byte MountMirror;
		public byte XMapped;
		public byte YMapped;
		public byte VelocityXMapped;
		public byte VelocityYMapped;
		public byte AccelerationXMapped;
		public byte AccelerationYMapped;
	}

	public struct NudgePhysicsSensorState
	{
		public byte Mapped;
		public SensorMappingKind Kind;
		public float Value;
		public ulong TimestampUsec;

		public bool IsMapped => Mapped != 0;

		public void Configure(bool mapped, SensorMappingKind kind)
		{
			Mapped = mapped ? (byte)1 : (byte)0;
			Kind = kind;
			Value = 0f;
			TimestampUsec = 0;
		}

		public void SetValue(float value, ulong timestampUsec)
		{
			Value = value;
			TimestampUsec = timestampUsec;
		}
	}

	public struct GamepadNudgeState
	{
		private const int DeactivationDelayMs = 10000;

		public NudgePhysicsSensorState XSensor;
		public NudgePhysicsSensorState YSensor;
		public NudgeIntentState Intent;
		public CabinetPhysicsState Cabinet;
		public float Strength;
		public float2 CabinetAcceleration;
		public float2 CabinetOffset;

		private int _deactivationDelay;

		public GamepadNudgeState(float strength, bool xMapped, bool yMapped)
		{
			XSensor = default;
			YSensor = default;
			XSensor.Configure(xMapped, SensorMappingKind.Position);
			YSensor.Configure(yMapped, SensorMappingKind.Position);
			Intent = new NudgeIntentState(true);
			Cabinet = CabinetPhysicsState.Default;
			Strength = math.clamp(strength, 0f, 2f);
			CabinetAcceleration = float2.zero;
			CabinetOffset = float2.zero;
			_deactivationDelay = 0;
		}

		public bool IsActive => _deactivationDelay > 0;

		public void ApplySample(NudgeSensorChannel channel, float value, ulong timestampUsec)
		{
			switch (channel) {
				case NudgeSensorChannel.X:
					XSensor.SetValue(value, timestampUsec);
					break;
				case NudgeSensorChannel.Y:
					YSensor.SetValue(value, timestampUsec);
					break;
			}
		}

		public void StepOneMillisecond()
		{
			var x = XSensor.Value * Strength * 16f;
			var y = YSensor.Value * Strength * 12f;
			Intent.StepOneMillisecond(new float2(x, y));

			if (Intent.IsImpulseInProgress) {
				Cabinet.StepOneMillisecond(Cabinet.Mass * Intent.ImpulseAcceleration);
				_deactivationDelay = DeactivationDelayMs;
			} else {
				Cabinet.StepOneMillisecond(float2.zero);
				if (_deactivationDelay > 0) {
					_deactivationDelay--;
				}
			}

			CabinetAcceleration = Cabinet.CabinetAcceleration;
			CabinetOffset = Cabinet.CabinetOffset;
		}
	}

	public struct CabinetSensorState
	{
		private const int DeactivationDelayMs = 10000;
		private const float GainConfidenceThreshold = 0.5f;
		private const int CrossRestCountThreshold = 75;
		private const int ForceRestCountThreshold = CrossRestCountThreshold + 200;

		private struct SyncedSensor
		{
			public NudgePhysicsSensorState Sensor;
			public ulong LastTimestampUsec;
			public long ClockDeltaUsec;
			public int RestCount;
			public byte ForceRest;
			public float LastValue;

			public void Configure(bool mapped, SensorMappingKind kind)
			{
				Sensor.Configure(mapped, kind);
				LastTimestampUsec = 0;
				ClockDeltaUsec = 0;
				RestCount = 0;
				ForceRest = 0;
				LastValue = 0f;
			}
		}

		public float Strength;
		public float CabinetMassKg;
		public MotionKalmanAxis KalmanX;
		public MotionKalmanAxis KalmanY;
		public MotionGainCalibratorAxis GainCalibratorX;
		public MotionGainCalibratorAxis GainCalibratorY;
		public CabinetPhysicsState Cabinet;
		public float2 CabinetAcceleration;
		public float2 CabinetOffset;
		public int DeactivationDelay => _deactivationDelay;
		public bool IsIntentSensor => _intentEnabled != 0;
		public bool IsActive => _deactivationDelay > 0;
		public float2 KalmanAcceleration => new(KalmanX.Acceleration, KalmanY.Acceleration);
		public float2 Gain => new(GainCalibratorX.Gain, GainCalibratorY.Gain);
		public float2 GainConfidence => new(GainCalibratorX.GlobalConfidence, GainCalibratorY.GlobalConfidence);

		private SyncedSensor _xVelSensor;
		private SyncedSensor _yVelSensor;
		private SyncedSensor _xAccSensor;
		private SyncedSensor _yAccSensor;
		private EmaState _emaX;
		private EmaState _emaY;
		private ulong _timeUs;
		private NudgeIntentState _intent;
		private byte _intentEnabled;
		private int _deactivationDelay;

		public CabinetSensorState(NudgeSensorType type, float strength, float cabinetMassKg,
			bool velXMapped, bool velYMapped, bool accXMapped, bool accYMapped)
		{
			Strength = math.clamp(strength, 0f, 2f);
			CabinetMassKg = math.clamp(cabinetMassKg, 0f, 200f);
			KalmanX = MotionKalmanAxis.Default;
			KalmanY = MotionKalmanAxis.Default;
			GainCalibratorX = MotionGainCalibratorAxis.Default;
			GainCalibratorY = MotionGainCalibratorAxis.Default;
			Cabinet = CabinetPhysicsState.Default;
			CabinetAcceleration = float2.zero;
			CabinetOffset = float2.zero;
			_xVelSensor = default;
			_yVelSensor = default;
			_xAccSensor = default;
			_yAccSensor = default;
			_xVelSensor.Configure(velXMapped, SensorMappingKind.Velocity);
			_yVelSensor.Configure(velYMapped, SensorMappingKind.Velocity);
			_xAccSensor.Configure(accXMapped, SensorMappingKind.Acceleration);
			_yAccSensor.Configure(accYMapped, SensorMappingKind.Acceleration);
			_emaX = new EmaState(0.004f);
			_emaY = new EmaState(0.004f);
			_timeUs = 0;
			_intent = new NudgeIntentState(false);
			_intentEnabled = type == NudgeSensorType.CabinetIntent ? (byte)1 : (byte)0;
			_deactivationDelay = 0;
		}

		public void ApplySample(NudgeSensorChannel channel, float value, ulong timestampUsec)
		{
			switch (channel) {
				case NudgeSensorChannel.VelocityX:
					_xVelSensor.Sensor.SetValue(value, timestampUsec);
					break;
				case NudgeSensorChannel.VelocityY:
					_yVelSensor.Sensor.SetValue(value, timestampUsec);
					break;
				case NudgeSensorChannel.AccelerationX:
					_xAccSensor.Sensor.SetValue(value, timestampUsec);
					break;
				case NudgeSensorChannel.AccelerationY:
					_yAccSensor.Sensor.SetValue(value, timestampUsec);
					break;
			}
		}

		public void StepOneMillisecond()
		{
			if (_deactivationDelay > 0) {
				_deactivationDelay--;
			}

			_timeUs += 1000;
			UpdateAxis(ref _xVelSensor, ref _xAccSensor, ref KalmanX, ref GainCalibratorX);
			UpdateAxis(ref _yVelSensor, ref _yAccSensor, ref KalmanY, ref GainCalibratorY);

			if (_intentEnabled != 0) {
				_intent.StepOneMillisecond(new float2(KalmanX.Acceleration * (4f / 3f), KalmanY.Acceleration));
				if (_intent.IsImpulseInProgress) {
					Cabinet.StepOneMillisecond(Cabinet.Mass * _intent.ImpulseAcceleration);
					_deactivationDelay = DeactivationDelayMs;
				} else {
					Cabinet.StepOneMillisecond(float2.zero);
				}
				CabinetAcceleration = Cabinet.CabinetAcceleration;
			} else {
				// Note: the reference (CabinetNudgeSensor.cpp:280-285) multiplies by the
				// strength scale twice, making direct-mode output scale with strength².
				// That's an upstream bug we deliberately don't reproduce: strength is
				// applied once, linearly.
				CabinetAcceleration.x = _emaX.Update(KalmanX.Acceleration, 0.001f);
				CabinetAcceleration.y = _emaY.Update(KalmanY.Acceleration, 0.001f);
				CabinetAcceleration *= Strength * CabinetMassKg / Cabinet.Mass;
				Cabinet.StepOneMillisecond(Cabinet.Mass * CabinetAcceleration);
			}
			CabinetOffset = Cabinet.CabinetOffset;
		}

		private void UpdateAxis(ref SyncedSensor velSensor, ref SyncedSensor accSensor, ref MotionKalmanAxis kalmanFilter,
			ref MotionGainCalibratorAxis gainCalibrator)
		{
			var isAccAndVelMapped = accSensor.Sensor.IsMapped && velSensor.Sensor.IsMapped;
			if (isAccAndVelMapped) {
				if (gainCalibrator.GlobalConfidence < GainConfidenceThreshold || gainCalibrator.Gain < 0.01f) {
					UpdateAxisSensor(ref accSensor, ref kalmanFilter, 1f);
					velSensor.RestCount = accSensor.RestCount;
				} else if (accSensor.Sensor.TimestampUsec < velSensor.Sensor.TimestampUsec) {
					UpdateAxisSensor(ref accSensor, ref kalmanFilter, 1f);
					UpdateAxisSensor(ref velSensor, ref kalmanFilter, 1f / gainCalibrator.Gain);
				} else {
					UpdateAxisSensor(ref velSensor, ref kalmanFilter, 1f / gainCalibrator.Gain);
					UpdateAxisSensor(ref accSensor, ref kalmanFilter, 1f);
				}
			} else {
				UpdateAxisSensor(ref accSensor, ref kalmanFilter, 1f);
				UpdateAxisSensor(ref velSensor, ref kalmanFilter, 1f);
			}

			accSensor.ForceRest = (byte)(accSensor.ForceRest != 0
				|| (accSensor.RestCount > CrossRestCountThreshold && accSensor.LastValue * accSensor.Sensor.Value < 0f) ? 1 : 0);
			velSensor.ForceRest = (byte)(velSensor.ForceRest != 0
				|| (velSensor.RestCount > CrossRestCountThreshold && velSensor.LastValue * velSensor.Sensor.Value < 0f) ? 1 : 0);

			var isRest = (accSensor.ForceRest != 0 || accSensor.RestCount > ForceRestCountThreshold)
				&& (velSensor.ForceRest != 0 || velSensor.RestCount > ForceRestCountThreshold);
			if (isRest) {
				velSensor.ForceRest = 1;
				accSensor.ForceRest = 1;
				kalmanFilter.UpdateRestConstraints(_timeUs);
			}

			accSensor.LastValue = accSensor.Sensor.Value;
			velSensor.LastValue = velSensor.Sensor.Value;

			if (isAccAndVelMapped) {
				if (!isRest) {
					if (!gainCalibrator.IsSegmentActive) {
						gainCalibrator.StartSegment(_timeUs);
					}
					gainCalibrator.AddSample(_timeUs, velSensor.Sensor.Value, accSensor.Sensor.Value);
				} else if (gainCalibrator.IsSegmentActive) {
					gainCalibrator.EndSegment();
				}
			}

			kalmanFilter.PredictTo(_timeUs);
		}

		private void UpdateAxisSensor(ref SyncedSensor sensor, ref MotionKalmanAxis axis, float axisGain)
		{
			if (!sensor.Sensor.IsMapped) {
				sensor.RestCount = DeactivationDelayMs;
				return;
			}

			var restThreshold = sensor.Sensor.Kind == SensorMappingKind.Acceleration ? 0.020f : 0.002f;
			if (math.abs(sensor.Sensor.Value) < restThreshold) {
				sensor.RestCount++;
				if (sensor.ForceRest != 0) {
					return;
				}
			} else {
				sensor.RestCount = 0;
				sensor.ForceRest = 0;
				_deactivationDelay = DeactivationDelayMs;
			}

			var timestampUsec = sensor.Sensor.TimestampUsec;
			if (timestampUsec == 0 || timestampUsec == sensor.LastTimestampUsec) {
				return;
			}

			sensor.LastTimestampUsec = timestampUsec;
			var alignedTimestampUsec = (long)timestampUsec + sensor.ClockDeltaUsec;
			if (alignedTimestampUsec > (long)_timeUs) {
				sensor.ClockDeltaUsec = (long)_timeUs - (long)timestampUsec;
				alignedTimestampUsec = (long)_timeUs;
			}
			if (alignedTimestampUsec < 0) {
				alignedTimestampUsec = 0;
			}

			if (sensor.Sensor.Kind == SensorMappingKind.Velocity) {
				axis.UpdateVelocity((ulong)alignedTimestampUsec, axisGain * sensor.Sensor.Value);
			} else if (sensor.Sensor.Kind == SensorMappingKind.Acceleration) {
				axis.UpdateAcceleration((ulong)alignedTimestampUsec, axisGain * sensor.Sensor.Value);
			}
		}

		private struct EmaState
		{
			private float _tau;
			private float _value;
			private byte _initialized;

			public EmaState(float tau)
			{
				_tau = math.max(1.0e-6f, tau);
				_value = 0f;
				_initialized = 0;
			}

			public float Update(float sample, float dt)
			{
				if (_initialized == 0) {
					_value = sample;
					_initialized = 1;
					return _value;
				}

				var alpha = 1f - math.exp(-dt / _tau);
				_value += alpha * (sample - _value);
				return _value;
			}
		}
	}

	public struct NudgeSensorState
	{
		public NudgeSensorType Type;
		public byte Enabled;
		public NudgeSensorMountRotation MountRotation;
		public byte MountMirror;
		public GamepadNudgeState Gamepad;
		public CabinetSensorState Cabinet;
		public float2 CabinetAcceleration;
		public float2 CabinetOffset;
		public ulong LastActivityTimestampUsec;

		public bool IsEnabled => Enabled != 0;
		public bool IsActive => IsEnabled && (Type == NudgeSensorType.GamepadIntent ? Gamepad.IsActive : Cabinet.IsActive);

		public NudgeSensorState(NudgeSensorRuntimeConfig config)
		{
			Type = config.Type;
			Enabled = 1;
			MountRotation = NudgeSensorMountTransform.NormalizeRotation(config.MountRotation);
			MountMirror = config.MountMirror;
			var xMapped = config.XMapped != 0;
			var yMapped = config.YMapped != 0;
			var velXMapped = config.VelocityXMapped != 0;
			var velYMapped = config.VelocityYMapped != 0;
			var accXMapped = config.AccelerationXMapped != 0;
			var accYMapped = config.AccelerationYMapped != 0;
			NudgeSensorMountTransform.TransformMappedAxes(ref xMapped, ref yMapped, MountRotation);
			NudgeSensorMountTransform.TransformMappedAxes(ref velXMapped, ref velYMapped, MountRotation);
			NudgeSensorMountTransform.TransformMappedAxes(ref accXMapped, ref accYMapped, MountRotation);
			Gamepad = new GamepadNudgeState(config.Strength, xMapped, yMapped);
			Cabinet = new CabinetSensorState(config.Type, config.Strength, config.CabinetMassKg,
				velXMapped, velYMapped,
				accXMapped, accYMapped);
			CabinetAcceleration = float2.zero;
			CabinetOffset = float2.zero;
			LastActivityTimestampUsec = 0;
		}

		public void Disable()
		{
			Enabled = 0;
			CabinetAcceleration = float2.zero;
			CabinetOffset = float2.zero;
			LastActivityTimestampUsec = 0;
		}

		public void ApplySample(NudgeSensorChannel channel, float value, ulong timestampUsec)
		{
			if (!IsEnabled) {
				return;
			}
			NudgeSensorMountTransform.TransformChannel(ref channel, ref value, MountRotation, MountMirror != 0);
			if (math.abs(value) > 1.0e-6f && timestampUsec > LastActivityTimestampUsec) {
				LastActivityTimestampUsec = timestampUsec;
			}
			if (Type == NudgeSensorType.GamepadIntent) {
				Gamepad.ApplySample(channel, value, timestampUsec);
			} else {
				Cabinet.ApplySample(channel, value, timestampUsec);
			}
		}

		public void StepOneMillisecond()
		{
			if (!IsEnabled) {
				CabinetAcceleration = float2.zero;
				CabinetOffset = float2.zero;
				return;
			}

			if (Type == NudgeSensorType.GamepadIntent) {
				Gamepad.StepOneMillisecond();
				CabinetAcceleration = Gamepad.CabinetAcceleration;
				CabinetOffset = Gamepad.CabinetOffset;
			} else {
				Cabinet.StepOneMillisecond();
				CabinetAcceleration = Cabinet.CabinetAcceleration;
				CabinetOffset = Cabinet.CabinetOffset;
			}
		}
	}
}
