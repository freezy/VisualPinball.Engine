// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Unity;

namespace VisualPinballUnity
{
	internal static class BallVelocityPhysics
	{
		public static void UpdateVelocities(ref BallData ball, float3 gravity)
		{
			if (ball.IsFrozen) {
				return;
			}

			if (ball.ManualControl) {
				ball.Velocity *= 0.5f; // Null out most of the X/Y velocity, want a little bit so the ball can sort of find its way out of obstacles.
				ball.Velocity += new float3(
					math.max(-10.0f, math.min(10.0f, (ball.ManualPosition.x - ball.Position.x) * (float)(1.0/10.0))),
					math.max(-10.0f, math.min(10.0f, (ball.ManualPosition.y - ball.Position.y) * (float)(1.0/10.0))),
					-2.0f
				);
			} else {
				ball.Velocity += (float)PhysicsConstants.PhysFactor * gravity;
			}
		}
	}
}
