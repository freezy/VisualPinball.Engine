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

// ReSharper disable ForCanBeConvertedToForeach

using Unity.Collections;

namespace VisualPinball.Unity
{
	public static class PhysicsStaticNarrowPhase
	{
		internal static void FindNextCollision(
			float hitTime,
			ref BallData ball,
			in NativeList<int> overlappingColliders,
			ref NativeList<ContactBufferElement> contacts,
			ref PhysicsState state
		)
		{
			// init contacts and event
			ball.CollisionEvent.ClearCollider(hitTime); // search upto current hit time

			for (var i = 0; i < overlappingColliders.Length; i++) {
				var newCollEvent = new CollisionEventData();
				var colliderRef = new ColliderRef(overlappingColliders[i], ref state.Colliders);
				var newTime = colliderRef.HitTest(ref ball, ref newCollEvent, ref contacts, ref state);
					SaveCollisions(ref ball, ref newCollEvent, ref contacts, colliderRef.Id, newTime);
				}

			// no negative time allowed
			if (ball.CollisionEvent.HitTime < 0) {
				ball.CollisionEvent.ClearCollider();
			}
		}

		private static float HitTest(ref BallData ball, in Collider collider, ref NativeList<ContactBufferElement> contacts)
		{
			ref var collEvent = ref ball.CollisionEvent;
			var hitTime = Collider.HitTest(in collider, ref collEvent, in ball, ball.CollisionEvent.HitTime);
			ball.CollisionEvent = collEvent;
			return hitTime;
		}

		private static void SaveCollisions(ref BallData ball, ref CollisionEventData newCollEvent,
			ref NativeList<ContactBufferElement> contacts, int colliderId, float newTime)
		{
			var validHit = newTime >= 0f && !Math.Sign(newTime) && newTime <= ball.CollisionEvent.HitTime;

			if (newCollEvent.IsContact || validHit) { // todo why newCollEvent.IsContact? it's not in vpx source
				newCollEvent.SetCollider(colliderId, ball.Id);
				newCollEvent.HitTime = newTime;
				if (newCollEvent.IsContact) { // remember all contacts?
					contacts.Add(new ContactBufferElement(ball.Id, newCollEvent));

				} else { // if (validhit)
					ball.CollisionEvent = newCollEvent;
				}
			}
		}
	}
}
