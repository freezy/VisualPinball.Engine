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

namespace VisualPinball.Unity
{
	internal static class TargetCollider
	{
		public static void DropTargetCollide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			ref DropTargetAnimationState animation, in float3 normal, in CollisionEventData collEvent,
			in ColliderHeader collHeader, ref PhysicsState state)
		{
			if (animation.IsDropped) {
				return;
			}

			var dot = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in collHeader.Material, in collEvent, in normal, ref state);

			if (collHeader.FireEvents && dot >= collHeader.Threshold && !animation.IsDropped) {
				animation.HitEvent = true;
				//todo m_obj->m_currentHitThreshold = dot;
				Collider.FireHitEvent(ref ball, ref hitEvents, in collHeader);
			}
		}

		public static void HitTargetCollide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			ref HitTargetAnimationData animationData, in float3 normal, in CollisionEventData collEvent,
			in ColliderHeader collHeader, ref PhysicsState state)
		{
			var dot = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in collHeader.Material, in collEvent, in normal, ref state);

			if (collHeader.FireEvents && dot >= collHeader.Threshold) {
				animationData.HitEvent = true;
				//todo m_obj->m_currentHitThreshold = dot;
				Collider.FireHitEvent(ref ball, ref hitEvents, in collHeader);
			}
		}
	}
}
