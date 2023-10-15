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
using Unity.Profiling;

namespace VisualPinball.Unity
{
	public static class PhysicsStaticNarrowPhase
	{
		private static readonly ProfilerMarker PerfMarkerNarrowPhase = new("NarrowPhase");

		internal static void FindNextCollision(
			float hitTime,
			ref BallState ball,
			ref NativeParallelHashSet<int> overlappingColliders,
			ref NativeList<ContactBufferElement> contacts,
			ref PhysicsState state
		)
		{
			PerfMarkerNarrowPhase.Begin();

			// init contacts and event
			ball.CollisionEvent.ClearCollider(hitTime); // search upto current hit time

			using (var enumerator = overlappingColliders.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					var overlappingColliderId = enumerator.Current;
					if (!state.IsColliderActive(overlappingColliderId)) {
						continue;
					}
					var newCollEvent = new CollisionEventData();
					var newTime = state.HitTest(overlappingColliderId, ref ball, ref newCollEvent, ref contacts, ref state);
					SaveCollisions(ref ball, ref newCollEvent, ref contacts, overlappingColliderId, newTime);
				}
			}

			// no negative time allowed
			if (ball.CollisionEvent.HitTime < 0) {
				ball.CollisionEvent.ClearCollider();
			}
			PerfMarkerNarrowPhase.End();
		}

		private static float HitTest(ref BallState ball, in Collider collider, ref NativeList<ContactBufferElement> contacts)
		{
			ref var collEvent = ref ball.CollisionEvent;
			var hitTime = Collider.HitTest(in collider, ref collEvent, in ball, ball.CollisionEvent.HitTime);
			ball.CollisionEvent = collEvent;
			return hitTime;
		}

		private static void SaveCollisions(ref BallState ball, ref CollisionEventData newCollEvent,
			ref NativeList<ContactBufferElement> contacts, int colliderId, float newTime)
		{
			var validHit = newTime >= 0f && !Math.Sign(newTime) && newTime <= ball.CollisionEvent.HitTime;

			if (newCollEvent.IsContact || validHit) { // todo why newCollEvent.IsContact? it's not in vpx source
				newCollEvent.SetCollider(colliderId);
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
