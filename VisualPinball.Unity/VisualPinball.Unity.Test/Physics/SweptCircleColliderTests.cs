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
using VisualPinball.Engine.VPT;

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
		public void CopyTransformShouldPreserveHeaderAndElasticityScale()
		{
			var source = CreateCollider(0.35f);
			var transformed = default(SweptCircleCollider);

			transformed.Transform(source, float4x4.Translate(new float3(3f, 4f, 5f)));

			Assert.That(transformed.Header.ItemId, Is.EqualTo(source.Header.ItemId));
			Assert.That(transformed.Header.Type, Is.EqualTo(ColliderType.SweptCircle));
			Assert.That(transformed.ElasticityScale, Is.EqualTo(0.35f));
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
				references.Add(CreateCollider(0.35f), float4x4.identity);
				using var native = new NativeColliders(ref references, Allocator.Temp);

				Assert.That(native.Length, Is.EqualTo(1));
				Assert.That(native.GetHeader(0).Type, Is.EqualTo(ColliderType.SweptCircle));
				Assert.That(native.SweptCircle(0).CordRadius, Is.EqualTo(2f));
				Assert.That(native.SweptCircle(0).ElasticityScale, Is.EqualTo(0.35f));
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
				references.Add(CreateCollider(0.35f), matrix);

				Assert.That(references.SweptCircleColliders[0].Header.IsTransformed, Is.False);
				Assert.That(references.SweptCircleColliders[0].ElasticityScale, Is.EqualTo(0.35f));
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
				Assert.That(references.SweptCircleColliders[0].ElasticityScale, Is.EqualTo(0.35f));
				using var native = new NativeColliders(ref references, Allocator.Temp);
				Assert.That(native.SweptCircle(0).ElasticityScale, Is.EqualTo(0.35f));
			} finally {
				references.Dispose();
				kinematicTransforms.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void KinematicCollisionPathShouldApplyElasticityScaleAndRestoreBallSpace()
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var kinematicTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var kinematicVelocities = new NativeParallelHashMap<int, KinematicVelocityState>(1,
				Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp, true);
			var hitEvents = new NativeQueue<EventData>(Allocator.Temp);
			var matrix = float4x4.Translate(new float3(100f, 0f, 0f));
			try {
				references.Add(new SweptCircleCollider(new float3(-10f, 0f, 0f),
					new float3(10f, 0f, 0f), 2f, new ColliderInfo {
						ItemId = 1,
						Material = new PhysicsMaterialData { Elasticity = 0.8f },
					}, 0.5f), matrix);
				using var current = new NativeColliders(ref references, Allocator.Temp);
				kinematicTransforms.Add(1, matrix);
				references.TransformToIdentity(ref kinematicTransforms);
				using var identity = new NativeColliders(ref references, Allocator.Temp);
				var writer = hitEvents.AsParallelWriter();
				var state = new PhysicsState {
					KinematicColliders = current,
					KinematicCollidersAtIdentity = identity,
					KinematicTransforms = kinematicTransforms,
					KinematicVelocities = kinematicVelocities,
					EventQueue = writer,
				};
				var ball = ImpactingBall();
				ball.Position += new float3(100f, 0f, 0f);
				ball.CollisionEvent = ImpactEvent(0);
				ball.CollisionEvent.IsKinematic = true;

				PhysicsStaticCollision.Collide(0f, ref ball, ref state);

				Assert.That(ball.Velocity.y, Is.EqualTo(4f).Within(1e-5f));
				Assert.That(math.distance(ball.Position, new float3(100f, 3f, 0f)),
					Is.LessThan(1e-5f));
				Assert.That(current.SweptCircle(0).ElasticityScale, Is.EqualTo(0.5f));
				Assert.That(identity.SweptCircle(0).ElasticityScale, Is.EqualTo(0.5f));
			} finally {
				hitEvents.Dispose();
				references.Dispose();
				kinematicVelocities.Dispose();
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
		public void BezierFreeSpanSubdivisionShouldRespectChordTolerance()
		{
			var span = new RubberPathElement {
				Type = RubberPathElementType.FreeSpan,
				Start = new float2(-50f, 0f),
				End = new float2(50f, 0f),
				Control1 = new float2(-50f, 50f),
				Control2 = new float2(50f, 50f),
			};
			const float tolerance = RubberPhysicalColliderGenerator.ArcChordToleranceVpx;
			var subdivisions = RubberPathMath.FreeSpanSubdivisionCount(span, tolerance);

			for (var segment = 0; segment < subdivisions; segment++) {
				var start = RubberPathMath.PointAt(span, segment / (float)subdivisions);
				var end = RubberPathMath.PointAt(span, (segment + 1f) / subdivisions);
				var midpoint = RubberPathMath.PointAt(span, (segment + 0.5f) / subdivisions);
				Assert.That(math.distance(midpoint, (start + end) * 0.5f),
					Is.LessThanOrEqualTo(tolerance + 1e-5f));
			}
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
					"Physical rubber 'Rubber' has no usable serialized guided path; falling back to Legacy collision.");

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
		public void StaleAuthoringStateShouldUseSerializedPhysicalPathAndRuntimeProfile()
		{
			var rubberObject = new GameObject("Rubber");
			rubberObject.SetActive(false);
			var guideAObject = new GameObject("Guide A");
			var guideBObject = new GameObject("Guide B");
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				guideBObject.transform.localPosition = new Vector3(0.1f, 0f, 0f);
				var guideA = guideAObject.AddComponent<RubberGuideComponent>();
				var guideB = guideBObject.AddComponent<RubberGuideComponent>();
				guideA.Slots = new[] { RubberGuideSlot.Create("Default", 0.01f) };
				guideB.Slots = new[] { RubberGuideSlot.Create("Default", 0.01f) };
				var rubber = rubberObject.AddComponent<RubberComponent>();
				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guideA, guideA.Slots[0].Id),
					new RubberGuideBinding(guideB, guideB.Slots[0].Id),
				});
				Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError),
					Is.True, bakeError);

				var rubberCollider = rubberObject.AddComponent<RubberColliderComponent>();
				rubberCollider.Mode = RubberColliderMode.Physical;
				rubberCollider.GuideDistanceElasticityEnabled = true;
				rubberCollider.GuideElasticityScale = 0.25f;
				rubberCollider.GuideInfluenceLength = 50f;
				rubberCollider.MaximumResponseSegmentLength = 20f;

				guideBObject.transform.localPosition += new Vector3(0.001f, 0f, 0f);
				Assert.That(rubber.HasValidGuidedPath, Is.False);
				Assert.That(rubberCollider.CanUsePhysical, Is.True);

				new TestRubberApi(rubberObject).GenerateColliders(ref references);

				Assert.That(references.Count,
					Is.EqualTo(references.SweptCircleColliders.Length));
				Assert.That(references.SweptCircleColliders.Length, Is.GreaterThan(0));
				var minimumScale = 1f;
				var maximumScale = 0f;
				foreach (var physicalCollider in references.SweptCircleColliders) {
					minimumScale = math.min(minimumScale, physicalCollider.ElasticityScale);
					maximumScale = math.max(maximumScale, physicalCollider.ElasticityScale);
				}
				Assert.That(minimumScale, Is.LessThan(0.5f));
				Assert.That(maximumScale, Is.EqualTo(1f));
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(rubberObject);
				UnityEngine.Object.DestroyImmediate(guideAObject);
				UnityEngine.Object.DestroyImmediate(guideBObject);
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
						Control1 = new float2(-10f / 3f, 0f),
						Control2 = new float2(10f / 3f, 0f),
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

				new RubberPhysicalColliderGenerator(api, rubber, float4x4.identity,
					RubberGuideDistanceElasticitySettings.Disabled)
					.GenerateColliders(2f, ref references, 1f);

				Assert.That(references.SweptCircleColliders.Length, Is.GreaterThan(2));
				Assert.That(references.SweptCircleColliders[0].CordRadius, Is.EqualTo(5f));
				Assert.That(references.SweptCircleColliders[0].V1.z, Is.EqualTo(2f));
				for (var i = 0; i < references.SweptCircleColliders.Length; i++) {
					Assert.That(references.SweptCircleColliders[i].ElasticityScale, Is.EqualTo(1f));
					if (i > 0) {
						Assert.That(math.distance(references.SweptCircleColliders[i - 1].V2,
							references.SweptCircleColliders[i].V1), Is.LessThan(1e-5f));
					}
				}
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void GuideDistanceProfileShouldSplitAndScaleAFreeSpanSymmetrically()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				var rubberCollider = go.AddComponent<RubberColliderComponent>();
				rubber._thickness = 8;
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				rubber.ApplyGuidedBake(new[] { StraightSpan(-100f, 100f) }, null,
					Matrix4x4.identity, default, 1);
				rubberCollider.GuideDistanceElasticityEnabled = true;
				rubberCollider.GuideElasticityScale = 0.5f;
				rubberCollider.GuideInfluenceLength = 50f;
				rubberCollider.MaximumResponseSegmentLength = 20f;

				new RubberPhysicalColliderGenerator(new RubberApi(go, null, null), rubber,
					float4x4.identity, rubberCollider.GuideDistanceElasticitySettings)
					.GenerateColliders(0f, ref references, 0f);

				Assert.That(references.SweptCircleColliders.Length, Is.EqualTo(32));
				for (var i = 0; i < references.SweptCircleColliders.Length; i++) {
					var collider = references.SweptCircleColliders[i];
					Assert.That(math.distance(collider.V1, collider.V2), Is.LessThanOrEqualTo(6.25f + 1e-5f));
					var opposite = references.SweptCircleColliders[
						references.SweptCircleColliders.Length - i - 1];
					Assert.That(collider.ElasticityScale,
						Is.EqualTo(opposite.ElasticityScale).Within(1e-6f));
					if (i > 0) {
						Assert.That(math.abs(collider.ElasticityScale
							- references.SweptCircleColliders[i - 1].ElasticityScale),
							Is.LessThanOrEqualTo(3f / 32f + 1e-6f));
					}
				}
				Assert.That(references.SweptCircleColliders[0].ElasticityScale,
					Is.LessThan(0.52f));
				Assert.That(references.SweptCircleColliders[15].ElasticityScale,
					Is.EqualTo(1f).Within(1e-6f));
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void GuideDistanceProfileShouldApplyGuideScaleToSupportedArcs()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				var rubberCollider = go.AddComponent<RubberColliderComponent>();
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				rubber.ApplyGuidedBake(new[] { SupportedArc() }, null,
					Matrix4x4.identity, default, 1);
				rubberCollider.GuideDistanceElasticityEnabled = true;
				rubberCollider.GuideElasticityScale = 0.25f;

				new RubberPhysicalColliderGenerator(new RubberApi(go, null, null), rubber,
					float4x4.identity, rubberCollider.GuideDistanceElasticitySettings)
					.GenerateColliders(0f, ref references, 0f);

				Assert.That(references.SweptCircleColliders.Length, Is.GreaterThan(0));
				foreach (var collider in references.SweptCircleColliders) {
					Assert.That(collider.ElasticityScale, Is.EqualTo(0.25f));
				}
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void UnitScaleShouldKeepExistingFreeSpanColliderCount()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				var rubberCollider = go.AddComponent<RubberColliderComponent>();
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				rubber.ApplyGuidedBake(new[] { StraightSpan(-100f, 100f) }, null,
					Matrix4x4.identity, default, 1);
				rubberCollider.GuideDistanceElasticityEnabled = true;
				rubberCollider.GuideElasticityScale = 1f;

				new RubberPhysicalColliderGenerator(new RubberApi(go, null, null), rubber,
					float4x4.identity, rubberCollider.GuideDistanceElasticitySettings)
					.GenerateColliders(0f, ref references, 0f);

				Assert.That(references.SweptCircleColliders.Length, Is.EqualTo(1));
				Assert.That(references.SweptCircleColliders[0].ElasticityScale, Is.EqualTo(1f));
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void AdjacentFreeSpansShouldTreatTheirJunctionAsASupport()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				var rubberCollider = go.AddComponent<RubberColliderComponent>();
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				rubber.ApplyGuidedBake(new[] {
					StraightSpan(-100f, 0f),
					StraightSpan(0f, 100f),
				}, null, Matrix4x4.identity, default, 1);
				rubberCollider.GuideDistanceElasticityEnabled = true;
				rubberCollider.GuideElasticityScale = 0.5f;

				new RubberPhysicalColliderGenerator(new RubberApi(go, null, null), rubber,
					float4x4.identity, rubberCollider.GuideDistanceElasticitySettings)
					.GenerateColliders(0f, ref references, 0f);

				Assert.That(references.SweptCircleColliders.Length, Is.EqualTo(32));
				Assert.That(references.SweptCircleColliders[15].ElasticityScale,
					Is.LessThan(0.52f));
				Assert.That(references.SweptCircleColliders[16].ElasticityScale,
					Is.LessThan(0.52f));
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShortFreeSpanShouldUseAPartialProfileWithoutOvershoot()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				var rubberCollider = go.AddComponent<RubberColliderComponent>();
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				rubber.ApplyGuidedBake(new[] { StraightSpan(-20f, 20f) }, null,
					Matrix4x4.identity, default, 1);
				rubberCollider.GuideDistanceElasticityEnabled = true;
				rubberCollider.GuideElasticityScale = 0.25f;
				rubberCollider.GuideInfluenceLength = 50f;

				new RubberPhysicalColliderGenerator(new RubberApi(go, null, null), rubber,
					float4x4.identity, rubberCollider.GuideDistanceElasticitySettings)
					.GenerateColliders(0f, ref references, 0f);

				var minimumScale = 1f;
				var maximumScale = 0f;
				for (var i = 0; i < references.SweptCircleColliders.Length; i++) {
					var scale = references.SweptCircleColliders[i].ElasticityScale;
					Assert.That(scale, Is.InRange(0.25f, 1f));
					minimumScale = math.min(minimumScale, scale);
					maximumScale = math.max(maximumScale, scale);
				}
				Assert.That(minimumScale, Is.GreaterThan(0.25f));
				Assert.That(maximumScale, Is.LessThan(1f));
				Assert.That(maximumScale, Is.GreaterThan(minimumScale));
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void CurvedFreeSpanShouldPreserveGeometryToleranceAndResponseLength()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				var rubberCollider = go.AddComponent<RubberColliderComponent>();
				var span = CurvedSpan();
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				rubber.ApplyGuidedBake(new[] { span }, null, Matrix4x4.identity, default, 1);
				rubberCollider.GuideDistanceElasticityEnabled = true;
				rubberCollider.GuideElasticityScale = 0.5f;
				rubberCollider.GuideInfluenceLength = 50f;

				new RubberPhysicalColliderGenerator(new RubberApi(go, null, null), rubber,
					float4x4.identity, rubberCollider.GuideDistanceElasticitySettings)
					.GenerateColliders(0f, ref references, 0f);

				var geometrySegmentCount = RubberPathMath.FreeSpanSubdivisionCount(span,
					RubberPhysicalColliderGenerator.ChordTolerance(rubber.Thickness * 0.5f));
				Assert.That(references.SweptCircleColliders.Length,
					Is.GreaterThanOrEqualTo(geometrySegmentCount));
				var emittedLength = 0f;
				for (var i = 0; i < references.SweptCircleColliders.Length; i++) {
					var collider = references.SweptCircleColliders[i];
					var length = math.distance(collider.V1, collider.V2);
					Assert.That(length, Is.LessThanOrEqualTo(6.25f + 1e-5f));
					emittedLength += length;
					if (i > 0) {
						Assert.That(math.distance(references.SweptCircleColliders[i - 1].V2,
							collider.V1), Is.LessThan(1e-5f));
					}
				}
				Assert.That(span.Length - emittedLength,
					Is.InRange(0f, geometrySegmentCount * RubberPhysicalColliderGenerator.ChordTolerance(
						rubber.Thickness * 0.5f)));
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void GeneratorShouldResolveColliderInfoOncePerPass()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				go.AddComponent<RubberColliderComponent>();
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				rubber.ApplyGuidedBake(new[] {
					StraightSpan(-100f, 0f),
					StraightSpan(0f, 100f),
				}, null, Matrix4x4.identity, default, 1);
				var api = new CountingColliderApi();

				new RubberPhysicalColliderGenerator(api, rubber, float4x4.identity,
					new RubberGuideDistanceElasticitySettings(true, 0.5f, 50f, 20f))
					.GenerateColliders(0f, ref references, 0f);

				Assert.That(api.GetColliderInfoCalls, Is.EqualTo(1));
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void MeasureGuideDistanceProfileRegistrationCost()
		{
			var go = new GameObject("Profile Measurement Rubber");
			go.SetActive(false);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				go.AddComponent<RubberColliderComponent>();
				rubber._thickness = 8;
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				var representativePath = new[] {
					StraightSpan(-100f, 100f), SupportedArc(),
					StraightSpan(-100f, 100f), SupportedArc(),
					StraightSpan(-100f, 100f), SupportedArc(),
					StraightSpan(-100f, 100f), SupportedArc(),
				};
				rubber.ApplyGuidedBake(representativePath, null, Matrix4x4.identity,
					default, 1);
				var api = new RubberApi(go, null, null);
				MeasureColliderBuild(api, rubber, RubberGuideDistanceElasticitySettings.Disabled,
					5, out _, out _, out _);
				MeasureColliderBuild(api, rubber,
					new RubberGuideDistanceElasticitySettings(true, 0.5f, 50f, 20f), 5,
					out _, out _, out _);
				MeasureColliderBuild(api, rubber, RubberGuideDistanceElasticitySettings.Disabled,
					200, out var baseCount, out var baseGenerationMs, out var baseRegistrationMs);
				MeasureColliderBuild(api, rubber,
					new RubberGuideDistanceElasticitySettings(true, 0.5f, 50f, 20f), 200,
					out var profileCount, out var profileGenerationMs,
					out var profileRegistrationMs);
				MeasureContactQueries(api, rubber, RubberGuideDistanceElasticitySettings.Disabled,
					2000, out var baseCandidates, out var baseContactQueryMs);
				MeasureContactQueries(api, rubber,
					new RubberGuideDistanceElasticitySettings(true, 0.5f, 50f, 20f), 2000,
					out var profileCandidates, out var profileContactQueryMs);
				var colliderBytes = global::Unity.Collections.LowLevel.Unsafe.UnsafeUtility
					.SizeOf<SweptCircleCollider>();

				TestContext.Out.WriteLine(
					$"representative|base={baseCount}|profile={profileCount}|bytes={colliderBytes}|baseGenMs={baseGenerationMs:0.000}|profileGenMs={profileGenerationMs:0.000}|baseRegisterMs={baseRegistrationMs:0.000}|profileRegisterMs={profileRegistrationMs:0.000}|baseCandidates={baseCandidates}|profileCandidates={profileCandidates}|baseContactQueryMs={baseContactQueryMs:0.000000}|profileContactQueryMs={profileContactQueryMs:0.000000}");

				rubber.ApplyGuidedBake(new[] {
					StraightSpan(-500f, 500f),
					StraightSpan(-500f, 500f),
					StraightSpan(-500f, 500f),
					StraightSpan(-500f, 500f),
				}, null, Matrix4x4.identity, default, 2);
				MeasureColliderBuild(api, rubber, RubberGuideDistanceElasticitySettings.Disabled,
					5, out _, out _, out _);
				MeasureColliderBuild(api, rubber,
					new RubberGuideDistanceElasticitySettings(true, 0.5f, 50f, 20f), 5,
					out _, out _, out _);
				MeasureColliderBuild(api, rubber, RubberGuideDistanceElasticitySettings.Disabled,
					200, out baseCount, out baseGenerationMs, out baseRegistrationMs);
				MeasureColliderBuild(api, rubber,
					new RubberGuideDistanceElasticitySettings(true, 0.5f, 50f, 20f), 200,
					out profileCount, out profileGenerationMs, out profileRegistrationMs);
				MeasureContactQueries(api, rubber, RubberGuideDistanceElasticitySettings.Disabled,
					2000, out baseCandidates, out baseContactQueryMs);
				MeasureContactQueries(api, rubber,
					new RubberGuideDistanceElasticitySettings(true, 0.5f, 50f, 20f), 2000,
					out profileCandidates, out profileContactQueryMs);
				TestContext.Out.WriteLine(
					$"worst-loop|base={baseCount}|profile={profileCount}|bytes={colliderBytes}|baseGenMs={baseGenerationMs:0.000}|profileGenMs={profileGenerationMs:0.000}|baseRegisterMs={baseRegistrationMs:0.000}|profileRegisterMs={profileRegistrationMs:0.000}|baseCandidates={baseCandidates}|profileCandidates={profileCandidates}|baseContactQueryMs={baseContactQueryMs:0.000000}|profileContactQueryMs={profileContactQueryMs:0.000000}");

				Assert.That(profileCount, Is.LessThanOrEqualTo(
					4 * RubberPhysicalColliderGenerator.MaximumResponseSegmentsPerElement));
			} finally {
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ExcessiveResponseSubdivisionShouldKeepGeometryAndWarnOnce()
		{
			var go = new GameObject("Rubber");
			go.SetActive(false);
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var rubber = go.AddComponent<RubberComponent>();
				var rubberCollider = go.AddComponent<RubberColliderComponent>();
				rubber.SetGuideBindings(Array.Empty<RubberGuideBinding>());
				rubber.ApplyGuidedBake(new[] { StraightSpan(-500000f, 500000f) }, null,
					Matrix4x4.identity, default, 1);
				rubberCollider.GuideDistanceElasticityEnabled = true;
				rubberCollider.GuideElasticityScale = 0f;
				rubberCollider.GuideInfluenceLength = 1f;
				rubberCollider.MaximumResponseSegmentLength = 0.1f;
				LogAssert.Expect(LogType.Warning,
					"Physical rubber 'Rubber' exceeded the 4096-segment guide-distance response limit; keeping its existing geometry tessellation with a coarser elasticity profile.");

				new RubberPhysicalColliderGenerator(new RubberApi(go, null, null), rubber,
					float4x4.identity, rubberCollider.GuideDistanceElasticitySettings)
					.GenerateColliders(0f, ref references, 0f);

				Assert.That(references.SweptCircleColliders.Length, Is.EqualTo(1));
				Assert.That(references.SweptCircleColliders[0].ElasticityScale, Is.EqualTo(1f));
			} finally {
				references.Dispose();
				transforms.Dispose();
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ElasticityScaleShouldAttenuateConstantMaterialRebound()
		{
			var material = new PhysicsMaterialData { Elasticity = 0.8f };

			Assert.That(CollideAndReturnNormalVelocity(material, 1f), Is.EqualTo(8f).Within(1e-5f));
			Assert.That(CollideAndReturnNormalVelocity(material, 0.5f), Is.EqualTo(4f).Within(1e-5f));
		}

		[Test]
		public void ElasticityScaleShouldAttenuateVelocityLutRebound()
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			var elasticityLuts = new NativeParallelHashMap<int, FixedList512Bytes<float>>(1,
				Allocator.Temp);
			var hitEvents = new NativeQueue<EventData>(Allocator.Temp);
			try {
				const int itemId = 17;
				var lut = new FixedList512Bytes<float>();
				for (var i = 0; i < 127; i++) {
					lut.Add(0.8f);
				}
				elasticityLuts.Add(itemId, lut);
				var material = new PhysicsMaterialData { UseElasticityOverVelocity = true };
				references.Add(new SweptCircleCollider(new float3(-10f, 0f, 0f),
					new float3(10f, 0f, 0f), 2f,
					new ColliderInfo { ItemId = itemId, Material = material }, 0.5f),
					float4x4.identity);
				using var native = new NativeColliders(ref references, Allocator.Temp);
				var state = new PhysicsState {
					Colliders = native,
					ElasticityOverVelocityLUTs = elasticityLuts,
				};
				var ball = ImpactingBall();
				var collision = ImpactEvent(0);
				var writer = hitEvents.AsParallelWriter();

				native.SweptCircle(0).Collide(ref ball, ref writer, in collision, ref state);

				Assert.That(ball.Velocity.y, Is.EqualTo(4f).Within(1e-5f));
			} finally {
				hitEvents.Dispose();
				elasticityLuts.Dispose();
				references.Dispose();
				transforms.Dispose();
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
						Control1 = new float2(-10f / 3f, 0f),
						Control2 = new float2(10f / 3f, 0f),
					},
				}, null, bakeFrame, default, 1);
				var api = new RubberApi(go, null, null);

				new RubberPhysicalColliderGenerator(api, rubber, float4x4.identity,
					RubberGuideDistanceElasticitySettings.Disabled)
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

		private sealed class CountingColliderApi : IApiColliderGenerator
		{
			internal int GetColliderInfoCalls { get; private set; }

			public void CreateColliders(ref ColliderReference colliders,
				float4x4 translateWithinPlayfieldMatrix, float margin)
			{
			}

			public ColliderInfo GetColliderInfo()
			{
				GetColliderInfoCalls++;
				return new ColliderInfo { ItemId = 1 };
			}

			public ColliderInfo GetColliderInfo(ItemType itemType) => GetColliderInfo();

			public bool IsColliderAvailable => true;
		}

		private static float CollideAndReturnNormalVelocity(PhysicsMaterialData material,
			float elasticityScale)
		{
			var collider = new SweptCircleCollider(new float3(-10f, 0f, 0f),
				new float3(10f, 0f, 0f), 2f,
				new ColliderInfo { ItemId = 1, Material = material }, elasticityScale);
			var ball = ImpactingBall();
			var collision = ImpactEvent(0);
			var state = new PhysicsState();
			using var hitEvents = new NativeQueue<EventData>(Allocator.Temp);
			var writer = hitEvents.AsParallelWriter();

			collider.Collide(ref ball, ref writer, in collision, ref state);
			return ball.Velocity.y;
		}

		private static BallState ImpactingBall()
		{
			return new BallState {
				Position = new float3(0f, 3f, 0f),
				Velocity = new float3(0f, -10f, 0f),
				Radius = 1f,
				Mass = 1f,
			};
		}

		private static CollisionEventData ImpactEvent(int colliderId)
		{
			return new CollisionEventData {
				ColliderId = colliderId,
				HitNormal = new float3(0f, 1f, 0f),
			};
		}

		private static RubberPathElement StraightSpan(float start, float end)
		{
			return new RubberPathElement {
				Type = RubberPathElementType.FreeSpan,
				Start = new float2(start, 0f),
				End = new float2(end, 0f),
				Control1 = new float2(math.lerp(start, end, 1f / 3f), 0f),
				Control2 = new float2(math.lerp(start, end, 2f / 3f), 0f),
				Length = math.abs(end - start),
			};
		}

		private static RubberPathElement CurvedSpan()
		{
			var span = new RubberPathElement {
				Type = RubberPathElementType.FreeSpan,
				Start = new float2(-100f, 0f),
				End = new float2(100f, 0f),
				Control1 = new float2(-60f, 100f),
				Control2 = new float2(60f, -100f),
			};
			span.Length = RubberPathMath.FreeSpanLength(span);
			return span;
		}

		private static RubberPathElement SupportedArc()
		{
			return new RubberPathElement {
				Type = RubberPathElementType.SupportedArc,
				Start = new float2(20f, 0f),
				End = new float2(0f, 20f),
				Center = float2.zero,
				Radius = 20f,
				StartAngleRad = 0f,
				SweepAngleRad = math.PI * 0.5f,
				Length = math.PI * 10f,
			};
		}

		private static void MeasureColliderBuild(IApiColliderGenerator api,
			RubberComponent rubber, RubberGuideDistanceElasticitySettings settings,
			int iterations, out int colliderCount, out double generationMilliseconds,
			out double registrationMilliseconds)
		{
			colliderCount = 0;
			var generation = new System.Diagnostics.Stopwatch();
			var registration = new System.Diagnostics.Stopwatch();
			for (var iteration = 0; iteration < iterations; iteration++) {
				var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
				var references = new ColliderReference(ref transforms, Allocator.Temp);
				try {
					generation.Start();
					new RubberPhysicalColliderGenerator(api, rubber, float4x4.identity, settings)
						.GenerateColliders(0f, ref references, 0f);
					generation.Stop();
					colliderCount = references.SweptCircleColliders.Length;
					registration.Start();
					using var native = new NativeColliders(ref references, Allocator.Temp);
					registration.Stop();
				} finally {
					references.Dispose();
					transforms.Dispose();
				}
			}
			generationMilliseconds = generation.Elapsed.TotalMilliseconds / iterations;
			registrationMilliseconds = registration.Elapsed.TotalMilliseconds / iterations;
		}

		private static void MeasureContactQueries(IApiColliderGenerator api,
			RubberComponent rubber, RubberGuideDistanceElasticitySettings settings,
			int iterations, out int candidateCount, out double queryMilliseconds)
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				new RubberPhysicalColliderGenerator(api, rubber, float4x4.identity, settings)
					.GenerateColliders(0f, ref references, 0f);
				var native = new NativeColliders(ref references, Allocator.Temp);
				NativeTrees.AABB bounds = new Aabb(new float3(-2000f), new float3(2000f));
				var octree = new NativeTrees.NativeOctree<int>(bounds,
					math.max(16, native.Length * 2), 10, Allocator.Temp);
				var overlaps = new NativeParallelHashSet<int>(native.Length, Allocator.Temp);
				try {
					PhysicsPopulate.Populate(ref native, ref octree);
					var ball = ImpactingBall();

					RunContactQueries(ref native, in octree, ref overlaps, in ball, 50,
						out _, out _);
					RunContactQueries(ref native, in octree, ref overlaps, in ball, iterations,
						out candidateCount, out queryMilliseconds);
				} finally {
					overlaps.Dispose();
					octree.Dispose();
					native.Dispose();
				}
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		private static void RunContactQueries(ref NativeColliders native,
			in NativeTrees.NativeOctree<int> octree,
			ref NativeParallelHashSet<int> overlaps, in BallState ball, int iterations,
			out int candidateCount, out double queryMilliseconds)
		{
			var hitTimeChecksum = 0f;
			var stopwatch = System.Diagnostics.Stopwatch.StartNew();
			for (var iteration = 0; iteration < iterations; iteration++) {
				PhysicsStaticBroadPhase.FindOverlaps(in octree, in ball, ref overlaps);
				foreach (var colliderId in overlaps) {
					var collision = new CollisionEventData();
					hitTimeChecksum += native.SweptCircle(colliderId)
						.HitTest(ref collision, in ball, 0.01f);
				}
			}
			stopwatch.Stop();
			candidateCount = overlaps.Count();
			queryMilliseconds = stopwatch.Elapsed.TotalMilliseconds / iterations;
			Assert.That(hitTimeChecksum, Is.Not.NaN);
		}

		private static SweptCircleCollider CreateCollider(float elasticityScale = 1f)
		{
			return new SweptCircleCollider(new float3(-10f, 0f, 0f),
				new float3(10f, 0f, 0f), 2f, new ColliderInfo { ItemId = 1 },
				elasticityScale);
		}
	}
}
