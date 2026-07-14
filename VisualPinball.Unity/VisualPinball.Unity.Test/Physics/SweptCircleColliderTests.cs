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
using UnityEngine.TestTools;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Test
{
	public class SweptCircleColliderTests
	{
		[Test]
		public void ShouldHitRoundCordSideWithoutTunneling()
		{
			var collider = CreateCollider();
			var ball = new BallState {
				Position = new float3(0f, 100f, 0f),
				Velocity = new float3(0f, -1000f, 0f),
				Radius = 1f,
			};
			var collision = new CollisionEventData();

			var hitTime = collider.HitTest(ref collision, in ball, 0.2f);

			Assert.That(hitTime, Is.EqualTo(0.097f).Within(1e-5f));
			Assert.That(math.distance(collision.HitNormal, new float3(0f, 1f, 0f)), Is.LessThan(1e-5f));
		}

		[Test]
		public void ShouldHitSphericalEndCap()
		{
			var collider = CreateCollider();
			var ball = new BallState {
				Position = new float3(15f, 0f, 0f),
				Velocity = new float3(-10f, 0f, 0f),
				Radius = 1f,
			};
			var collision = new CollisionEventData();

			var hitTime = collider.HitTest(ref collision, in ball, 1f);

			Assert.That(hitTime, Is.EqualTo(0.2f).Within(1e-5f));
			Assert.That(collision.HitNormal.x, Is.EqualTo(1f).Within(1e-5f));
		}

		[Test]
		public void ShouldHitEndCapWhileMovingParallelToAxis()
		{
			var collider = CreateCollider();
			var ball = new BallState {
				Position = new float3(-20f, 2f, 0f),
				Velocity = new float3(10f, 0f, 0f),
				Radius = 1f,
			};
			var collision = new CollisionEventData();

			var hitTime = collider.HitTest(ref collision, in ball, 1f);

			Assert.That(hitTime, Is.EqualTo((10f - math.sqrt(5f)) / 10f).Within(1e-5f));
			Assert.That(collision.HitNormal.y, Is.GreaterThan(0f));
		}

		[Test]
		public void ShouldMissOutsideCapsuleRadius()
		{
			var collider = CreateCollider();
			var ball = new BallState {
				Position = new float3(20f, 4f, 0f),
				Velocity = new float3(-40f, 0f, 0f),
				Radius = 1f,
			};
			var collision = new CollisionEventData();

			Assert.That(collider.HitTest(ref collision, in ball, 1f), Is.EqualTo(-1f));
		}

		[Test]
		public void ShouldReportRestingContactWithoutJitterImpulse()
		{
			var collider = CreateCollider();
			var ball = new BallState {
				Position = new float3(0f, 3.001f, 0f),
				Velocity = float3.zero,
				Radius = 1f,
			};
			var collision = new CollisionEventData();

			var hitTime = collider.HitTest(ref collision, in ball, 0.01f);

			Assert.That(hitTime, Is.Zero);
			Assert.That(collision.IsContact, Is.True);
			Assert.That(collision.HitOrgNormalVelocity, Is.Zero);
		}

		[Test]
		public void ShouldReportSlightPenetrationAsRestingContact()
		{
			var collider = CreateCollider();
			var ball = new BallState {
				Position = new float3(0f, 2.99f, 0f),
				Velocity = float3.zero,
				Radius = 1f,
			};
			var collision = new CollisionEventData();

			var hitTime = collider.HitTest(ref collision, in ball, 0.01f);

			Assert.That(hitTime, Is.Zero);
			Assert.That(collision.IsContact, Is.True);
			Assert.That(collision.HitDistance, Is.EqualTo(-0.01f).Within(1e-5f));
		}

		[Test]
		public void ShouldTreatDegenerateCordSegmentAsSphere()
		{
			var collider = new SweptCircleCollider(float3.zero, float3.zero, 2f,
				new ColliderInfo { ItemId = 1 });
			var ball = new BallState {
				Position = new float3(0f, 10f, 0f),
				Velocity = new float3(0f, -10f, 0f),
				Radius = 1f,
			};
			var collision = new CollisionEventData();

			Assert.That(collider.HitTest(ref collision, in ball, 1f),
				Is.EqualTo(0.7f).Within(1e-5f));
			Assert.That(collision.HitNormal.y, Is.EqualTo(1f).Within(1e-5f));
		}

		[Test]
		public void ShouldIncludeCordRadiusInBounds()
		{
			var collider = CreateCollider();

			Assert.That(math.distance(collider.Bounds.Aabb.Min, new float3(-12f, -2f, -2f)), Is.LessThan(1e-5f));
			Assert.That(math.distance(collider.Bounds.Aabb.Max, new float3(12f, 2f, 2f)), Is.LessThan(1e-5f));
		}

		[Test]
		public void ShouldTransformEndpointsAndRadiusWithUniformScale()
		{
			var collider = CreateCollider().Transform(float4x4.TRS(
				new float3(3f, 4f, 5f), quaternion.RotateZ(math.PI * 0.5f), new float3(2f)));

			Assert.That(math.distance(collider.V1, new float3(3f, -16f, 5f)), Is.LessThan(1e-5f));
			Assert.That(math.distance(collider.V2, new float3(3f, 24f, 5f)), Is.LessThan(1e-5f));
			Assert.That(collider.CordRadius, Is.EqualTo(4f).Within(1e-5f));
		}

		[Test]
		public void ShouldRejectShearedTransform()
		{
			var shear = float4x4.identity;
			shear.c1.x = 0.25f;

			Assert.That(SweptCircleCollider.IsTransformable(shear), Is.False);
			Assert.Throws<System.InvalidOperationException>(() => CreateCollider().Transform(shear));
		}

		[Test]
		public void ShouldSurviveNativeColliderAllocation()
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				references.Add(CreateCollider(), float4x4.identity);
				using var native = new NativeColliders(ref references, Allocator.Temp);

				Assert.That(native.Length, Is.EqualTo(1));
				Assert.That(native.GetHeader(0).Type, Is.EqualTo(ColliderType.SweptCircle));
				Assert.That(native.SweptCircle(0).CordRadius, Is.EqualTo(2f));
				Assert.That(native.ToArray()[0], Is.TypeOf<SweptCircleCollider>());
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void KinematicRegistrationShouldKeepCordInLocalSpace()
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var kinematicTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp, true);
			var matrix = float4x4.TRS(new float3(3f, 4f, 5f), quaternion.RotateZ(0.25f),
				new float3(2f, 3f, 4f));
			try {
				references.Add(CreateCollider(), matrix);

				Assert.That(references.SweptCircleColliders[0].Header.IsTransformed, Is.False);
				Assert.That(math.distance(references.SweptCircleColliders[0].V1,
					new float3(-10f, 0f, 0f)), Is.LessThan(1e-5f));
				Assert.That(transforms.TryGetValue(1, out var storedMatrix), Is.True);
				Assert.That(storedMatrix.Equals(matrix), Is.True);
				var ball = new BallState {
					Position = matrix.MultiplyPoint(new float3(0f, 4f, 0f)),
					Velocity = matrix.MultiplyVector(new float3(0f, -10f, 0f)),
					Radius = 1f,
				};
				ball.Transform(math.inverse(storedMatrix));
				var collision = new CollisionEventData();
				Assert.That(references.SweptCircleColliders[0].HitTest(
					ref collision, in ball, 0.2f), Is.EqualTo(0.1f).Within(1e-5f));

				kinematicTransforms.Add(1, matrix);
				references.TransformToIdentity(ref kinematicTransforms);
				Assert.That(references.SweptCircleColliders[0].Header.IsTransformed, Is.False);
			} finally {
				references.Dispose();
				kinematicTransforms.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void StaticRegistrationShouldRejectNonuniformScale()
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			var matrix = float4x4.Scale(new float3(1f, 2f, 1f));
			try {
				Assert.Throws<InvalidOperationException>(() => references.Add(
					CreateCollider(), matrix));
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void ArcChordSubdivisionShouldRespectSagittaTolerance()
		{
			const float radius = 30f;
			const float tolerance = RubberPhysicalColliderGenerator.ArcChordToleranceVpx;
			var angle = RubberPhysicalColliderGenerator.MaximumChordAngle(radius, tolerance);

			var sagitta = radius * (1f - math.cos(angle * 0.5f));
			Assert.That(sagitta, Is.LessThanOrEqualTo(tolerance + 1e-5f));
		}

		[Test]
		public void ThinCordShouldUseRadiusRelativeChordTolerance()
		{
			Assert.That(RubberPhysicalColliderGenerator.ChordTolerance(0.5f),
				Is.EqualTo(RubberPhysicalColliderGenerator.ArcChordRadiusFraction * 0.5f)
					.Within(1e-6f));
			Assert.That(RubberPhysicalColliderGenerator.ChordTolerance(4f),
				Is.EqualTo(RubberPhysicalColliderGenerator.ArcChordToleranceVpx)
					.Within(1e-6f));
		}

		[Test]
		public void InvalidPhysicalRubberShouldGenerateLegacyFallbackColliders()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				rubber.DragPoints = new[] {
					new DragPointData(-10f, -10f),
					new DragPointData(-10f, 10f),
					new DragPointData(10f, 10f),
					new DragPointData(10f, -10f),
				};
				var collider = go.AddComponent<RubberColliderComponent>();
				collider.Mode = RubberColliderMode.Physical;
				var api = new TestRubberApi(go);
				LogAssert.Expect(LogType.Warning,
					"Physical rubber 'Rubber' has no current valid guided bake; falling back to Legacy collision.");

				api.GenerateColliders(ref references);

				Assert.That(references.Count, Is.GreaterThan(0));
				Assert.That(references.SweptCircleColliders.Length, Is.Zero);
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void PhysicalRubberGeneratorShouldCreateContinuousRoundCordSegments()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				go.AddComponent<RubberColliderComponent>();
				rubber._thickness = 8;
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				rubber.ApplyGuidedBake(new[] {
					new RubberPathElement {
						Type = RubberPathElementType.FreeSpan,
						Start = new float2(-10f, 0f),
						End = new float2(10f, 0f),
					},
					new RubberPathElement {
						Type = RubberPathElementType.SupportedArc,
						Start = new float2(10f, 0f),
						End = new float2(0f, 10f),
						Center = float2.zero,
						Radius = 10f,
						StartAngleRad = 0f,
						SweepAngleRad = math.PI * 0.5f,
					},
				}, null, Matrix4x4.identity, default, 1);
				var api = new RubberApi(go, null, null);

				new RubberPhysicalColliderGenerator(api, rubber, float4x4.identity)
					.GenerateColliders(2f, ref references, 1f);

				Assert.That(references.SweptCircleColliders.Length, Is.GreaterThan(2));
				Assert.That(references.SweptCircleColliders[0].CordRadius, Is.EqualTo(5f));
				Assert.That(references.SweptCircleColliders[0].V1.z, Is.EqualTo(2f));
				for (var i = 1; i < references.SweptCircleColliders.Length; i++) {
					Assert.That(math.distance(references.SweptCircleColliders[i - 1].V2,
						references.SweptCircleColliders[i].V1), Is.LessThan(1e-5f));
				}
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void PhysicalRubberGeneratorShouldApplyOffsetAlongBakeNormal()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				go.AddComponent<RubberColliderComponent>();
				rubber._thickness = 8;
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				var bakeFrame = Matrix4x4.identity;
				bakeFrame.SetColumn(0, new Vector4(0f, 1f, 0f, 0f));
				bakeFrame.SetColumn(1, new Vector4(0f, 0f, 1f, 0f));
				bakeFrame.SetColumn(2, new Vector4(1f, 0f, 0f, 0f));
				bakeFrame.SetColumn(3, new Vector4(3f, 4f, 5f, 1f));
				rubber.ApplyGuidedBake(new[] {
					new RubberPathElement {
						Type = RubberPathElementType.FreeSpan,
						Start = new float2(-10f, 0f),
						End = new float2(10f, 0f),
					},
				}, null, bakeFrame, default, 1);
				var api = new RubberApi(go, null, null);

				new RubberPhysicalColliderGenerator(api, rubber, float4x4.identity)
					.GenerateColliders(2f, ref references, 0f);

				Assert.That(references.SweptCircleColliders.Length, Is.EqualTo(1));
				Assert.That(math.distance(references.SweptCircleColliders[0].V1,
					new float3(5f, -6f, 5f)), Is.LessThan(1e-5f));
				Assert.That(math.distance(references.SweptCircleColliders[0].V2,
					new float3(5f, 14f, 5f)), Is.LessThan(1e-5f));
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		private sealed class TestRubberApi : RubberApi
		{
			internal TestRubberApi(GameObject go) : base(go, null, null)
			{
			}

			internal void GenerateColliders(ref ColliderReference references)
				=> CreateColliders(ref references, float4x4.identity, 0f);
		}

		private static SweptCircleCollider CreateCollider()
		{
			return new SweptCircleCollider(new float3(-10f, 0f, 0f),
				new float3(10f, 0f, 0f), 2f, new ColliderInfo { ItemId = 1 });
		}
	}
}
