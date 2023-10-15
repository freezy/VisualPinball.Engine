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

using Unity.Collections;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal static class PhysicsDynamicNarrowPhase
	{

		internal static void FindNextCollision(ref BallData ball, ref NativeParallelHashSet<int> collidingBalls,
			ref NativeList<ContactBufferElement> contacts, ref PhysicsState state)
		{
			// don't play with frozen balls
			if (ball.IsFrozen) {
				return;
			}

			ref var collEvent = ref ball.CollisionEvent;
			using var enumerator = collidingBalls.GetEnumerator();
			while (enumerator.MoveNext()) {
				var collidingBallId = enumerator.Current;
				ref var collBall = ref state.Balls.GetValueByRef(collidingBallId);

				var newCollEvent = new CollisionEventData();
				var newTime = BallCollider.HitTest(ref newCollEvent, ref ball, in collBall, collEvent.HitTime);
				var validHit = newTime >= 0 && !Math.Sign(newTime) && newTime <= collEvent.HitTime;

				if (newCollEvent.IsContact || validHit) {
					newCollEvent.SetCollider(collidingBallId, ball.Id);
					newCollEvent.HitTime = newTime;
					if (newCollEvent.IsContact) {
						contacts.Add(new ContactBufferElement(ball.Id, newCollEvent));

					} else { // if (validhit)
						collEvent = newCollEvent;
					}
				}
			}
		}
	}
}
