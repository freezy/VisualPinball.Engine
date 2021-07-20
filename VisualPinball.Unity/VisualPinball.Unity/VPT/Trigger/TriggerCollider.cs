// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	internal static class TriggerCollider
	{
		public static void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
			ref TriggerAnimationData animationData, in Entity ballEntity, in Collider coll)
		{
			var insideOf = BallData.IsInsideOf(in insideOfs, coll.Entity);
			if (collEvent.HitFlag == insideOf) {                                         // Hit == NotAlreadyHit
				ball.Position += PhysicsConstants.StaticTime * ball.Velocity;            // move ball slightly forward
				if (!insideOf) {
					BallData.SetInsideOf(ref insideOfs, coll.Entity);
					animationData.HitEvent = true;

					events.Enqueue(new EventData(EventId.HitEventsHit, coll.ParentEntity,  ballEntity, true));

				} else {
					BallData.SetOutsideOf(ref insideOfs, coll.Entity);
					animationData.UnHitEvent = true;

					events.Enqueue(new EventData(EventId.HitEventsUnhit, coll.ParentEntity, ballEntity, true));
				}
			}
		}
	}
}
