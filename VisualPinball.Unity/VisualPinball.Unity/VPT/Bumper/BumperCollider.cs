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
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	internal static class BumperCollider
	{
		public static void Collide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref CollisionEventData collEvent, ref BumperRingAnimationState ringState, ref BumperSkirtAnimationState skirtState,
			in ColliderHeader collHeader, in BumperStaticState state, ref Random random, ref InsideOfs insideOfs)
		{
			var wasBallInside = insideOfs.IsInsideOf(collHeader.ItemId, ball.Id);
			var isBallInside = !collEvent.HitFlag;
			if (isBallInside != wasBallInside) {
				ball.Position += ball.Velocity * PhysicsConstants.StaticTime;
				if (isBallInside) {
					insideOfs.SetInsideOf(collHeader.ItemId, ball.Id);
					events.Enqueue(new EventData(EventId.HitEventsHit, collHeader.ItemId, ball.Id, true));
				} else {
					insideOfs.SetOutsideOf(collHeader.ItemId, ball.Id);
					events.Enqueue(new EventData(EventId.HitEventsUnhit, collHeader.ItemId, ball.Id, true));
				}
			}
		}
	}
}
