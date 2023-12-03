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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	internal static class TriggerCollider
	{
		public static void Collide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref CollisionEventData collEvent, ref InsideOfs insideOfs,
			ref TriggerAnimationState animation, in ColliderHeader collHeader)
		{
			var insideOf = insideOfs.IsInsideOf(collHeader.ItemId, ball.Id);
			if (collEvent.HitFlag == insideOf) {                                         // Hit == NotAlreadyHit
				ball.Position += PhysicsConstants.StaticTime * ball.Velocity;            // move ball slightly forward
				if (!insideOf) {
					insideOfs.SetInsideOf(collHeader.ItemId, ball.Id);
					animation.HitEvent = true;

					events.Enqueue(new EventData(EventId.HitEventsHit, collHeader.ItemId, ball.Id, true));

				} else {
					insideOfs.SetOutsideOf(collHeader.ItemId, ball.Id);
					animation.UnHitEvent = true;

					events.Enqueue(new EventData(EventId.HitEventsUnhit, collHeader.ItemId, ball.Id, true));
				}
			}
		}
	}
}
