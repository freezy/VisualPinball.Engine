﻿// Visual Pinball Engine
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

namespace VisualPinball.Unity
{
	internal static class BumperCollider
	{
		public static void Collide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref CollisionEventData collEvent, ref BumperRingAnimationState ringState, ref BumperSkirtAnimationState skirtState,
			in ColliderHeader collHeader, in BumperStaticState state, ref Random random)
		{
			// todo
			// if (!m_enabled) return;

			var dot = math.dot(collEvent.HitNormal, ball.Velocity); // needs to be computed before Collide3DWall()!
			var material = collHeader.Material;
			BallCollider.Collide3DWall(ref ball, in material, in collEvent, in collEvent.HitNormal, ref random); // reflect ball from wall

			if (state.HitEvent && dot <= -state.Threshold) { // if velocity greater than threshold level

				ball.Velocity += collEvent.HitNormal * state.Force; // add a chunk of velocity to drive ball away

				ringState.IsHit = true;
				skirtState.HitEvent = true;
				skirtState.BallPosition = ball.Position;

				events.Enqueue(new EventData(EventId.HitEventsHit, collHeader.ItemId, ball.Id, true));
			}
		}
	}
}
