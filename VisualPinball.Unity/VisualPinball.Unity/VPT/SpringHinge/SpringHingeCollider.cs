// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	internal struct SpringHingeCollider : ICollider
	{
		private const int ConservativeIterations = 32;
		private const int RefinementIterations = 14;
		private const int FallbackSegments = 32;
		private const float AdvanceSafety = 0.8f;
		private const float TimeEpsilon = 1e-7f;

		public int Id
		{
			get => Header.Id;
			set => Header.Id = value;
		}

		public ColliderHeader Header;
		public readonly int HingeOwnerId;
		public readonly float3 CentreArm;
		public readonly float3 HalfExtents;
		public readonly float3 ReferenceAxisX;
		public readonly float3 ReferenceAxisY;
		public readonly float3 ReferenceAxisZ;
		public readonly float MaxProxyRadius;
		private readonly Aabb _fullTravelAabb;

		public ColliderBounds Bounds => new(Header.ItemId, Header.Id, _fullTravelAabb);

		internal SpringHingeCollider(int hingeOwnerId, in float3 pivot, in float3 centreArm,
			in float3 halfExtents, in float3 referenceAxisX, in float3 referenceAxisY,
			in float3 referenceAxisZ, ColliderInfo info) : this()
		{
			Header.Init(info, ColliderType.SpringHinge);
			HingeOwnerId = hingeOwnerId;
			CentreArm = centreArm;
			HalfExtents = math.max(halfExtents, float3.zero);
			ReferenceAxisX = math.normalizesafe(referenceAxisX, new float3(1f, 0f, 0f));
			ReferenceAxisY = math.normalizesafe(referenceAxisY, new float3(0f, 1f, 0f));
			ReferenceAxisZ = math.normalizesafe(referenceAxisZ, new float3(0f, 0f, 1f));
			MaxProxyRadius = CalculateMaxProxyRadius();
			var radius = new float3(MaxProxyRadius);
			_fullTravelAabb = new Aabb(pivot - radius, pivot + radius);
		}

		internal SpringHingeDistance Distance(in SpringHingeState hinge, in float3 sphereCentre,
			float sphereRadius, float time = 0f)
		{
			var angle = math.clamp(hinge.Movement.Angle + hinge.Movement.AngularVelocity * time,
				hinge.Static.MinimumAngle, hinge.Static.MaximumAngle);
			math.sincos(angle, out var sine, out var cosine);
			var centre = hinge.Static.Pivot + SpringHingeVelocityPhysics.RotateAroundAxis(
				CentreArm, hinge.Static.Axis, sine, cosine);
			var axisX = SpringHingeVelocityPhysics.RotateAroundAxis(ReferenceAxisX, hinge.Static.Axis, sine, cosine);
			var axisY = SpringHingeVelocityPhysics.RotateAroundAxis(ReferenceAxisY, hinge.Static.Axis, sine, cosine);
			var axisZ = SpringHingeVelocityPhysics.RotateAroundAxis(ReferenceAxisZ, hinge.Static.Axis, sine, cosine);
			var relative = sphereCentre - centre;
			var local = new float3(math.dot(relative, axisX), math.dot(relative, axisY), math.dot(relative, axisZ));
			var closest = math.clamp(local, -HalfExtents, HalfExtents);
			var outside = local - closest;
			var outsideLengthSq = math.lengthsq(outside);

			float3 normalLocal;
			float signedPointDistance;
			if (outsideLengthSq > 1e-12f) {
				var outsideLength = math.sqrt(outsideLengthSq);
				normalLocal = outside / outsideLength;
				signedPointDistance = outsideLength;
			} else {
				var faceDistance = HalfExtents - math.abs(local);
				var face = faceDistance.x <= faceDistance.y && faceDistance.x <= faceDistance.z ? 0
					: faceDistance.y <= faceDistance.z ? 1 : 2;
				normalLocal = float3.zero;
				normalLocal[face] = local[face] < 0f ? -1f : 1f;
				closest = local;
				closest[face] = normalLocal[face] * HalfExtents[face];
				signedPointDistance = -faceDistance[face];
			}

			var normal = math.normalizesafe(axisX * normalLocal.x + axisY * normalLocal.y + axisZ * normalLocal.z,
				axisX);
			var witness = centre + axisX * closest.x + axisY * closest.y + axisZ * closest.z;
			return new SpringHingeDistance(signedPointDistance - sphereRadius, witness, normal);
		}

		internal float HitTest(ref CollisionEventData collEvent, in SpringHingeState hinge,
			in BallState ball, float dTime)
		{
			if (ball.IsFrozen || dTime < 0f) {
				return -1f;
			}

			var maxTime = dTime;
			var stopTime = SpringHingeDisplacementPhysics.GetStopTime(hinge);
			if (stopTime > 0f) {
				maxTime = math.min(maxTime, stopTime);
			}
			var rateBound = math.length(ball.Velocity)
				+ math.abs(hinge.Movement.AngularVelocity) * MaxProxyRadius;
			var time = 0f;
			var distance = Distance(in hinge, ball.Position, ball.Radius);

			if (distance.Separation <= PhysicsConstants.PhysTouch) {
				return PopulateHit(ref collEvent, in hinge, in ball, in distance, 0f);
			}
			if (rateBound <= math.EPSILON || maxTime <= 0f) {
				return -1f;
			}

			var needsFallback = false;
			var iterations = 0;
			for (; iterations < ConservativeIterations && time < maxTime; iterations++) {
				var previousTime = time;
				var advance = AdvanceSafety * distance.Separation / rateBound;
				if (!math.isfinite(advance) || advance <= TimeEpsilon) {
					needsFallback = true;
					break;
				}
				time = math.min(maxTime, time + advance);
				distance = Distance(in hinge, ball.Position + ball.Velocity * time, ball.Radius, time);
				if (distance.Separation <= 0f) {
					time = RefineHitTime(in hinge, in ball, previousTime, time);
					distance = Distance(in hinge, ball.Position + ball.Velocity * time, ball.Radius, time);
					return PopulateHit(ref collEvent, in hinge, in ball, in distance, time);
				}
				if (time <= previousTime) {
					needsFallback = true;
					break;
				}
			}

			if (!needsFallback && (time >= maxTime || iterations < ConservativeIterations)) {
				return -1f;
			}
			return FallbackHitTest(ref collEvent, in hinge, in ball, time, maxTime);
		}

		internal void Collide(ref BallState ball, ref SpringHingeState hinge,
			in CollisionEventData collEvent, ref PhysicsState state)
		{
			var distance = Distance(in hinge, ball.Position, ball.Radius);
			var normal = distance.Normal;
			var correction = math.clamp(-PhysicsConstants.DispGain * distance.Separation,
				0f, PhysicsConstants.DispLimit);
			if (correction > 1e-4f) {
				ball.Position += correction * normal;
				distance = Distance(in hinge, ball.Position, ball.Radius);
			}
			var arm = distance.Witness - hinge.Static.Pivot;
			var surfaceVelocity = hinge.Movement.AngularVelocity * math.cross(hinge.Static.Axis, arm);
			var normalVelocity = math.dot(ball.Velocity - surfaceVelocity, normal);
			if (normalVelocity >= -PhysicsConstants.LowNormVel) {
				return;
			}

			var responseArm = math.dot(hinge.Static.Axis, math.cross(arm, normal));
			var hingeResponse = responseArm * responseArm / hinge.Static.Inertia;
			if (PushesIntoActiveStop(in hinge.Movement, -responseArm)) {
				hingeResponse = 0f;
			}
			var inverseEffectiveMass = ball.InvMass + hingeResponse;
			if (inverseEffectiveMass <= math.EPSILON) {
				return;
			}
			var elasticity = Math.ElasticityWithFalloff(Header.Material.Elasticity,
				Header.Material.ElasticityFalloff, normalVelocity);
			if (Header.Material.UseElasticityOverVelocity) {
				var lut = state.ElasticityOverVelocityLUTs[Header.ItemId];
				elasticity = lut.InterpolateLUT(0, 127f, -normalVelocity);
			}
			var impulse = -(1f + elasticity) * normalVelocity / inverseEffectiveMass;
			ball.Velocity += impulse * normal * ball.InvMass;
			ApplyAngularImpulse(ref hinge, -impulse * responseArm);

			var ballArm = -ball.Radius * normal;
			var relativeSurfaceVelocity = BallState.SurfaceVelocity(in ball, in ballArm)
				- hinge.Movement.AngularVelocity * math.cross(hinge.Static.Axis, arm);
			var tangentVelocity = relativeSurfaceVelocity
				- normal * math.dot(relativeSurfaceVelocity, normal);
			var tangentSpeed = math.length(tangentVelocity);
			if (tangentSpeed > PhysicsConstants.Precision) {
				var tangent = tangentVelocity / tangentSpeed;
				var ballCross = math.cross(ballArm, tangent);
				var hingeTangentArm = math.dot(hinge.Static.Axis, math.cross(arm, tangent));
				var tangentResponse = ball.InvMass
					+ math.dot(tangent, math.cross(ballCross / ball.Inertia, ballArm));
				if (!PushesIntoActiveStop(in hinge.Movement, hingeTangentArm)) {
					tangentResponse += hingeTangentArm * hingeTangentArm / hinge.Static.Inertia;
				}
				var friction = GetFriction(ref state, normalVelocity);
				var frictionImpulse = math.clamp(-tangentSpeed / tangentResponse,
					-friction * impulse, friction * impulse);
				ball.ApplySurfaceImpulse(frictionImpulse * ballCross, frictionImpulse * tangent);
				ApplyAngularImpulse(ref hinge, -frictionImpulse * hingeTangentArm);
			}
			SpringHingeVelocityPhysics.RefreshContinuousAcceleration(ref hinge);
			if (-normalVelocity >= Header.Threshold) {
				Collider.FireHitEvent(ref ball, ref state.EventQueue, in Header);
			}
		}

		internal void Contact(ref BallState ball, ref SpringHingeState hinge,
			in CollisionEventData collEvent, float dTime, in float3 acceleration,
			in float3 frictionAcceleration, in float3 frictionVelocity,
			in float3 frictionAngularMomentum)
		{
			var distance = Distance(in hinge, ball.Position, ball.Radius);
			var normal = distance.Normal;
			if (distance.Separation < -PhysicsConstants.Embedded) {
				ball.Velocity += 0.1f * normal;
			}
			var ballArm = -ball.Radius * normal;
			var hingeArm = distance.Witness - hinge.Static.Pivot;
			var relativeVelocity = BallState.SurfaceVelocity(in ball, in ballArm)
				- hinge.Movement.AngularVelocity * math.cross(hinge.Static.Axis, hingeArm);
			var normalVelocity = math.dot(relativeVelocity, normal);
			if (normalVelocity > PhysicsConstants.ContactVel) {
				return;
			}

			var angularVelocity = hinge.Movement.AngularVelocity * hinge.Static.Axis;
			var hingeAcceleration = hinge.Movement.ContinuousAngularAcceleration
				* math.cross(hinge.Static.Axis, hingeArm)
				+ math.cross(angularVelocity, math.cross(angularVelocity, hingeArm));
			var ballAcceleration = BallState.SurfaceAcceleration(in ball, in ballArm, in acceleration);
			var frictionBall = ball;
			frictionBall.Velocity = frictionVelocity;
			frictionBall.AngularMomentum = frictionAngularMomentum;
			var frictionBallAcceleration = BallState.SurfaceAcceleration(in frictionBall, in ballArm,
				in frictionAcceleration);
			var normalDerivative = math.cross(angularVelocity, normal);
			var normalAcceleration = math.dot(ballAcceleration - hingeAcceleration, normal)
				+ 2f * math.dot(normalDerivative, relativeVelocity);
			var responseArm = math.dot(hinge.Static.Axis, math.cross(hingeArm, normal));
			var hingeResponse = PushesIntoActiveStop(in hinge.Movement, -responseArm)
				? 0f : responseArm * responseArm / hinge.Static.Inertia;
			var inverseEffectiveMass = ball.InvMass + hingeResponse;
			if (inverseEffectiveMass <= math.EPSILON) {
				return;
			}
			var supportForce = math.max(0f, -normalAcceleration / inverseEffectiveMass);
			var normalImpulse = math.max(0f,
				-normalVelocity / inverseEffectiveMass + supportForce * dTime);
			var hingeAngularVelocityBeforeImpulse = hinge.Movement.AngularVelocity;
			ball.Velocity += normalImpulse * normal * ball.InvMass;
			ApplyAngularImpulse(ref hinge, -normalImpulse * responseArm);

			// Friction uses the contact-pass snapshots on both bodies. The normal solve above
			// must not manufacture tangential slip by changing only the hinge side first.
			var frictionRelative = BallState.SurfaceVelocity(in frictionBall, in ballArm)
				- hingeAngularVelocityBeforeImpulse * math.cross(hinge.Static.Axis, hingeArm);
			var frictionNormalAcceleration = math.dot(frictionBallAcceleration - hingeAcceleration, normal)
				+ 2f * math.dot(normalDerivative, frictionRelative);
			var frictionSupportForce = math.max(0f, -frictionNormalAcceleration / inverseEffectiveMass);
			var slip = frictionRelative - normal * math.dot(frictionRelative, normal);
			var slipSpeed = math.length(slip);
			if (slipSpeed > PhysicsConstants.Precision && frictionSupportForce > 0f) {
				var tangent = slip / slipSpeed;
				var ballCross = math.cross(ballArm, tangent);
				var hingeTangentArm = math.dot(hinge.Static.Axis, math.cross(hingeArm, tangent));
				var tangentResponse = ball.InvMass
					+ math.dot(tangent, math.cross(ballCross / ball.Inertia, ballArm));
				if (!PushesIntoActiveStop(in hinge.Movement, hingeTangentArm)) {
					tangentResponse += hingeTangentArm * hingeTangentArm / hinge.Static.Inertia;
				}
				var friction = Header.Material.Friction;
				var impulse = math.clamp(-slipSpeed / tangentResponse,
					-friction * frictionSupportForce * dTime, friction * frictionSupportForce * dTime);
				ball.ApplySurfaceImpulse(impulse * ballCross, impulse * tangent);
				ApplyAngularImpulse(ref hinge, -impulse * hingeTangentArm);
			}
			SpringHingeVelocityPhysics.RefreshContinuousAcceleration(ref hinge);
		}

		private float FallbackHitTest(ref CollisionEventData collEvent, in SpringHingeState hinge,
			in BallState ball, float startTime, float maxTime)
		{
			var previousTime = startTime;
			for (var i = 1; i <= FallbackSegments; i++) {
				var time = math.lerp(startTime, maxTime, (float)i / FallbackSegments);
				var distance = Distance(in hinge, ball.Position + ball.Velocity * time, ball.Radius, time);
				if (distance.Separation <= 0f) {
					var refined = RefineHitTime(in hinge, in ball, previousTime, time);
					var refinedDistance = Distance(in hinge,
						ball.Position + ball.Velocity * refined, ball.Radius, refined);
					return PopulateHit(ref collEvent, in hinge, in ball, in refinedDistance, refined);
				}
				previousTime = time;
			}
			return -1f;
		}

		private float RefineHitTime(in SpringHingeState hinge, in BallState ball, float lower, float upper)
		{
			for (var i = 0; i < RefinementIterations; i++) {
				var middle = (lower + upper) * 0.5f;
				var distance = Distance(in hinge, ball.Position + ball.Velocity * middle, ball.Radius, middle);
				if (distance.Separation > 0f) {
					lower = middle;
				} else {
					upper = middle;
				}
			}
			return upper;
		}

		private float PopulateHit(ref CollisionEventData collEvent, in SpringHingeState hinge,
			in BallState ball, in SpringHingeDistance distance, float time)
		{
			var angularVelocity = hinge.Movement.AngularVelocity;
			var predictedAngle = hinge.Movement.Angle + angularVelocity * time;
			if (predictedAngle <= hinge.Static.MinimumAngle && angularVelocity < 0f
			    || predictedAngle >= hinge.Static.MaximumAngle && angularVelocity > 0f) {
				angularVelocity = 0f;
			}
			var surfaceVelocity = angularVelocity * math.cross(hinge.Static.Axis,
				distance.Witness - hinge.Static.Pivot);
			var normalVelocity = math.dot(ball.Velocity - surfaceVelocity, distance.Normal);
			if (normalVelocity > PhysicsConstants.LowNormVel && distance.Separation > -PhysicsConstants.Embedded) {
				return -1f;
			}
			collEvent.HitNormal = distance.Normal;
			collEvent.HitDistance = distance.Separation;
			collEvent.HitOrgNormalVelocity = normalVelocity;
			collEvent.IsContact = math.abs(normalVelocity) <= PhysicsConstants.ContactVel
				&& distance.Separation <= PhysicsConstants.PhysTouch;
			return time;
		}

		private float CalculateMaxProxyRadius()
		{
			var maxRadiusSq = 0f;
			for (var x = -1; x <= 1; x += 2) {
				for (var y = -1; y <= 1; y += 2) {
					for (var z = -1; z <= 1; z += 2) {
						var corner = CentreArm + x * HalfExtents.x * ReferenceAxisX
							+ y * HalfExtents.y * ReferenceAxisY + z * HalfExtents.z * ReferenceAxisZ;
						maxRadiusSq = math.max(maxRadiusSq, math.lengthsq(corner));
					}
				}
			}
			return math.sqrt(maxRadiusSq);
		}

		private static bool PushesIntoActiveStop(in SpringHingeMovementState movement,
			float angularImpulseWithoutMagnitude)
			=> movement.ActiveStop != 0 && movement.ActiveStop * angularImpulseWithoutMagnitude > 0f;

		private static void ApplyAngularImpulse(ref SpringHingeState hinge, float angularImpulse)
		{
			if (hinge.Static.Inertia <= 0f || PushesIntoActiveStop(in hinge.Movement, angularImpulse)) {
				return;
			}
			hinge.Movement.AngularVelocity += angularImpulse / hinge.Static.Inertia;
			if (hinge.Movement.ActiveStop * hinge.Movement.AngularVelocity < -PhysicsConstants.Precision) {
				hinge.Movement.ActiveStop = 0;
			}
		}

		private float GetFriction(ref PhysicsState state, float normalVelocity)
		{
			if (!Header.Material.UseFrictionOverVelocity) {
				return Header.Material.Friction;
			}
			var lut = state.FrictionOverVelocityLUTs[Header.ItemId];
			return lut.InterpolateLUT(0, 127f, -normalVelocity);
		}

		public override string ToString()
			=> $"SpringHingeCollider[{Header.ItemId}] owner {HingeOwnerId}";
	}

	internal readonly struct SpringHingeDistance
	{
		internal readonly float Separation;
		internal readonly float3 Witness;
		internal readonly float3 Normal;

		internal SpringHingeDistance(float separation, in float3 witness, in float3 normal)
		{
			Separation = separation;
			Witness = witness;
			Normal = normal;
		}
	}
}
