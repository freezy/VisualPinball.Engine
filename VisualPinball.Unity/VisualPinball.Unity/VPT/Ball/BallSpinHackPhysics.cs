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

using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Unity;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace VisualPinballUnity
{
	[DisableAutoCreation]
	internal static class BallSpinHackPhysics
	{
		internal static void Update(ref BallData ball)
		{
			var p0 = (ball.RingCounterOldPos / (10000 / PhysicsConstants.PhysicsStepTime) + 1) % BallPositions.Count;
			var p1 = (ball.RingCounterOldPos / (10000 / PhysicsConstants.PhysicsStepTime) + 2) % BallPositions.Count;

			// only if already initialized
			if (ball.CollisionEvent.HitDistance < PhysicsConstants.PhysTouch && ball.LastPositions[p0].x != Constants.FloatMax && ball.LastPositions[p1].x != float.MaxValue) {
				var diffPos = ball.LastPositions[p0] - ball.Position;
				var mag = diffPos.x*diffPos.x + diffPos.y*diffPos.y;
				var diffPos2 = ball.LastPositions[p1] - ball.Position;
				var mag2 = diffPos2.x*diffPos2.x + diffPos2.y*diffPos2.y;
				var threshold = (ball.AngularMomentum.x*ball.AngularMomentum.x + ball.AngularMomentum.y*ball.AngularMomentum.y) / math.max(mag, mag2);

				if (!float.IsNaN(threshold) && !float.IsInfinity(threshold) && threshold > 666) {
					var damp = math.clamp(1.0f - (threshold - 666) / 10000, 0.23f, 1); // do not kill spin completely, otherwise stuck balls will happen during regular gameplay
					ball.AngularMomentum *= damp;
				}
			}
		}
	}
}
