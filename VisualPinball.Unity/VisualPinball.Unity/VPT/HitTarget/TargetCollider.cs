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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	internal static class TargetCollider
	{
		public static void DropTargetCollide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			ref DropTargetState target, in float3 normal, in CollisionEventData collEvent,
			in ColliderHeader collHeader, ref PhysicsState state)
		{
			if (target.Static.PhysicsMode == DropTargetPhysicsMode.Mechanical) {
				MechanicalCollide(ref ball, ref hitEvents, ref target, in normal, in collEvent, in collHeader, ref state);
				return;
			}
			if (target.Static.PhysicsMode == DropTargetPhysicsMode.RothCompatible) {
				RothCompatibleCollide(ref ball, ref hitEvents, ref target, in normal, in collEvent, in collHeader, ref state);
				return;
			}
			LegacyDropTargetCollide(ref ball, ref hitEvents, ref target.Animation, in normal, in collEvent, in collHeader, ref state);
		}

		private static void MechanicalCollide(ref BallState ball,
			ref NativeQueue<EventData>.ParallelWriter hitEvents, ref DropTargetState target, in float3 normal,
			in CollisionEventData collEvent, in ColliderHeader collHeader, ref PhysicsState state)
		{
			if (target.Mechanical.State == DropTargetMechanismState.Down) {
				return;
			}
			var preImpactVelocity = ball.Velocity;
			var hitNormal = math.normalizesafe(collEvent.HitNormal);
			var faceAlignment = math.abs(math.dot(hitNormal, math.normalizesafe(target.Static.FaceNormal)));
			if (collHeader.Role != ColliderRole.DropTargetPhysicalFace || faceAlignment < 0.5f) {
				BallCollider.Collide3DWall(ref ball, in collHeader.Material, in collEvent, in normal, ref state);
				if (collHeader.Role == ColliderRole.DropTargetBackFace) {
					var impulse = ball.Mass * math.max(-math.dot(preImpactVelocity, hitNormal), 0f);
					if (target.Static.Mechanical.EnableBacksideRelease
						&& impulse >= target.Static.Mechanical.BacksideReleaseImpulse) {
						target.Mechanical.State = DropTargetMechanismState.Released;
						target.Mechanical.LastImpactOutcome = DropTargetImpactOutcome.BacksideDrop;
						FireDropTargetHit(ref ball, ref hitEvents, in collHeader);
					} else {
						target.Mechanical.LastImpactOutcome = DropTargetImpactOutcome.BacksideBounce;
					}
				} else {
					target.Mechanical.LastImpactOutcome = DropTargetImpactOutcome.SideDeflection;
				}
				return;
			}

			var approachSpeed = -math.dot(preImpactVelocity
				- MechanicalDropTargetPhysics.SurfaceVelocity(in target.Static, in target.Mechanical), hitNormal);
			var restitution = ResolveElasticity(in collHeader.Material, in collEvent, approachSpeed, ref state);
			var friction = ResolveFriction(in collHeader.Material, in collEvent, approachSpeed, ref state);
			var result = MechanicalDropTargetPhysics.ResolveImpact(ref ball, ref target.Mechanical,
				in target.Static, in hitNormal, restitution, friction);
			if (!result.Applied) {
				return;
			}

			var hDist = math.clamp(-PhysicsConstants.DispGain * collEvent.HitDistance,
				0f, PhysicsConstants.DispLimit);
			ball.Position += hitNormal * hDist;
			if (!target.Mechanical.HitEventFired
				&& approachSpeed >= collHeader.Threshold
				&& result.NormalImpulse >= target.Static.Mechanical.MinimumFaceImpulse) {
				target.Mechanical.HitEventFired = true;
				FireDropTargetHit(ref ball, ref hitEvents, in collHeader);
			}
		}

		private static float ResolveElasticity(in PhysicsMaterialData material,
			in CollisionEventData collEvent, float speed, ref PhysicsState state)
		{
			if (!material.UseElasticityOverVelocity) {
				return Math.ElasticityWithFalloff(material.Elasticity, material.ElasticityFalloff, speed);
			}
			// Mechanical contacts deliberately sample loss by approach-speed
			// magnitude; the legacy wall path's signed lookup clamps to its first bin.
			var colliders = collEvent.IsKinematic ? state.KinematicColliders : state.Colliders;
			var itemId = colliders.GetItemId(collEvent.ColliderId);
			return state.ElasticityOverVelocityLUTs[itemId].InterpolateLUT(0f, 63f, math.abs(speed));
		}

		private static float ResolveFriction(in PhysicsMaterialData material,
			in CollisionEventData collEvent, float speed, ref PhysicsState state)
		{
			if (!material.UseFrictionOverVelocity) {
				return material.Friction;
			}
			// As above, velocity-dependent Mechanical friction is indexed by speed.
			var colliders = collEvent.IsKinematic ? state.KinematicColliders : state.Colliders;
			var itemId = colliders.GetItemId(collEvent.ColliderId);
			return state.FrictionOverVelocityLUTs[itemId].InterpolateLUT(0f, 127f, math.abs(speed));
		}

		private static void LegacyDropTargetCollide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
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

		private static void RothCompatibleCollide(ref BallState ball,
			ref NativeQueue<EventData>.ParallelWriter hitEvents, ref DropTargetState target, in float3 normal,
			in CollisionEventData collEvent, in ColliderHeader collHeader, ref PhysicsState state)
		{
			if (target.Animation.IsDropped) {
				return;
			}

			var preImpactVelocity = ball.Velocity;
			if (collHeader.Role == ColliderRole.DropTargetFrontSensor) {
				var inside = state.InsideOfs.IsInsideOf(collHeader.ItemId, ball.Id);
				if (collEvent.HitFlag != inside) {
					return;
				}
				ball.Position += PhysicsConstants.StaticTime * ball.Velocity;
				if (inside) {
					state.InsideOfs.SetOutsideOf(collHeader.ItemId, ball.Id);
					return;
				}
				state.InsideOfs.SetInsideOf(collHeader.ItemId, ball.Id);
				ProcessRothHit(ref ball, ref hitEvents, ref target, in preImpactVelocity, in collEvent, in collHeader, false);
				return;
			}

			BallCollider.Collide3DWall(ref ball, in collHeader.Material, in collEvent, in normal, ref state);
			if (collHeader.Role == ColliderRole.DropTargetPhysicalFace && !target.Static.HasRothSensor) {
				ProcessRothHit(ref ball, ref hitEvents, ref target, in preImpactVelocity, in collEvent, in collHeader, true);
				return;
			}
			if (collHeader.Role == ColliderRole.DropTargetBackFace) {
				var approachSpeed = -math.dot(preImpactVelocity, math.normalizesafe(collEvent.HitNormal));
				var outcome = RothDropTargetPhysics.Classify(in target.Static.Roth, collHeader.Role,
					approachSpeed, 1f, 0f);
				if (outcome == RothDropTargetOutcome.BacksideDrop && approachSpeed >= collHeader.Threshold) {
					ActivateDrop(ref ball, ref hitEvents, ref target, in collHeader);
				}
			}
		}

		private static void ProcessRothHit(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			ref DropTargetState target, in float3 preImpactVelocity, in CollisionEventData collEvent,
			in ColliderHeader collHeader, bool solidFallback)
		{
			var faceNormal = math.normalizesafe(target.Static.FaceNormal);
			if (math.dot(faceNormal, collEvent.HitNormal) < 0f) {
				faceNormal = -faceNormal;
			}
			var hitNormal = math.normalizesafe(collEvent.HitNormal);
			var approachSpeed = -math.dot(preImpactVelocity, faceNormal);
			var faceAlignment = math.abs(math.dot(hitNormal, faceNormal));
			var tangent = math.normalizesafe(math.cross(new float3(0f, 0f, 1f), faceNormal));
			var contactPoint = ball.Position - ball.Radius * hitNormal;
			var centerDistance = math.abs(math.dot(contactPoint - target.Static.Center, tangent));
			var outcome = RothDropTargetPhysics.Classify(in target.Static.Roth, collHeader.Role,
				approachSpeed, faceAlignment, centerDistance);
			var qualifies = approachSpeed >= collHeader.Threshold;

			if (solidFallback) {
				// Without a separate sensor and offset wall, retain the wall rebound
				// unless this hit actually drops the target. Otherwise an elastic mass
				// correction would point the ball back into a face that remains solid.
				if (outcome == RothDropTargetOutcome.FaceDrop && qualifies) {
					RothDropTargetPhysics.ApplyMassCorrection(ref ball, in preImpactVelocity, in faceNormal,
						target.Static.Roth.TargetMass);
					RothDropTargetPhysics.ApplyVerticalBouncer(ref ball, in target.Static.Roth,
						collHeader.ItemId, target.RothHitCounter++);
					ActivateDrop(ref ball, ref hitEvents, ref target, in collHeader);
				} else if (outcome == RothDropTargetOutcome.Brick && qualifies) {
					FireDropTargetHit(ref ball, ref hitEvents, in collHeader);
				}
				return;
			}

			// Roth applies DTBallPhysics to state 4 (side) as well as normal and
			// brick hits; preserve that script behavior in the true sensor path.
			if (outcome == RothDropTargetOutcome.FaceDrop || outcome == RothDropTargetOutcome.Brick
				|| outcome == RothDropTargetOutcome.SideHit) {
				RothDropTargetPhysics.ApplyMassCorrection(ref ball, in preImpactVelocity, in faceNormal,
					target.Static.Roth.TargetMass);
				RothDropTargetPhysics.ApplyVerticalBouncer(ref ball, in target.Static.Roth,
					collHeader.ItemId, target.RothHitCounter++);
			}

			if (!qualifies) {
				return;
			}
			if (outcome == RothDropTargetOutcome.FaceDrop) {
				ActivateDrop(ref ball, ref hitEvents, ref target, in collHeader);
			} else if (outcome == RothDropTargetOutcome.Brick) {
				FireDropTargetHit(ref ball, ref hitEvents, in collHeader);
			}
		}

		private static void ActivateDrop(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			ref DropTargetState target, in ColliderHeader collHeader)
		{
			target.Animation.HitEvent = true;
			target.Animation.MoveAnimation = true;
			FireDropTargetHit(ref ball, ref hitEvents, in collHeader);
		}

		private static void FireDropTargetHit(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in ColliderHeader collHeader)
		{
			var eventHeader = collHeader;
			eventHeader.ItemType = ItemType.HitTarget;
			Collider.FireHitEvent(ref ball, ref hitEvents, in eventHeader);
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
