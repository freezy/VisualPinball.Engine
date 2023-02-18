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
using Unity.Profiling;

namespace VisualPinball.Unity
{
	public static class PhysicsStaticNarrowPhase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticNarrowPhase");
		
		internal static void FindNextCollision(float hitTime, ref BallData ball, in NativeList<PlaneCollider> overlappingColliders, 
			ref NativeList<ContactBufferElement> contacts)
		{
			PerfMarker.Begin();

			// init contacts and event
			ball.CollisionEvent.ClearCollider(hitTime); // search upto current hit time

			foreach (var coll in overlappingColliders) {
				var newCollEvent = new CollisionEventData();
				float newTime = 0;
				
				HitTest(ref ball, in coll, ref contacts);
				SaveCollisions(ref ball, ref newCollEvent, ref contacts, in coll, newTime);
			}

			// no negative time allowed
			if (ball.CollisionEvent.HitTime < 0) {
				ball.CollisionEvent.ClearCollider();
			}

			PerfMarker.End();
		}
		
		private static void HitTest(ref BallData ball, in PlaneCollider coll, ref NativeList<ContactBufferElement> contacts) {

			var newCollEvent = new CollisionEventData();
			var newTime = Collider.HitTest(ref ball, in coll, ball.CollisionEvent.HitTime);

			SaveCollisions(ref ball, ref newCollEvent, ref contacts, in coll, newTime);
		}

		private static void SaveCollisions(ref BallData ball, ref CollisionEventData newCollEvent,
			ref NativeList<ContactBufferElement> contacts, in PlaneCollider coll, float newTime)
		{
			var validHit = newTime >= 0f && !Math.Sign(newTime) && newTime <= ball.CollisionEvent.HitTime;

			if (newCollEvent.IsContact || validHit) {
				newCollEvent.SetCollider(coll);
				newCollEvent.HitTime = newTime;
				if (newCollEvent.IsContact) {
					contacts.Add(new ContactBufferElement(ball.Id, newCollEvent));

				} else { // if (validhit)
					ball.CollisionEvent = newCollEvent;
				}
			}
		}
	}
}
