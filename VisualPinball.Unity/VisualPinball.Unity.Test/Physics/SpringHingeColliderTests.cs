// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity.Test
{
	public class SpringHingeColliderTests
	{
		[Test]
		public void BoxDistanceCoversFaceEdgeCornerAndInside()
		{
			var collider = CreateCollider();
			var hinge = CreateHinge();

			var face = collider.Distance(in hinge, new float3(15.5f, 0f, 0f), 0.5f);
			var edge = collider.Distance(in hinge, new float3(16f, 3f, 0f), 0.5f);
			var corner = collider.Distance(in hinge, new float3(16f, 3f, 3f), 0.5f);
			var inside = collider.Distance(in hinge, new float3(10f, 0f, 0f), 0.5f);

			Assert.That(face.Separation, Is.EqualTo(0f).Within(1e-6f));
			Assert.That(edge.Separation, Is.EqualTo(math.sqrt(2f) - 0.5f).Within(1e-6f));
			Assert.That(corner.Separation, Is.EqualTo(math.sqrt(3f) - 0.5f).Within(1e-6f));
			Assert.That(inside.Separation, Is.EqualTo(-2.5f).Within(1e-6f));
			Assert.That(math.length(inside.Normal), Is.EqualTo(1f).Within(1e-6f));
		}

		[Test]
		public void LinearSweepFindsFirstFaceImpact()
		{
			var collider = CreateCollider();
			var hinge = CreateHinge();
			var ball = CreateBall(new float3(20f, 0f, 0f), new float3(-10f, 0f, 0f));
			var collEvent = new CollisionEventData();

			var time = collider.HitTest(ref collEvent, in hinge, in ball, 1f);

			Assert.That(time, Is.EqualTo(0.4f).Within(2e-4f));
			Assert.That(collEvent.HitNormal, Is.EqualTo(new float3(1f, 0f, 0f)));
			Assert.That(collEvent.IsContact, Is.False);
		}

		[Test]
		public void RotatingBoxCannotPassThroughStationaryBall()
		{
			var collider = CreateCollider(centreArm: new float3(5f, 0f, 0f),
				halfExtents: new float3(4f, 1f, 1f));
			var hinge = CreateHinge(angularVelocity: math.TAU);
			var ball = CreateBall(new float3(0f, 5f, 0f), float3.zero);
			var collEvent = new CollisionEventData();

			var time = collider.HitTest(ref collEvent, in hinge, in ball, 1f);

			Assert.That(time, Is.GreaterThanOrEqualTo(0f).And.LessThan(0.25f));
			Assert.That(collEvent.HitOrgNormalVelocity, Is.LessThan(0f));
		}

		[Test]
		public void HighSpeedSweepUsesConservativeFallbackWithoutZeroTimeImpact()
		{
			var collider = CreateCollider();
			var hinge = CreateHinge();
			var ball = CreateBall(new float3(20f, 0f, 0f), new float3(-100000f, 0f, 0f));
			var collEvent = new CollisionEventData();

			var time = collider.HitTest(ref collEvent, in hinge, in ball, 0.001f);

			Assert.That(time, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.000045f));
			Assert.That(collEvent.HitOrgNormalVelocity, Is.LessThan(0f));
		}

		[Test]
		public void FallbackPreservesConservativeAdvancementProgress()
		{
			var collider = CreateCollider();
			var hinge = CreateHinge();
			var ball = CreateBall(new float3(16.1f, 0f, 0f), new float3(-17f, 0f, 0f));
			var collEvent = new CollisionEventData();

			var time = collider.HitTest(ref collEvent, in hinge, in ball, 0.1f);

			Assert.That(time, Is.EqualTo(0.1f / 17f).Within(2e-5f));
			Assert.That(math.abs(collEvent.HitDistance), Is.LessThanOrEqualTo(2e-4f));
		}

		[Test]
		public void DistanceSupportsNonBasisReferenceFrame()
		{
			var rotation = quaternion.EulerXYZ(math.radians(new float3(17f, -23f, 31f)));
			var axisX = math.mul(rotation, new float3(1f, 0f, 0f));
			var axisY = math.mul(rotation, new float3(0f, 1f, 0f));
			var axisZ = math.mul(rotation, new float3(0f, 0f, 1f));
			var collider = CreateCollider(referenceAxisX: axisX, referenceAxisY: axisY,
				referenceAxisZ: axisZ);
			var hinge = CreateHinge();

			var distance = collider.Distance(in hinge,
				new float3(10f, 0f, 0f) + axisX * 5.5f, 0.5f);

			Assert.That(distance.Separation, Is.EqualTo(0f).Within(2e-5f));
			Assert.That(math.dot(distance.Normal, axisX), Is.EqualTo(1f).Within(2e-5f));
		}

		[Test]
		public void ImpactIsReciprocalForFiniteHingeInertia()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			var collider = CreateCollider();
			var hinge = CreateHinge();
			var ball = CreateBall(new float3(10f, 3f, 0f), new float3(0f, -2f, 0f));
			var before = ProjectedAngularMomentum(in ball, in hinge);
			var collEvent = new CollisionEventData { HitNormal = new float3(0f, 1f, 0f) };

			collider.Collide(ref ball, ref hinge, in collEvent, ref state);

			var surfaceVelocity = hinge.Movement.AngularVelocity * 10f;
			Assert.That(ball.Velocity.y - surfaceVelocity, Is.GreaterThan(0f));
			Assert.That(hinge.Movement.AngularVelocity, Is.LessThan(0f));
			Assert.That(ProjectedAngularMomentum(in ball, in hinge), Is.EqualTo(before).Within(2e-5f));
		}

		[Test]
		public void SustainedContactAppliesReciprocalSupportImpulse()
		{
			var collider = CreateCollider();
			var hinge = CreateHinge();
			var ball = CreateBall(new float3(10f, 3f, 0f), float3.zero);
			var collEvent = new CollisionEventData {
				HitNormal = new float3(0f, 1f, 0f),
				HitDistance = 0f,
				IsContact = true
			};
			var before = ProjectedAngularMomentum(in ball, in hinge);
			var acceleration = new float3(0f, -0.1f, 0f);

			collider.Contact(ref ball, ref hinge, in collEvent, 0.1f, in acceleration,
				in acceleration, in ball.Velocity, in ball.AngularMomentum);

			Assert.That(ball.Velocity.y, Is.GreaterThan(0f));
			Assert.That(hinge.Movement.AngularVelocity, Is.LessThan(0f));
			Assert.That(ProjectedAngularMomentum(in ball, in hinge), Is.EqualTo(before).Within(2e-5f));
		}

		[Test]
		public void FullTravelBoundContainsAllRotatedCorners()
		{
			var collider = CreateCollider(centreArm: new float3(5f, 3f, 2f),
				halfExtents: new float3(4f, 2f, 1f));
			var radius = collider.MaxProxyRadius;

			Assert.That(collider.Bounds.Aabb.Left, Is.EqualTo(-radius));
			Assert.That(collider.Bounds.Aabb.Right, Is.EqualTo(radius));
			Assert.That(collider.Bounds.Aabb.Top, Is.EqualTo(-radius));
			Assert.That(collider.Bounds.Aabb.Bottom, Is.EqualTo(radius));
			Assert.That(collider.Bounds.Aabb.ZLow, Is.EqualTo(-radius));
			Assert.That(collider.Bounds.Aabb.ZHigh, Is.EqualTo(radius));
			var bounds = collider.Bounds.Aabb;
			for (var angleIndex = 0; angleIndex < 9; angleIndex++) {
				var angle = math.TAU * angleIndex / 9f;
				for (var x = -1; x <= 1; x += 2) {
					for (var y = -1; y <= 1; y += 2) {
						for (var z = -1; z <= 1; z += 2) {
							var corner = new float3(5f, 3f, 2f) + new float3(4f * x, 2f * y, z);
							var rotated = SpringHingeVelocityPhysics.RotateAroundAxis(in corner,
								new float3(0f, 0f, 1f), angle);
							Assert.That(rotated.x, Is.InRange(bounds.Left, bounds.Right));
							Assert.That(rotated.y, Is.InRange(bounds.Top, bounds.Bottom));
							Assert.That(rotated.z, Is.InRange(bounds.ZLow, bounds.ZHigh));
						}
					}
				}
			}
		}

		[Test]
		public void ComponentGeometryUsesAuthoredVpxUnits()
		{
			var hingeObject = new GameObject("spring-hinge-vpx-units-test");
			var magnetObject = new GameObject("owned-magnet-vpx-units-test");
			try {
				var hinge = hingeObject.AddComponent<SpringHingeComponent>();
				hinge.CentreOfMass = new Vector3(50f, 0f, 0f);
				var proxy = hingeObject.AddComponent<SpringHingeColliderComponent>();
				proxy.LocalCentre = new Vector3(30f, 0f, 0f);
				proxy.HalfExtents = new Vector3(10f, 20f, 30f);

				var hingeState = hinge.CreateState();
				var collider = SpringHingeColliderGenerator.Create(hinge, proxy,
					new ColliderInfo { ItemId = hinge.ItemId }, 0f);

				magnetObject.transform.SetParent(hingeObject.transform, false);
				var magnet = magnetObject.AddComponent<MagnetComponent>();
				magnet.MagnetType = MagnetType.Spatial;
				magnet.ForceProfile = MagnetForceProfile.Physical;
				magnet.CoupleToParentHinge = true;
				magnet.HeldBallCentreOffset = new Vector3(25f, 0f, 0f);
				var magnetState = magnet.CreateState();

				Assert.That(math.distance(hingeState.Static.CentreOfMassArm,
					new float3(50f, 0f, 0f)), Is.LessThan(1e-4f));
				Assert.That(math.distance(collider.CentreArm,
					new float3(30f, 0f, 0f)), Is.LessThan(1e-4f));
				Assert.That(math.distance(collider.HalfExtents,
					new float3(10f, 20f, 30f)), Is.LessThan(1e-4f));
				Assert.That(math.distance(magnetState.LocalHeldCentreArm,
					new float3(25f, 0f, 0f)), Is.LessThan(1e-4f));
			} finally {
				UnityEngine.Object.DestroyImmediate(hingeObject);
			}
		}

		[Test]
		public void GeneratorRejectsShearedBoxFrame()
		{
			var gameObject = new GameObject("spring-hinge-sheared-test");
			try {
				gameObject.transform.localScale = new Vector3(2f, 1f, 1f);
				var hinge = gameObject.AddComponent<SpringHingeComponent>();
				var collider = gameObject.AddComponent<SpringHingeColliderComponent>();
				collider.LocalRotation = new Vector3(0f, 0f, 45f);
				var info = new ColliderInfo { ItemId = hinge.ItemId };

				Assert.Throws<InvalidOperationException>(() =>
					SpringHingeColliderGenerator.Create(hinge, collider, info, 0f));
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void SpecializedColliderRoundTripsThroughNativeStorage()
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var id = references.Add(CreateCollider());
				var native = new NativeColliders(ref references, Allocator.Temp);
				try {
					Assert.That(native.GetHeader(id).Type, Is.EqualTo(ColliderType.SpringHinge));
					Assert.That(native.SpringHinge(id).HingeOwnerId, Is.EqualTo(12));
					Assert.That(native.GetAabb(id), Is.EqualTo(CreateCollider().Bounds.Aabb));
					Assert.That(native.ToArray()[id], Is.TypeOf<SpringHingeCollider>());
				} finally {
					native.Dispose();
				}
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void PhysicsStateDispatchesHitAndReciprocalCollision()
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			using var harness = new PhysicsStateHarness();
			try {
				var colliderId = references.Add(CreateCollider());
				harness.SetStaticColliders(ref references);
				var hinge = CreateHinge();
				harness.SpringHingeStates.Add(hinge.AnimationItemId, hinge);
				var state = harness.CreateState();
				var ball = CreateBall(new float3(10f, 3f, 0f), new float3(0f, -2f, 0f));
				ball.CollisionEvent.ClearCollider(0.1f);
				var collEvent = new CollisionEventData();
				var contacts = new NativeList<ContactBufferElement>(Allocator.Temp);
				try {
					var hitTime = state.HitTest(ref state.Colliders, colliderId, ref ball,
						ref collEvent, ref contacts);
					Assert.That(hitTime, Is.EqualTo(0f));
					collEvent.SetCollider(colliderId, false);
					collEvent.HitTime = hitTime;
					ball.CollisionEvent = collEvent;
					var before = ProjectedAngularMomentum(in ball, in hinge);

					PhysicsStaticCollision.Collide(hitTime, ref ball, ref state);

					ref var updatedHinge = ref state.SpringHingeStates.GetValueByRef(hinge.AnimationItemId);
					Assert.That(updatedHinge.Movement.AngularVelocity, Is.LessThan(0f));
					Assert.That(ProjectedAngularMomentum(in ball, in updatedHinge),
						Is.EqualTo(before).Within(2e-5f));
				} finally {
					contacts.Dispose();
				}
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void StopBearingBlocksOnlyOutwardImpact()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			var collider = CreateCollider();
			var hinge = CreateHinge();
			hinge.Movement.ActiveStop = 1;
			var outwardBall = CreateBall(new float3(10f, -3f, 0f), new float3(0f, 2f, 0f));
			var outwardEvent = new CollisionEventData { HitNormal = new float3(0f, -1f, 0f) };

			collider.Collide(ref outwardBall, ref hinge, in outwardEvent, ref state);

			Assert.That(hinge.Movement.AngularVelocity, Is.Zero);
			Assert.That(outwardBall.Velocity.y, Is.LessThan(0f));

			var inwardBall = CreateBall(new float3(10f, 3f, 0f), new float3(0f, -2f, 0f));
			var inwardEvent = new CollisionEventData { HitNormal = new float3(0f, 1f, 0f) };
			collider.Collide(ref inwardBall, ref hinge, in inwardEvent, ref state);

			Assert.That(hinge.Movement.AngularVelocity, Is.LessThan(0f));
			Assert.That(hinge.Movement.ActiveStop, Is.Zero);
		}

		[Test]
		public void HitTestPreservesVelocityDepartingEitherStop()
		{
			var collider = CreateCollider();
			var lower = CreateHinge(angularVelocity: 1f);
			lower.Static.MinimumAngle = 0f;
			lower.Static.MaximumAngle = math.PI;
			lower.Movement.Angle = 0f;
			var lowerBall = CreateBall(new float3(10f, 3f, 0f), float3.zero);
			var lowerEvent = new CollisionEventData();

			var lowerTime = collider.HitTest(ref lowerEvent, in lower, in lowerBall, 0.1f);

			Assert.That(lowerTime, Is.Zero);
			Assert.That(lowerEvent.HitOrgNormalVelocity, Is.EqualTo(-10f).Within(1e-5f));
			Assert.That(lowerEvent.IsContact, Is.False);

			var upper = CreateHinge(angularVelocity: -1f);
			upper.Static.MinimumAngle = -math.PI;
			upper.Static.MaximumAngle = 0f;
			upper.Movement.Angle = 0f;
			var upperBall = CreateBall(new float3(10f, -3f, 0f), float3.zero);
			var upperEvent = new CollisionEventData();

			var upperTime = collider.HitTest(ref upperEvent, in upper, in upperBall, 0.1f);

			Assert.That(upperTime, Is.Zero);
			Assert.That(upperEvent.HitOrgNormalVelocity, Is.EqualTo(-10f).Within(1e-5f));
			Assert.That(upperEvent.IsContact, Is.False);
		}

		[Test]
		public void ContactPhysicsDispatchesReciprocalHingeContact()
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			using var harness = new PhysicsStateHarness();
			try {
				var colliderId = references.Add(CreateCollider());
				harness.SetStaticColliders(ref references);
				var hinge = CreateHinge();
				harness.SpringHingeStates.Add(hinge.AnimationItemId, hinge);
				var state = harness.CreateState();
				var ball = CreateBall(new float3(10f, 3f, 0f), float3.zero);
				ball.ExternalAcceleration = new float3(0f, -0.1f, 0f);
				var contact = new ContactBufferElement(ball.Id, new CollisionEventData {
					ColliderId = colliderId,
					HitNormal = new float3(0f, 1f, 0f),
					IsContact = true
				}) {
					FrictionAcceleration = ball.ExternalAcceleration
				};
				var colliders = state.Colliders;

				ContactPhysics.Update(ref contact, ref ball, ref state, ref colliders, 0.1f);

				ref var updatedHinge = ref state.SpringHingeStates.GetValueByRef(hinge.AnimationItemId);
				Assert.That(ball.Velocity.y, Is.GreaterThan(0f));
				Assert.That(updatedHinge.Movement.AngularVelocity, Is.LessThan(0f));
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		private static SpringHingeCollider CreateCollider(float3? centreArm = null,
			float3? halfExtents = null, float3? referenceAxisX = null,
			float3? referenceAxisY = null, float3? referenceAxisZ = null)
		{
			var pivot = float3.zero;
			var centre = centreArm ?? new float3(10f, 0f, 0f);
			var extents = halfExtents ?? new float3(5f, 2f, 2f);
			var x = referenceAxisX ?? new float3(1f, 0f, 0f);
			var y = referenceAxisY ?? new float3(0f, 1f, 0f);
			var z = referenceAxisZ ?? new float3(0f, 0f, 1f);
			var info = new ColliderInfo {
				ItemId = 12,
				Material = new PhysicsMaterialData {
					Elasticity = 0.5f,
					Friction = 0.3f
				}
			};
			return new SpringHingeCollider(12, in pivot, in centre, in extents, in x, in y, in z, info);
		}

		private static SpringHingeState CreateHinge(float angularVelocity = 0f)
		{
			return new SpringHingeState(12, new SpringHingeStaticState {
				OwnerId = 12,
				Pivot = float3.zero,
				Axis = new float3(0f, 0f, 1f),
				Mass = 1f,
				Inertia = 20f,
				MinimumAngle = -math.PI,
				MaximumAngle = math.PI
			}, new SpringHingeMovementState {
				AngularVelocity = angularVelocity
			});
		}

		private static BallState CreateBall(in float3 position, in float3 velocity)
		{
			return new BallState {
				Id = 1,
				Position = position,
				Velocity = velocity,
				Mass = 1f,
				Radius = 1f
			};
		}

		private static float ProjectedAngularMomentum(in BallState ball, in SpringHingeState hinge)
			=> math.dot(hinge.Static.Axis, math.cross(ball.Position - hinge.Static.Pivot,
				ball.Mass * ball.Velocity) + ball.AngularMomentum)
				+ hinge.Static.Inertia * hinge.Movement.AngularVelocity;
	}
}
