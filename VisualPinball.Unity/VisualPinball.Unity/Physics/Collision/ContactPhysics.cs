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
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal static class ContactPhysics
	{
		internal static void Update(ref ContactBufferElement contact, ref BallState ball, ref PhysicsState state, ref NativeColliders colliders, float hitTime)
		{
			ref var collEvent = ref contact.CollEvent;
			contact.RollingContact = default;
			if (collEvent.ColliderId > -1) { // collide with static collider

				var gravity = state.Env.Gravity;
				var usesColliderSpace = !colliders.IsTransformed(collEvent.ColliderId);
				if (usesColliderSpace) {
					ref var matrix = ref state.GetNonTransformableColliderMatrix(collEvent.ColliderId, ref colliders);
					var matrixInv = math.inverse(matrix);
					ball.Transform(matrixInv);
					collEvent.Transform(matrixInv);
					gravity = matrixInv.MultiplyVector(gravity);
				}

				ref var collHeader = ref state.GetColliderHeader(ref colliders, collEvent.ColliderId);
				var supportImpulse = 0f;
				var colliderVelocity = float3.zero;
				if (collHeader.Type == ColliderType.Flipper) {
					// Rolling resistance cannot be applied until the opposing generalized
					// impulse can also be coupled into the flipper movement state.
					ref var flipperCollider = ref colliders.Flipper(collEvent.ColliderId);
					ref var flipperState = ref state.GetFlipperState(collEvent.ColliderId, ref colliders);
					flipperCollider.Contact(ref ball, ref flipperState.Movement, in collEvent, in flipperState.Static, in flipperState.Velocity, hitTime, in gravity);
				} else {
					// surface velocity of the collider at the contact point (zero unless kinematic and moving)
					colliderVelocity = state.GetKinematicSurfaceVelocity(in collEvent,
						ball.Position - ball.Radius * collEvent.HitNormal);
					supportImpulse = Collider.Contact(in collHeader, ref ball, in collEvent, hitTime,
						in gravity, in colliderVelocity);
				}

				if (usesColliderSpace) {
					ref var matrix = ref state.GetNonTransformableColliderMatrix(collEvent.ColliderId, ref colliders);
					var supportVector = supportImpulse * collEvent.HitNormal;
					ball.Transform(matrix);
					collEvent.Transform(matrix);
					colliderVelocity = matrix.MultiplyVector(colliderVelocity);
					supportImpulse = math.length(matrix.MultiplyVector(supportVector));
				}

				if (collHeader.Type != ColliderType.Flipper) {
					contact.RollingContact = new RollingContactData {
						IsContact = collEvent.IsContact,
						RollingResistance = PhysicsMaterialData.SanitizeRollingResistance(
							collHeader.Material.RollingResistance),
						SupportImpulse = supportImpulse,
						ContactNormal = collEvent.HitNormal,
						ColliderVelocity = colliderVelocity,
					};
				}

			} else if (collEvent.BallId != 0) { // collide with ball
				// Ball-to-ball rolling resistance is intentionally unsupported in v1.
				var collHeader = collEvent.IsKinematic
					? ref state.GetColliderHeader(ref state.KinematicColliders, contact.CollEvent.ColliderId)
					: ref state.GetColliderHeader(ref state.Colliders, contact.CollEvent.ColliderId);
				var material = collHeader.Material;
				material.RollingResistance = 0f;
				BallCollider.HandleStaticContact(ref ball, in collEvent, in material, hitTime,
					in state.Env.Gravity, float3.zero);
			}
		}

		internal static void ApplyRollingResistance(ref NativeList<ContactBufferElement> contacts,
			ref PhysicsState state)
		{
			// Narrow phase appends every contact for one ball before moving to the
			// next ball. Preserve that grouping so aggregation remains a single,
			// allocation-free pass over the contact buffer.
			var firstContact = 0;
			while (firstContact < contacts.Length) {
				var ballId = contacts[firstContact].BallId;
				var endContact = firstContact + 1;
				while (endContact < contacts.Length && contacts[endContact].BallId == ballId) {
					endContact++;
				}

				ref var ball = ref state.Balls.GetValueByRef(ballId);
				var rollingContact = SelectRollingContact(in contacts, firstContact, endContact, in ball);
				BallCollider.ApplyRollingResistance(ref ball, in rollingContact);
				firstContact = endContact;
			}
		}

		internal static RollingContactData SelectRollingContact(
			in NativeList<ContactBufferElement> contacts, int firstContact, int endContact,
			in BallState ball)
		{
			// Applying one gravity-derived support impulse per representative keeps
			// duplicate mesh contacts from multiplying rolling loss. Prefer the
			// largest Crr * JnSupport; geometric data supplies deterministic ties.
			var selected = default(RollingContactData);
			for (var i = firstContact; i < endContact; i++) {
				var candidate = contacts[i].RollingContact;
				if (BallCollider.IsRollingContact(in ball, in candidate)
				    && (!selected.IsValid || candidate.IsPreferredOver(in selected))) {
					selected = candidate;
				}
			}
			return selected;
		}
	}
}
