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
	public struct CabinetPhysicsState
	{
		public const float DefaultKeyboardDampingRatio = 0.5f;
		public const float MinKeyboardDampingRatio = 0.05f;
		public const float MaxKeyboardDampingRatio = 1f;

		private const float XFrequencyHz = 9.3f;
		private const float YFrequencyHz = 5.8f;
		private const float XCalibratedDampingRatio = 0.052f;
		private const float YCalibratedDampingRatio = 0.055f;

		public float Mass;
		public DampedHarmonicOscillator X;
		public DampedHarmonicOscillator Y;
		public float2 CabinetAcceleration;
		public float2 CabinetOffset;

		public CabinetPhysicsState(float mass) : this(mass, XCalibratedDampingRatio, YCalibratedDampingRatio)
		{
		}

		private CabinetPhysicsState(float mass, float xDampingRatio, float yDampingRatio)
		{
			Mass = mass;
			X = new DampedHarmonicOscillator(mass, XFrequencyHz, xDampingRatio);
			Y = new DampedHarmonicOscillator(mass, YFrequencyHz, yDampingRatio);
			CabinetAcceleration = float2.zero;
			CabinetOffset = float2.zero;
		}

		public static CabinetPhysicsState Default => new(113f);

		public static CabinetPhysicsState Keyboard(float dampingRatio)
		{
			dampingRatio = math.clamp(dampingRatio, MinKeyboardDampingRatio, MaxKeyboardDampingRatio);
			return new CabinetPhysicsState(113f, dampingRatio, dampingRatio);
		}

		public void StepOneMillisecond(float2 force)
		{
			X.StepOneMillisecond(force.x);
			Y.StepOneMillisecond(force.y);
			CabinetAcceleration = new float2(X.Acceleration, Y.Acceleration);
			CabinetOffset = new float2(X.Displacement, Y.Displacement);
		}

		public void Reset()
		{
			X.Reset();
			Y.Reset();
			CabinetAcceleration = float2.zero;
			CabinetOffset = float2.zero;
		}
	}
}
