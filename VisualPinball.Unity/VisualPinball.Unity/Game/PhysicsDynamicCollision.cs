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

using Unity.Profiling;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal static class PhysicsDynamicCollision
	{
		private static readonly ProfilerMarker PerfMarker = new("DynamicCollision");

		internal static void Collide(float hitTime, ref BallState ball, ref PhysicsState state)
		{
			// pick "other" ball
			ref var collEvent = ref ball.CollisionEvent;
			ref var otherId = ref collEvent.BallId;

			// find balls with hit objects and minimum time
			if (otherId != 0 && collEvent.HitTime <= hitTime) {

				PerfMarker.Begin();

				ref var otherBall = ref state.Balls.GetValueByRef(otherId);
				ref var otherCollEvent = ref otherBall.CollisionEvent;

				// now collision, contact and script reactions on active ball (object)+++++++++

				//this.activeBall = ball;                         // For script that wants the ball doing the collision

				BallCollider.Collide(ref otherBall, ref ball, in otherCollEvent, in collEvent,
					state.SwapBallCollisionHandling);

				// remove trial hit object pointer
				collEvent.ClearCollider();

				PerfMarker.End();
			}
		}
	}
}
