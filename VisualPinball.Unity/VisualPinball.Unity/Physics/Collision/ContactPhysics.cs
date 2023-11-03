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

namespace VisualPinball.Unity
{
	internal static class ContactPhysics
	{
		internal static void Update(ref ContactBufferElement contact, ref BallState ball, ref PhysicsState state, float hitTime)
		{
			ref var collEvent = ref contact.CollEvent;
			if (collEvent.ColliderId > -1) { // collide with static collider
				var collHeader = collEvent.IsKinematic
					? ref state.GetColliderHeader(ref state.KinematicColliders, collEvent.ColliderId)
					: ref state.GetColliderHeader(ref state.Colliders, collEvent.ColliderId);
				if (collHeader.Type == ColliderType.Flipper) {
					var flipperCollider = collEvent.IsKinematic
						? ref state.KinematicColliders.Flipper(collEvent.ColliderId)
						: ref state.Colliders.Flipper(collEvent.ColliderId);
					ref var flipperState = ref state.GetFlipperState(collEvent.ColliderId);
					flipperCollider.Contact(ref ball, ref flipperState.Movement, in collEvent,
						in flipperState.Static, in flipperState.Velocity, hitTime, in state.Env.Gravity);
				} else {
					Collider.Contact(in collHeader, ref ball, in collEvent, hitTime, in state.Env.Gravity);
				}
			} else if (collEvent.BallId != 0) { // collide with ball
				var collHeader = collEvent.IsKinematic
					? ref state.GetColliderHeader(ref state.KinematicColliders, contact.CollEvent.ColliderId)
					: ref state.GetColliderHeader(ref state.Colliders, contact.CollEvent.ColliderId);
				BallCollider.HandleStaticContact(ref ball, in collEvent, collHeader.Material.Friction, hitTime, state.Env.Gravity);
			}
		}
	}
}
