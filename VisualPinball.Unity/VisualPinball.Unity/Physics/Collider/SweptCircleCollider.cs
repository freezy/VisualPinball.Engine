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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A round cord segment: the Minkowski sum of a line segment and a sphere.
	/// Ball CCD is an analytic ray/capsule test using the sum of both radii.
	/// </summary>
	internal struct SweptCircleCollider : ICollider
	{
		public int Id {
			get => Header.Id;
			set {
				Header.Id = value;
				var bounds = Bounds;
				bounds.ColliderId = value;
				Bounds = bounds;
			}
		}

		public ColliderHeader Header;
		public float3 V1;
		public float3 V2;
		public float CordRadius;
		public ColliderBounds Bounds { get; private set; }

		public SweptCircleCollider(float3 v1, float3 v2, float cordRadius,
			ColliderInfo info) : this()
		{
			Header.Init(info, ColliderType.SweptCircle);
			V1 = v1;
			V2 = v2;
			CordRadius = cordRadius;
			CalculateBounds();
		}

		public float HitTest(ref CollisionEventData collEvent, in BallState ball, float dTime)
		{
			var radius = ball.Radius + CordRadius;
			var axis = V2 - V1;
			var axisLengthSquared = math.lengthsq(axis);
			if (axisLengthSquared <= 1e-10f || radius <= 0f) {
				return -1f;
			}

			var closest = ClosestPoint(ball.Position, V1, axis, axisLengthSquared);
			var separation = ball.Position - closest;
			var distance = math.length(separation);
			var normal = SafeNormal(separation, ball.Velocity, axis);
			var normalVelocity = math.dot(normal, ball.Velocity);
			var hitDistance = distance - radius;
			// The capsule is convex, so the closest-point normal defines a separating
			// plane. A ball moving away from that plane cannot hit another feature.
			if (normalVelocity > PhysicsConstants.ContactVel) {
				return -1f;
			}

			float hitTime;
			var isContact = false;
			if (hitDistance < PhysicsConstants.PhysTouch) {
				if (hitDistance <= 0f) {
					hitTime = 0f;
				} else if (math.abs(normalVelocity) <= PhysicsConstants.ContactVel) {
					isContact = true;
					hitTime = 0f;
				} else {
					hitTime = -hitDistance / normalVelocity;
				}
			} else {
				hitTime = FirstRayCapsuleHit(ball.Position, ball.Velocity, V1, V2,
					radius, dTime);
				if (hitTime < 0f) {
					return -1f;
				}
				var hitPosition = ball.Position + ball.Velocity * hitTime;
				closest = ClosestPoint(hitPosition, V1, axis, axisLengthSquared);
				normal = SafeNormal(hitPosition - closest, ball.Velocity, axis);
				normalVelocity = math.dot(normal, ball.Velocity);
				if (normalVelocity > PhysicsConstants.ContactVel) {
					return -1f;
				}
				hitDistance = math.distance(hitPosition, closest) - radius;
			}

			if (!float.IsFinite(hitTime) || hitTime < 0f || hitTime > dTime) {
				return -1f;
			}
			collEvent.HitNormal = normal;
			collEvent.IsContact = isContact;
			if (isContact) {
				collEvent.HitOrgNormalVelocity = normalVelocity;
			}
			collEvent.HitDistance = hitDistance;
			return hitTime;
		}

		public void Collide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, ref PhysicsState state)
		{
			var impactSpeed = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in Header.Material, in collEvent,
				in collEvent.HitNormal, ref state);
			if (Header.FireEvents && impactSpeed >= Header.Threshold) {
				Collider.FireHitEvent(ref ball, ref hitEvents, in Header);
			}
		}

		public static bool IsTransformable(float4x4 matrix)
		{
			var scale = matrix.GetScale();
			if (!Collider.HasEqualScale(scale.x, scale.y)
				|| !Collider.HasEqualScale(scale.x, scale.z)
				|| scale.x <= Collider.Tolerance) {
				return false;
			}

			var x = matrix.c0.xyz / scale.x;
			var y = matrix.c1.xyz / scale.y;
			var z = matrix.c2.xyz / scale.z;
			return math.abs(math.dot(x, y)) <= Collider.Tolerance
				&& math.abs(math.dot(x, z)) <= Collider.Tolerance
				&& math.abs(math.dot(y, z)) <= Collider.Tolerance;
		}

		public SweptCircleCollider Transform(float4x4 matrix)
		{
			Transform(this, matrix);
			return this;
		}

		public void Transform(SweptCircleCollider collider, float4x4 matrix)
		{
			if (!IsTransformable(matrix)) {
				throw new System.InvalidOperationException("A swept-circle collider requires uniform scale.");
			}
			V1 = matrix.MultiplyPoint(collider.V1);
			V2 = matrix.MultiplyPoint(collider.V2);
			CordRadius = collider.CordRadius * math.abs(matrix.GetScale().x);
			CalculateBounds();
		}

		public Aabb GetTransformedAabb(float4x4 matrix)
		{
			var v1 = matrix.MultiplyPoint(V1);
			var v2 = matrix.MultiplyPoint(V2);
			var radius = CordRadius * math.cmax(math.abs(matrix.GetScale()));
			return new Aabb(math.min(v1, v2) - radius, math.max(v1, v2) + radius);
		}

		public SweptCircleCollider TransformAabb(float4x4 matrix)
		{
			Bounds = new ColliderBounds(Header.ItemId, Header.Id, GetTransformedAabb(matrix));
			return this;
		}

		private void CalculateBounds()
		{
			Bounds = new ColliderBounds(Header.ItemId, Header.Id,
				new Aabb(math.min(V1, V2) - CordRadius, math.max(V1, V2) + CordRadius));
		}

		private static float3 ClosestPoint(float3 point, float3 start, float3 axis,
			float axisLengthSquared)
		{
			return start + axis * math.saturate(math.dot(point - start, axis) / axisLengthSquared);
		}

		private static float3 SafeNormal(float3 separation, float3 velocity, float3 axis)
		{
			var lengthSquared = math.lengthsq(separation);
			if (lengthSquared > 1e-12f) {
				return separation / math.sqrt(lengthSquared);
			}
			if (math.lengthsq(velocity) > 1e-12f) {
				return -math.normalize(velocity);
			}
			var perpendicular = math.cross(axis, new float3(0f, 0f, 1f));
			if (math.lengthsq(perpendicular) <= 1e-12f) {
				perpendicular = math.cross(axis, new float3(0f, 1f, 0f));
			}
			return math.normalizesafe(perpendicular, new float3(1f, 0f, 0f));
		}

		private static float FirstRayCapsuleHit(float3 origin, float3 velocity,
			float3 start, float3 end, float radius, float maximumTime)
		{
			var axis = end - start;
			var axisLengthSquared = math.lengthsq(axis);
			var offset = origin - start;
			var offsetAlongAxis = math.dot(offset, axis);
			var velocityAlongAxis = math.dot(velocity, axis);
			var velocitySquared = math.lengthsq(velocity);
			var offsetVelocity = math.dot(offset, velocity);

			var best = float.PositiveInfinity;
			var a = axisLengthSquared * velocitySquared - velocityAlongAxis * velocityAlongAxis;
			var b = axisLengthSquared * offsetVelocity - offsetAlongAxis * velocityAlongAxis;
			var c = axisLengthSquared * (math.lengthsq(offset) - radius * radius)
				- offsetAlongAxis * offsetAlongAxis;
			if (math.abs(a) > 1e-10f) {
				var discriminant = b * b - a * c;
				if (discriminant >= 0f) {
					var root = (-b - math.sqrt(discriminant)) / a;
					var axial = offsetAlongAxis + root * velocityAlongAxis;
					if (root >= 0f && root <= maximumTime && axial > 0f
						&& axial < axisLengthSquared) {
						best = root;
					}
				}
			}

			best = math.min(best, FirstRaySphereHit(origin, velocity, start, radius, maximumTime));
			best = math.min(best, FirstRaySphereHit(origin, velocity, end, radius, maximumTime));
			return float.IsFinite(best) ? best : -1f;
		}

		private static float FirstRaySphereHit(float3 origin, float3 velocity,
			float3 center, float radius, float maximumTime)
		{
			var offset = origin - center;
			var a = math.lengthsq(velocity);
			if (a <= 1e-12f) {
				return float.PositiveInfinity;
			}
			var b = math.dot(offset, velocity);
			var c = math.lengthsq(offset) - radius * radius;
			var discriminant = b * b - a * c;
			if (discriminant < 0f) {
				return float.PositiveInfinity;
			}
			var root = (-b - math.sqrt(discriminant)) / a;
			return root >= 0f && root <= maximumTime ? root : float.PositiveInfinity;
		}
	}
}
