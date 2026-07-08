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
	/// Command queued by the main thread to apply one keyboard-driven cabinet
	/// impulse on the simulation thread.
	/// </summary>
	internal readonly struct KeyboardNudgeCommand
	{
		public readonly float AngleDeg;
		public readonly float Force;

		/// <summary>
		/// Creates a keyboard nudge command in playfield degrees.
		/// </summary>
		public KeyboardNudgeCommand(float angleDeg, float force)
		{
			AngleDeg = angleDeg;
			Force = force;
		}
	}

	/// <summary>
	/// Command queued by the input thread to deliver one mapped analog nudge
	/// sample to the simulation thread.
	/// </summary>
	internal readonly struct NudgeSensorSampleCommand
	{
		public readonly int SensorIndex;
		public readonly NudgeSensorChannel Channel;
		public readonly float Value;
		public readonly ulong TimestampUsec;

		/// <summary>
		/// Creates a timestamped sensor sample for the configured sensor slot.
		/// </summary>
		public NudgeSensorSampleCommand(int sensorIndex, NudgeSensorChannel channel, float value, ulong timestampUsec)
		{
			SensorIndex = sensorIndex;
			Channel = channel;
			Value = value;
			TimestampUsec = timestampUsec;
		}
	}

	/// <summary>
	/// Owns all cabinet nudge sources and exposes the single cabinet motion value
	/// consumed by the physics step.
	/// </summary>
	/// <remarks>
	/// This is the VPE-side coordinator for the VP cabinet nudge model, tying
	/// together ports/adaptations from
	/// <c>vpinball/src/physics/cabinet/NudgeHandler.*</c>,
	/// <c>KeyboardNudge.*</c>, <c>GamepadNudge.*</c>, and
	/// <c>CabinetNudgeSensor.*</c>. Keyboard input intentionally wins while its
	/// spring response is active; otherwise the most recently active analog sensor
	/// supplies the cabinet acceleration and visual offset.
	/// </remarks>
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

		/// <summary>
		/// Creates a nudge coordinator with keyboard nudging enabled and no analog
		/// sensors configured.
		/// </summary>
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

		/// <summary>
		/// Applies a manual nudge impulse and increments the keyboard nudge counter
		/// used by telemetry/UI.
		/// </summary>
		public void ApplyKeyboardImpulse(float angleDeg, float force)
		{
			KeyboardNudgeIndex++;
			Keyboard.Nudge(angleDeg, force);
		}

		/// <summary>
		/// Sets the number of analog sensor slots that should be considered active.
		/// </summary>
		public void ConfigureSensors(int count)
		{
			SensorCount = math.clamp(count, 0, MaxSensors);
			for (var i = SensorCount; i < MaxSensors; i++) {
				DisableSensor(i);
			}
		}

		/// <summary>
		/// Rebuilds one analog sensor slot from serialized/player configuration.
		/// </summary>
		public void ConfigureSensor(int index, NudgeSensorRuntimeConfig config)
		{
			if ((uint)index >= MaxSensors) {
				return;
			}
			config.Strength = math.clamp(config.Strength, 0f, 2f);
			config.CabinetMassKg = math.clamp(config.CabinetMassKg <= 0f ? 113f : config.CabinetMassKg, 0f, 200f);
			config.MountRotation = NudgeSensorMountTransform.NormalizeRotation(config.MountRotation);
			ConfigureSensorState(index, config);
			if (index >= SensorCount) {
				SensorCount = index + 1;
			}
		}

		/// <summary>
		/// Delivers one mapped native-input value to the selected sensor slot.
		/// </summary>
		public void ApplySensorSample(int sensorIndex, NudgeSensorChannel channel, float value, ulong timestampUsec)
		{
			if ((uint)sensorIndex >= SensorCount) {
				return;
			}
			ApplySensorSampleTo(sensorIndex, channel, value, timestampUsec);
		}

		/// <summary>
		/// Advances keyboard and sensor models one millisecond and selects the
		/// cabinet motion source for this physics tick.
		/// </summary>
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
			} else if (TryGetActiveSensor(out var activeSensorIndex, out var sensorAcceleration, out var sensorOffset)) {
				CabinetAcceleration = sensorAcceleration;
				CabinetOffset = sensorOffset;
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

		/// <summary>
		/// Returns the largest signed acceleration seen since the last read and
		/// clears the telemetry accumulator.
		/// </summary>
		public float2 ReadAndResetMaxCabinetAcceleration()
		{
			var value = MaxCabinetAcceleration;
			MaxCabinetAcceleration = float2.zero;
			return value;
		}

		/// <summary>
		/// Advances a configured sensor slot and writes the value back into the
		/// fixed field storage.
		/// </summary>
		private void StepSensor(int index)
		{
			if (index >= SensorCount) {
				return;
			}
			StepSensorState(index);
		}

		/// <summary>
		/// Finds the analog sensor that should drive cabinet motion this tick.
		/// </summary>
		/// <remarks>
		/// Later timestamps win so that a device the user is actively touching
		/// supersedes an older device that is still ringing down. Equal timestamps
		/// fall back to the stronger cabinet motion.
		/// </remarks>
		private bool TryGetActiveSensor(out int index, out float2 cabinetAcceleration, out float2 cabinetOffset)
		{
			var bestScore = -1f;
			var bestActivityTimestampUsec = 0UL;
			index = -2;
			cabinetAcceleration = float2.zero;
			cabinetOffset = float2.zero;

			if (SensorCount > 0) {
				ConsiderActiveSensor(0, ref Sensor0, ref index, ref bestScore, ref bestActivityTimestampUsec,
					ref cabinetAcceleration, ref cabinetOffset);
			}
			if (SensorCount > 1) {
				ConsiderActiveSensor(1, ref Sensor1, ref index, ref bestScore, ref bestActivityTimestampUsec,
					ref cabinetAcceleration, ref cabinetOffset);
			}
			if (SensorCount > 2) {
				ConsiderActiveSensor(2, ref Sensor2, ref index, ref bestScore, ref bestActivityTimestampUsec,
					ref cabinetAcceleration, ref cabinetOffset);
			}
			if (SensorCount > 3) {
				ConsiderActiveSensor(3, ref Sensor3, ref index, ref bestScore, ref bestActivityTimestampUsec,
					ref cabinetAcceleration, ref cabinetOffset);
			}
			return index >= 0;
		}

		private static void ConsiderActiveSensor(int candidateIndex, ref NudgeSensorState candidate, ref int bestIndex,
			ref float bestScore, ref ulong bestActivityTimestampUsec, ref float2 cabinetAcceleration, ref float2 cabinetOffset)
		{
			if (!candidate.IsActive) {
				return;
			}

			var score = SensorActivityScore(ref candidate);
			if (bestIndex >= 0) {
				if (candidate.LastActivityTimestampUsec < bestActivityTimestampUsec) {
					return;
				}
				if (candidate.LastActivityTimestampUsec == bestActivityTimestampUsec && score <= bestScore) {
					return;
				}
			}

			bestScore = score;
			bestActivityTimestampUsec = candidate.LastActivityTimestampUsec;
			bestIndex = candidateIndex;
			cabinetAcceleration = candidate.CabinetAcceleration;
			cabinetOffset = candidate.CabinetOffset;
		}

		/// <summary>
		/// Scores active sensors by physical output rather than raw input, so intent
		/// and direct sensors can be compared fairly.
		/// </summary>
		private static float SensorActivityScore(ref NudgeSensorState sensor)
		{
			var accelerationScore = math.lengthsq(sensor.CabinetAcceleration);
			return accelerationScore > 1.0e-9f ? accelerationScore : math.lengthsq(sensor.CabinetOffset);
		}

		private void DisableSensor(int index)
		{
			switch (index) {
				case 0:
					Sensor0.Disable();
					break;
				case 1:
					Sensor1.Disable();
					break;
				case 2:
					Sensor2.Disable();
					break;
				case 3:
					Sensor3.Disable();
					break;
			}
		}

		private void ConfigureSensorState(int index, NudgeSensorRuntimeConfig config)
		{
			switch (index) {
				case 0:
					Sensor0 = new NudgeSensorState(config);
					break;
				case 1:
					Sensor1 = new NudgeSensorState(config);
					break;
				case 2:
					Sensor2 = new NudgeSensorState(config);
					break;
				case 3:
					Sensor3 = new NudgeSensorState(config);
					break;
			}
		}

		private void ApplySensorSampleTo(int index, NudgeSensorChannel channel, float value, ulong timestampUsec)
		{
			switch (index) {
				case 0:
					Sensor0.ApplySample(channel, value, timestampUsec);
					break;
				case 1:
					Sensor1.ApplySample(channel, value, timestampUsec);
					break;
				case 2:
					Sensor2.ApplySample(channel, value, timestampUsec);
					break;
				case 3:
					Sensor3.ApplySample(channel, value, timestampUsec);
					break;
			}
		}

		private void StepSensorState(int index)
		{
			switch (index) {
				case 0:
					Sensor0.StepOneMillisecond();
					break;
				case 1:
					Sensor1.StepOneMillisecond();
					break;
				case 2:
					Sensor2.StepOneMillisecond();
					break;
				case 3:
					Sensor3.StepOneMillisecond();
					break;
			}
		}
	}
}
