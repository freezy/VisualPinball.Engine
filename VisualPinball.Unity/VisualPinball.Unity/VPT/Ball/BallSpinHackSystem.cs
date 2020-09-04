// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	internal class BallSpinHackSystem : SystemBase
	{
		protected override void OnUpdate()
		{
			var lastPositionBuffer = GetBufferFromEntity<BallLastPositionsBufferElement>(true);
			Entities.ForEach((Entity entity, ref BallData ball, in CollisionEventData collEvent) => {

				var lastPos = lastPositionBuffer[entity];
				var p0 = (ball.RingCounterOldPos / (10000 / PhysicsConstants.PhysicsStepTime) + 1) % BallRingCounterSystem.MaxBallTrailPos;
				var p1 = (ball.RingCounterOldPos / (10000 / PhysicsConstants.PhysicsStepTime) + 2) % BallRingCounterSystem.MaxBallTrailPos;

				// only if already initialized
				if (collEvent.HitDistance < PhysicsConstants.PhysTouch && lastPos[p0].Value.x != Constants.FloatMax && lastPos[p1].Value.x != float.MaxValue) {
					var diffPos = lastPos[p0].Value - ball.Position;
					var mag = diffPos.x*diffPos.x + diffPos.y*diffPos.y;
					var diffPos2 = lastPos[p1].Value - ball.Position;
					var mag2 = diffPos2.x*diffPos2.x + diffPos2.y*diffPos2.y;
					var threshold = (ball.AngularMomentum.x*ball.AngularMomentum.x + ball.AngularMomentum.y*ball.AngularMomentum.y) / math.max(mag, mag2);

					if (!float.IsNaN(threshold) && !float.IsInfinity(threshold) && threshold > 666) {
						var damp = math.clamp(1.0f - (threshold - 666) / 10000, 0.23f, 1); // do not kill spin completely, otherwise stuck balls will happen during regular gameplay
						ball.AngularMomentum *= damp;
					}
				}

			}).Run();
		}
	}
}
