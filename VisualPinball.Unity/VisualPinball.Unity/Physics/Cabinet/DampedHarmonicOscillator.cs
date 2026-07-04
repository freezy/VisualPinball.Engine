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
	public struct DampedHarmonicOscillator
	{
		public float Mass;
		public float Omega0;
		public float SpringConstant;
		public float DampingCoefficient;
		public float Displacement;
		public float Velocity;
		public float Acceleration;

		public DampedHarmonicOscillator(float mass, float frequencyHz, float dampingRatio)
		{
			Mass = mass;
			Omega0 = 2f * math.PI * frequencyHz;
			SpringConstant = mass * Omega0 * Omega0;
			DampingCoefficient = 2f * dampingRatio * mass * Omega0;
			Displacement = 0f;
			Velocity = 0f;
			Acceleration = 0f;
		}

		public void StepOneMillisecond(float force)
		{
			Step(force, 0.001f);
		}

		public void Step(float force, float deltaTime)
		{
			Acceleration = (force - DampingCoefficient * Velocity - SpringConstant * Displacement) / Mass;
			Velocity += Acceleration * deltaTime;
			Displacement += Velocity * deltaTime;
		}

		public void Reset()
		{
			Displacement = 0f;
			Velocity = 0f;
			Acceleration = 0f;
		}
	}
}
