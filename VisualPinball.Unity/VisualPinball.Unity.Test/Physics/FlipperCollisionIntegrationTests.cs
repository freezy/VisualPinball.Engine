// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System;
using NativeTrees;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	public class FlipperCollisionIntegrationTests
	{
		[Test]
		public void EosRubberRunsAfterNormalAndFrictionResponse()
		{
			var normalOnly = RunCollision(float4x4.identity, false, false, false, 50f, 0f);
			var baseline = RunCollision(float4x4.identity, false, false, false, 50f, 0.2f);
			var rubberized = RunCollision(float4x4.identity, false, true, false, 50f, 0.2f);
			var incomingSpeed = math.length(new float3(-5f, -1f, 0f));
			var expectedScale = FlipperCollider.EosRubberDesiredCor(incomingSpeed) * incomingSpeed
			                    / math.length(baseline.Velocity);

			Assert.That(math.distance(baseline.Velocity, new float3(-5f, -1f, 0f)), Is.GreaterThan(0.1f));
			Assert.That(math.distance(baseline.Velocity, normalOnly.Velocity), Is.GreaterThan(1e-4f));
			AssertFloat3(rubberized.Velocity, baseline.Velocity * expectedScale);
			AssertFloat3(rubberized.AngularMomentum, baseline.AngularMomentum);
		}

		[TestCase(-10f, false)]
		[TestCase(10f, false)]
		[TestCase(-10f, true)]
		[TestCase(10f, true)]
		public void MirroredZRotationsPreservePlayfieldRubberResponse(float angleDegrees, bool isKinematic)
		{
			var matrix = float4x4.TRS(
				new float3(25f, -10f, 5f),
				quaternion.RotateZ(math.radians(angleDegrees)),
				new float3(1f)
			);
			var baseline = RunCollision(float4x4.identity, false, true, false, 50f, 0.15f);
			var transformed = RunCollision(matrix, isKinematic, true, false, 50f, 0.15f);

			AssertFloat3(transformed.Velocity, matrix.MultiplyVector(baseline.Velocity));
			AssertFloat3(transformed.AngularMomentum, matrix.MultiplyVector(baseline.AngularMomentum));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void RuntimeLocalTransformPreservesPlayfieldRubberResponse(bool isKinematic)
		{
			var matrix = float4x4.TRS(
				new float3(25f, -10f, 5f),
				quaternion.RotateX(math.radians(10f)),
				new float3(1f)
			);
			var baseline = RunCollision(float4x4.identity, false, true, false, 50f, 0.15f);
			var transformed = RunCollision(matrix, isKinematic, true, false, 50f, 0.15f);

			AssertFloat3(transformed.Velocity, matrix.MultiplyVector(baseline.Velocity));
			AssertFloat3(transformed.AngularMomentum, matrix.MultiplyVector(baseline.AngularMomentum));
		}

		[Test]
		public void KinematicTransformUsesColliderToPlayfieldMatrixForVelocityGate()
		{
			var matrix = float4x4.TRS(
				new float3(25f, -10f, 5f),
				quaternion.RotateZ(math.radians(90f)),
				new float3(1f)
			);
			var baseline = RunPlayfieldGateCollision(matrix, false);
			var rubberized = RunPlayfieldGateCollision(matrix, true);
			var incomingSpeed = math.length(new float3(0f, -1f, -5f));
			var expectedScale = FlipperCollider.EosRubberDesiredCor(incomingSpeed) * incomingSpeed
			                    / math.length(baseline.Velocity);

			AssertFloat3(rubberized.Velocity, baseline.Velocity * expectedScale);
		}

		[Test]
		public void LiveCatchBaseDampeningSuppressesEosRubberFallback()
		{
			var baseline = RunCollision(float4x4.identity, false, false, false, 20f, 0.15f);
			var caught = RunCollision(float4x4.identity, false, true, true, 20f, 0.15f);

			AssertFloat3(caught.Velocity, baseline.Velocity * 0.55f);
			AssertFloat3(caught.AngularMomentum, baseline.AngularMomentum * 0.55f);
		}

		[Test]
		public void EligibleLiveCatchWithoutVelocityChangeSuppressesEosRubberFallback()
		{
			var baseline = RunCollision(float4x4.identity, false, false, false, -20f, 0.15f);
			var eligible = RunCollision(float4x4.identity, false, true, true, -20f, 0.15f);

			AssertFloat3(eligible.Velocity, baseline.Velocity);
			AssertFloat3(eligible.AngularMomentum, baseline.AngularMomentum);
		}

		[Test]
		public void PerfectLiveCatchSuppressesEosRubberFallback()
		{
			var caught = RunCollision(float4x4.identity, false, true, true, 50f, 0.15f);

			AssertFloat3(caught.Velocity, float3.zero);
			AssertFloat3(caught.AngularMomentum, float3.zero);
		}

		private static CollisionResult RunCollision(float4x4 matrix, bool isKinematic,
			bool useCatchPhysics, bool hasLiveCatchWindow, float distanceFromBase, float friction)
		{
			using var harness = new FlipperCollisionHarness(matrix, isKinematic, useCatchPhysics,
				hasLiveCatchWindow, friction);
			var localPosition = new float3(0f, -distanceFromBase, 10f);
			var localVelocity = new float3(-5f, -1f, 0f);
			var localNormal = new float3(1f, 0f, 0f);
			var ball = new BallState {
				Id = 7,
				Position = matrix.MultiplyPoint(localPosition),
				EventPosition = matrix.MultiplyPoint(localPosition),
				Velocity = matrix.MultiplyVector(localVelocity),
				Radius = 1f,
				Mass = 1f,
				CollisionEvent = new CollisionEventData {
					ColliderId = 0,
					IsKinematic = isKinematic,
					HitTime = 0f,
					HitNormal = matrix.MultiplyVector(localNormal),
					HitDistance = 0f,
				}
			};
			var state = harness.CreateState();

			PhysicsStaticCollision.Collide(0f, ref ball, ref state);

			return new CollisionResult(ball.Velocity, ball.AngularMomentum);
		}

		private static CollisionResult RunPlayfieldGateCollision(float4x4 matrix, bool useCatchPhysics)
		{
			using var harness = new FlipperCollisionHarness(matrix, true, useCatchPhysics, false, 0f);
			var localPosition = new float3(0f, -50f, 10f);
			var ball = new BallState {
				Id = 7,
				Position = matrix.MultiplyPoint(localPosition),
				EventPosition = matrix.MultiplyPoint(localPosition),
				Velocity = new float3(0f, -1f, -5f),
				Radius = 1f,
				Mass = 1f,
				CollisionEvent = new CollisionEventData {
					ColliderId = 0,
					IsKinematic = true,
					HitTime = 0f,
					HitNormal = new float3(0f, 0f, 1f),
					HitDistance = 0f,
				}
			};
			var state = harness.CreateState();

			PhysicsStaticCollision.Collide(0f, ref ball, ref state);

			return new CollisionResult(ball.Velocity, ball.AngularMomentum);
		}

		private static void AssertFloat3(float3 actual, float3 expected)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f));
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f));
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-4f));
		}

		private readonly struct CollisionResult
		{
			internal readonly float3 Velocity;
			internal readonly float3 AngularMomentum;

			internal CollisionResult(float3 velocity, float3 angularMomentum)
			{
				Velocity = velocity;
				AngularMomentum = angularMomentum;
			}
		}

		private sealed class FlipperCollisionHarness : IDisposable
		{
			private const int ItemId = 42;

			private PhysicsEnv _env;
			private NativeOctree<int> _octree;
			private NativeColliders _colliders;
			private NativeColliders _kinematicColliders;
			private NativeColliders _kinematicCollidersAtIdentity;
			private NativeParallelHashMap<int, float4x4> _kinematicTransforms;
			private NativeParallelHashMap<int, float4x4> _kinematicTargetTransforms;
			private NativeParallelHashMap<int, float4x4> _nonTransformableColliderTransforms;
			private NativeParallelHashMap<int, NativeColliderIds> _kinematicColliderLookups;
			private NativeQueue<EventData> _events;
			private InsideOfs _insideOfs;
			private NativeParallelHashMap<int, BallState> _balls;
			private NativeParallelHashMap<int, BumperState> _bumperStates;
			private NativeParallelHashMap<int, DropTargetState> _dropTargetStates;
			private NativeParallelHashMap<int, FlipperState> _flipperStates;
			private NativeParallelHashMap<int, GateState> _gateStates;
			private NativeParallelHashMap<int, HitTargetState> _hitTargetStates;
			private NativeParallelHashMap<int, KickerState> _kickerStates;
			private NativeParallelHashMap<int, MagnetState> _magnetStates;
			private NativeParallelHashMap<int, PlungerState> _plungerStates;
			private NativeParallelHashMap<int, SpinnerState> _spinnerStates;
			private NativeParallelHashMap<int, SurfaceState> _surfaceStates;
			private NativeParallelHashMap<int, TurntableState> _turntableStates;
			private NativeParallelHashMap<int, TriggerState> _triggerStates;
			private NativeParallelHashSet<int> _disabledCollisionItems;
			private bool _swapBallCollisionHandling;
			private NativeParallelHashMap<int, FixedList512Bytes<float>> _elasticityLuts;
			private NativeParallelHashMap<int, FixedList512Bytes<float>> _frictionLuts;
			private NativeParallelHashMap<int, KinematicVelocityState> _kinematicVelocities;
			private ColliderReference _colliderReference;
			private readonly bool _isKinematic;

			internal FlipperCollisionHarness(float4x4 matrix, bool isKinematic, bool useCatchPhysics,
				bool hasLiveCatchWindow, float friction)
			{
				_isKinematic = isKinematic;
				_env = new PhysicsEnv { TimeMsec = 105 };
				_kinematicTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
				_kinematicTargetTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
				_nonTransformableColliderTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
				_kinematicColliderLookups = new NativeParallelHashMap<int, NativeColliderIds>(1, Allocator.Persistent);
				_events = new NativeQueue<EventData>(Allocator.Persistent);
				_insideOfs = new InsideOfs(Allocator.Persistent);
				_balls = new NativeParallelHashMap<int, BallState>(1, Allocator.Persistent);
				_flipperStates = new NativeParallelHashMap<int, FlipperState>(1, Allocator.Persistent);
				_disabledCollisionItems = new NativeParallelHashSet<int>(1, Allocator.Persistent);
				_elasticityLuts = new NativeParallelHashMap<int, FixedList512Bytes<float>>(1, Allocator.Persistent);
				_frictionLuts = new NativeParallelHashMap<int, FixedList512Bytes<float>>(1, Allocator.Persistent);
				_kinematicVelocities = new NativeParallelHashMap<int, KinematicVelocityState>(1, Allocator.Persistent);

				_colliderReference = new ColliderReference(ref _nonTransformableColliderTransforms,
					Allocator.Persistent, isKinematic);
				var collider = new FlipperCollider(50f, 100f, 20f, 10f, 0f, 0f,
					new ColliderInfo {
						ItemId = ItemId,
						ItemType = ItemType.Flipper,
						Material = new PhysicsMaterialData {
							Elasticity = 0f,
							ElasticityFalloff = 0f,
							Friction = friction,
						},
					});
				_colliderReference.Add(collider, matrix);
				if (isKinematic) {
					_kinematicColliders = new NativeColliders(ref _colliderReference, Allocator.Persistent);
					_kinematicTransforms.Add(ItemId, matrix);
				} else {
					_colliders = new NativeColliders(ref _colliderReference, Allocator.Persistent);
				}

				_flipperStates.Add(ItemId, new FlipperState(
					new FlipperStaticData {
						AngleStart = 0f,
						AngleEnd = 0f,
						Inertia = 1000f,
					},
					new FlipperMovementState { Angle = 0f },
					new FlipperVelocityData {
						IsInContact = true,
						ContactTorque = -1f,
					},
					new FlipperHitData(),
					new FlipperTricksData {
						UseFlipperLiveCatch = useCatchPhysics,
						OriginalAngleEnd = 0f,
						AngleEnd = 0f,
						LiveCatchDistanceMin = 5f,
						LiveCatchDistanceMax = 114f,
						LiveCatchMinimalBallSpeed = 3f,
						LiveCatchPerfectTime = 8f,
						LiveCatchFullTime = 16f,
						LiveCatchMinimalBounceSpeedMultiplier = 0f,
						LiveCatchInaccurateBounceSpeedMultiplier = 32f,
						LiveCatchBaseDampenDistance = 30f,
						LiveCatchBaseDampen = 0.55f,
						LiveCatchEosTimeMsec = 100,
						HasLiveCatchEosTime = hasLiveCatchWindow,
					},
					new SolenoidState { Value = true }
				));
			}

			internal PhysicsState CreateState()
			{
				var writer = _events.AsParallelWriter();
				return new PhysicsState(ref _env, ref _octree, ref _colliders, ref _kinematicColliders,
					ref _kinematicCollidersAtIdentity, ref _kinematicTransforms, ref _kinematicTargetTransforms,
					ref _nonTransformableColliderTransforms, ref _kinematicColliderLookups, ref writer,
					ref _insideOfs, ref _balls, ref _bumperStates, ref _dropTargetStates, ref _flipperStates,
					ref _gateStates, ref _hitTargetStates, ref _kickerStates, ref _magnetStates,
					ref _plungerStates, ref _spinnerStates, ref _surfaceStates, ref _turntableStates,
					ref _triggerStates, ref _disabledCollisionItems, ref _swapBallCollisionHandling,
					ref _elasticityLuts, ref _frictionLuts, ref _kinematicVelocities);
			}

			public void Dispose()
			{
				if (_isKinematic) {
					_kinematicColliders.Dispose();
				} else {
					_colliders.Dispose();
				}
				_colliderReference.Dispose();
				_kinematicTransforms.Dispose();
				_kinematicTargetTransforms.Dispose();
				_nonTransformableColliderTransforms.Dispose();
				_kinematicColliderLookups.Dispose();
				_events.Dispose();
				_insideOfs.Dispose();
				_balls.Dispose();
				_flipperStates.Dispose();
				_disabledCollisionItems.Dispose();
				_elasticityLuts.Dispose();
				_frictionLuts.Dispose();
				_kinematicVelocities.Dispose();
			}
		}
	}
}
