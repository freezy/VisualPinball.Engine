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
	internal readonly struct KeyboardNudgeCommand
	{
		public readonly float AngleDeg;
		public readonly float Force;

		public KeyboardNudgeCommand(float angleDeg, float force)
		{
			AngleDeg = angleDeg;
			Force = force;
		}
	}

	internal readonly struct NudgeSensorSampleCommand
	{
		public readonly int SensorIndex;
		public readonly NudgeSensorChannel Channel;
		public readonly float Value;
		public readonly ulong TimestampUsec;

		public NudgeSensorSampleCommand(int sensorIndex, NudgeSensorChannel channel, float value, ulong timestampUsec)
		{
			SensorIndex = sensorIndex;
			Channel = channel;
			Value = value;
			TimestampUsec = timestampUsec;
		}
	}

	public struct NudgeState
	{
		public const int MaxSensors = 4;

		public KeyboardNudgeState Keyboard;
		public NudgeSensorState Sensor0;
		public NudgeSensorState Sensor1;
		public NudgeSensorState Sensor2;
		public NudgeSensorState Sensor3;
		public int SensorCount;
		public int ActiveSourceIndex;
		public float2 CabinetAcceleration;
		public float2 CabinetOffset;
		public float2 MaxCabinetAcceleration;
		public int KeyboardNudgeIndex;

		public NudgeState(KeyboardNudgeMode keyboardMode, float keyboardStrength, float nudgeTime,
			float keyboardCabinetDamping = CabinetPhysicsState.DefaultKeyboardDampingRatio)
		{
			Keyboard = new KeyboardNudgeState(keyboardMode, keyboardStrength, nudgeTime, keyboardCabinetDamping);
			CabinetAcceleration = float2.zero;
			CabinetOffset = float2.zero;
			MaxCabinetAcceleration = float2.zero;
			KeyboardNudgeIndex = 0;
			Sensor0 = default;
			Sensor1 = default;
			Sensor2 = default;
			Sensor3 = default;
			SensorCount = 0;
			ActiveSourceIndex = -2;
		}

		public void ApplyKeyboardImpulse(float angleDeg, float force)
		{
			KeyboardNudgeIndex++;
			Keyboard.Nudge(angleDeg, force);
		}

		public void ConfigureSensors(int count)
		{
			SensorCount = math.clamp(count, 0, MaxSensors);
			for (var i = SensorCount; i < MaxSensors; i++) {
				DisableSensor(i);
			}
		}

		public void ConfigureSensor(int index, NudgeSensorRuntimeConfig config)
		{
			if ((uint)index >= MaxSensors) {
				return;
			}
			config.Strength = math.clamp(config.Strength, 0f, 2f);
			config.CabinetMassKg = math.clamp(config.CabinetMassKg <= 0f ? 113f : config.CabinetMassKg, 0f, 200f);
			config.MountRotation = NudgeSensorMountTransform.NormalizeRotation(config.MountRotation);
			SetSensor(index, new NudgeSensorState(config));
			if (index >= SensorCount) {
				SensorCount = index + 1;
			}
		}

		public void ApplySensorSample(int sensorIndex, NudgeSensorChannel channel, float value, ulong timestampUsec)
		{
			if ((uint)sensorIndex >= SensorCount) {
				return;
			}
			var sensor = GetSensor(sensorIndex);
			sensor.ApplySample(channel, value, timestampUsec);
			SetSensor(sensorIndex, sensor);
		}

		public void StepOneMillisecond()
		{
			Keyboard.StepOneMillisecond();
			StepSensor(0);
			StepSensor(1);
			StepSensor(2);
			StepSensor(3);

			if (Keyboard.IsActive) {
				CabinetAcceleration = Keyboard.CabinetAcceleration;
				CabinetOffset = Keyboard.CabinetOffset;
				ActiveSourceIndex = -1;
			} else if (TryGetActiveSensor(out var activeSensorIndex, out var activeSensor)) {
				CabinetAcceleration = activeSensor.CabinetAcceleration;
				CabinetOffset = activeSensor.CabinetOffset;
				ActiveSourceIndex = activeSensorIndex;
			} else {
				CabinetAcceleration = float2.zero;
				CabinetOffset = float2.zero;
				ActiveSourceIndex = -2;
			}

			if (math.abs(CabinetAcceleration.x) > math.abs(MaxCabinetAcceleration.x)) {
				MaxCabinetAcceleration.x = CabinetAcceleration.x;
			}
			if (math.abs(CabinetAcceleration.y) > math.abs(MaxCabinetAcceleration.y)) {
				MaxCabinetAcceleration.y = CabinetAcceleration.y;
			}
		}

		public float2 ReadAndResetMaxCabinetAcceleration()
		{
			var value = MaxCabinetAcceleration;
			MaxCabinetAcceleration = float2.zero;
			return value;
		}

		private void StepSensor(int index)
		{
			if (index >= SensorCount) {
				return;
			}
			var sensor = GetSensor(index);
			sensor.StepOneMillisecond();
			SetSensor(index, sensor);
		}

		private bool TryGetActiveSensor(out int index, out NudgeSensorState sensor)
		{
			for (var i = 0; i < SensorCount; i++) {
				var candidate = GetSensor(i);
				if (!candidate.IsActive) {
					continue;
				}
				index = i;
				sensor = candidate;
				return true;
			}
			index = -2;
			sensor = default;
			return false;
		}

		private void DisableSensor(int index)
		{
			var sensor = GetSensor(index);
			sensor.Disable();
			SetSensor(index, sensor);
		}

		private NudgeSensorState GetSensor(int index)
		{
			return index switch {
				0 => Sensor0,
				1 => Sensor1,
				2 => Sensor2,
				3 => Sensor3,
				_ => default
			};
		}

		private void SetSensor(int index, NudgeSensorState sensor)
		{
			switch (index) {
				case 0:
					Sensor0 = sensor;
					break;
				case 1:
					Sensor1 = sensor;
					break;
				case 2:
					Sensor2 = sensor;
					break;
				case 3:
					Sensor3 = sensor;
					break;
			}
		}
	}
}
