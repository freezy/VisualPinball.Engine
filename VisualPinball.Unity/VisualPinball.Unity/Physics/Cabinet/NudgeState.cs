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

	public struct NudgeState
	{
		public KeyboardNudgeState Keyboard;
		public float2 CabinetAcceleration;
		public float2 CabinetOffset;
		public float2 MaxCabinetAcceleration;
		public int KeyboardNudgeIndex;

		public NudgeState(KeyboardNudgeMode keyboardMode, float keyboardStrength, float nudgeTime)
		{
			Keyboard = new KeyboardNudgeState(keyboardMode, keyboardStrength, nudgeTime);
			CabinetAcceleration = float2.zero;
			CabinetOffset = float2.zero;
			MaxCabinetAcceleration = float2.zero;
			KeyboardNudgeIndex = 0;
		}

		public void ApplyKeyboardImpulse(float angleDeg, float force)
		{
			KeyboardNudgeIndex++;
			Keyboard.Nudge(angleDeg, force);
		}

		public void StepOneMillisecond()
		{
			Keyboard.StepOneMillisecond();

			if (Keyboard.IsActive) {
				CabinetAcceleration = Keyboard.CabinetAcceleration;
				CabinetOffset = Keyboard.CabinetOffset;
			} else {
				CabinetAcceleration = float2.zero;
				CabinetOffset = float2.zero;
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
	}
}
