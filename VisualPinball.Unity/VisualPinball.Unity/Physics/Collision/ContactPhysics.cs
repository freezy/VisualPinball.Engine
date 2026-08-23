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

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal static class ContactPhysics
	{
		private const float DuplicateContactNormalDot = 0.999f;

		/// <summary>
		/// Triangle meshes can report both a face and one of its edge colliders for
		/// the same physical contact. Resolving both applies the sustained normal
		/// load twice and can kick the ball away from the surface every other step.
		/// Only co-oriented contacts belonging to the same item are duplicates.
		/// Kinematic contacts may be deduplicated when both belong to the same moving
		/// item because they share one transform and surface velocity field.
		/// </summary>
		internal static bool IsDuplicateContact(in ContactBufferElement current, in ColliderHeader currentHeader,
			in ContactBufferElement previous, in ColliderHeader previousHeader)
		{
			if (current.BallId != previous.BallId ||
			    current.CollEvent.ColliderId < 0 || previous.CollEvent.ColliderId < 0 ||
			    current.CollEvent.IsKinematic != previous.CollEvent.IsKinematic ||
			    currentHeader.ItemId != previousHeader.ItemId) {
				return false;
			}

			var currentLengthSq = math.lengthsq(current.CollEvent.HitNormal);
			var previousLengthSq = math.lengthsq(previous.CollEvent.HitNormal);
			if (currentLengthSq <= math.EPSILON || previousLengthSq <= math.EPSILON) {
				return false;
			}

			var normalDot = math.dot(current.CollEvent.HitNormal, previous.CollEvent.HitNormal) *
			                math.rsqrt(currentLengthSq * previousLengthSq);
			return normalDot > DuplicateContactNormalDot;
		}

		internal static float3 SupportedAcceleration(in float3 acceleration, in float3 contactNormal)
			=> contactNormal * math.min(0f, math.dot(acceleration, contactNormal));

		internal static void Update(ref ContactBufferElement contact, ref BallState ball, ref PhysicsState state, ref NativeColliders colliders, float hitTime)
		{
			var collEvent = contact.CollEvent;
			var frictionVelocity = contact.FrictionVelocity;
			var frictionAngularMomentum = contact.FrictionAngularMomentum;
			var frictionAcceleration = contact.FrictionAcceleration;
			if (collEvent.ColliderId > -1) { // collide with static collider

				var gravity = state.Env.Gravity;
				if (!colliders.IsTransformed(collEvent.ColliderId)) {
					ref var matrix = ref state.GetNonTransformableColliderMatrix(collEvent.ColliderId, ref colliders);
					var matrixInv = math.inverse(matrix);
					ball.Transform(matrixInv);
					collEvent.Transform(matrixInv);
					gravity = matrixInv.MultiplyVector(gravity);
					frictionVelocity = matrixInv.MultiplyVector(frictionVelocity);
					frictionAngularMomentum = matrixInv.MultiplyVector(frictionAngularMomentum);
					frictionAcceleration = matrixInv.MultiplyVector(frictionAcceleration);
				}

				ref var collHeader = ref state.GetColliderHeader(ref colliders, collEvent.ColliderId);
				if (collHeader.Type == ColliderType.Flipper) {
					ref var flipperCollider = ref colliders.Flipper(collEvent.ColliderId);
					ref var flipperState = ref state.GetFlipperState(collEvent.ColliderId, ref colliders);
					var acceleration = gravity + ball.ExternalAcceleration;
					flipperCollider.Contact(ref ball, ref flipperState.Movement, in collEvent, in flipperState.Static,
						in flipperState.Velocity, hitTime, in acceleration, in frictionAcceleration,
						in frictionVelocity, in frictionAngularMomentum);
				} else {
					// surface velocity of the collider at the contact point (zero unless kinematic and moving)
					var colliderVelocity = state.GetKinematicSurfaceVelocity(in collEvent, ball.Position - ball.Radius * collEvent.HitNormal);
					Collider.Contact(in collHeader, ref ball, in collEvent, hitTime, in gravity, in colliderVelocity,
						in frictionAcceleration, in frictionVelocity, in frictionAngularMomentum);
				}

				if (!colliders.IsTransformed(collEvent.ColliderId)) {
					ref var matrix = ref state.GetNonTransformableColliderMatrix(collEvent.ColliderId, ref colliders);
					ball.Transform(matrix);
					collEvent.Transform(matrix);
				}

			} else if (collEvent.BallId != 0) { // collide with ball
				var collHeader = collEvent.IsKinematic
					? ref state.GetColliderHeader(ref state.KinematicColliders, contact.CollEvent.ColliderId)
					: ref state.GetColliderHeader(ref state.Colliders, contact.CollEvent.ColliderId);
				BallCollider.HandleStaticContact(ref ball, in collEvent, collHeader.Material.Friction, hitTime,
					state.Env.Gravity, float3.zero, in frictionAcceleration, in frictionVelocity, in frictionAngularMomentum);
			}
		}
	}
}
