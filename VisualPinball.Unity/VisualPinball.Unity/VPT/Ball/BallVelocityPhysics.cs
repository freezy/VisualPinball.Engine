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

namespace VisualPinball.Unity
{
	internal static class BallVelocityPhysics
	{
		public static void UpdateVelocities(ref BallState ball, float3 gravity)
		{
			if (ball.IsFrozen) {
				return;
			}

			// A resting ball must stop rotating. The point-contact model has no
			// drilling friction, and the per-contact friction impulses that hold the
			// ball in place re-inject a little angular momentum every tick (their
			// torques don't cancel across sequentially solved contacts) — without
			// this, a ball cradled on a raised flipper keeps spinning forever.
			// Applied here, before displacement integrates it: a firm spin-down for
			// visible spin, and a hard stop below ~30°/s (the per-tick injection is
			// far below that band, so the flutter dies instantly). Any real impulse
			// breaks the rest state (see BallSpinHackPhysics), so gameplay spin is
			// unaffected.
			if (ball.IsAtRest) {
				ball.AngularMomentum *= 0.988f; // half-life ~60 ms
				var restSpinEps = 0.005f * ball.Inertia; // ~30°/s
				if (math.lengthsq(ball.AngularMomentum) < restSpinEps * restSpinEps) {
					ball.AngularMomentum = float3.zero;
				}
			}

			if (ball.ManualControl) {
				ball.Velocity *= 0.5f; // Null out most of the X/Y velocity, want a little bit so the ball can sort of find its way out of obstacles.
				ball.Velocity += new float3(
					math.max(-10.0f, math.min(10.0f, (ball.ManualPosition.x - ball.Position.x) * (float)(1.0/10.0))),
					math.max(-10.0f, math.min(10.0f, (ball.ManualPosition.y - ball.Position.y) * (float)(1.0/10.0))),
					-2.0f
				);
			} else {
				ball.Velocity += PhysicsConstants.PhysFactor * gravity;
			}
		}
	}
}
