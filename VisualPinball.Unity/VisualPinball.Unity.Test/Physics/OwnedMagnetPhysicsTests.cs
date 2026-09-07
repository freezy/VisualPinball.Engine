// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity.Test
{
	public class OwnedMagnetPhysicsTests
	{
		[Test]
		public void UnconstrainedHoldExchangesProjectedMomentumReciprocally()
		{
			var hinge = CreateHinge(inertia: 5f);
			SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, float3.zero, 0.01f);
			var magnet = CreateMagnet(stiffness: 40f, damping: 8f, maxForce: 10000f);
			var target = new float3(2f, 0f, 0f);
			var ball = CreateBall(1, target + new float3(0.2f, 0.1f, 0f),
				new float3(0.3f, 1.2f, 0f));
			var before = ProjectedAngularMomentum(in ball, in hinge);
			var oldVelocity = ball.Velocity;

			var held = OwnedMagnetPhysics.SolveHold(ref ball, ref hinge, in magnet,
				in target, 0.01f, out var saturated);

			Assert.That(held, Is.True);
			Assert.That(saturated, Is.False);
			Assert.That(ProjectedAngularMomentum(in ball, in hinge),
				Is.EqualTo(before).Within(2e-5f));
			AssertFloat3(ball.ExternalAcceleration, (ball.Velocity - oldVelocity) / 0.01f);
		}

		[Test]
		public void TightHoldApproachesLoadedHingeInertiaWithoutMassDoubleCounting()
		{
			const float step = 0.001f;
			const float toyInertia = 4f;
			const float hingeStiffness = 2f;
			const float angleError = 0.2f;
			const float radius = 2f;
			var hinge = CreateHinge(inertia: toyInertia, stiffness: hingeStiffness,
				angle: angleError);
			SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, float3.zero, step);
			var magnet = CreateMagnet(stiffness: 1e9f, damping: 0f, maxForce: 1e9f);
			var target = new float3(radius, 0f, 0f);
			var ball = CreateBall(1, target, float3.zero);

			OwnedMagnetPhysics.SolveHold(ref ball, ref hinge, in magnet, in target,
				step, out _);

			var loadedInertia = toyInertia + ball.Mass * radius * radius;
			var expected = -step * hingeStiffness * angleError
				/ (loadedInertia + step * step * hingeStiffness);
			Assert.That(hinge.Movement.AngularVelocity, Is.EqualTo(expected).Within(2e-7f));
		}

		[Test]
		public void HoldCapIsAProjectedVectorImpulse()
		{
			const float step = 0.1f;
			var hinge = CreateHinge(inertia: 2f);
			SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, float3.zero, step);
			var magnet = CreateMagnet(stiffness: 100f, damping: 20f, maxForce: 3f);
			var target = new float3(2f, 0f, 0f);
			var ball = CreateBall(1, target + new float3(1f, 1f, 1f),
				new float3(3f, -2f, 1f));
			var oldVelocity = ball.Velocity;

			OwnedMagnetPhysics.SolveHold(ref ball, ref hinge, in magnet, in target,
				step, out var saturated);

			var impulse = ball.Mass * (ball.Velocity - oldVelocity);
			Assert.That(saturated, Is.True);
			Assert.That(math.length(impulse), Is.EqualTo(magnet.MaxHoldForce * step).Within(2e-6f));
		}

		[Test]
		public void BallGravityTransfersThroughHoldWithoutDoubleCountingMass()
		{
			const float step = 0.01f;
			var gravity = new float3(0f, -1f, 0f);
			var hinge = CreateHinge(inertia: 4f);
			hinge.Static.CentreOfMassArm = new float3(2f, 0f, 0f);
			SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, in gravity, step);
			var magnet = CreateMagnet(stiffness: 1e7f, damping: 1000f, maxForce: 1e9f);
			var target = new float3(2f, 0f, 0f);
			var ball = CreateBall(1, target, gravity * step);

			OwnedMagnetPhysics.SolveHold(ref ball, ref hinge, in magnet, in target,
				step, out _);

			var toyTorque = math.dot(hinge.Static.Axis,
				math.cross(hinge.Static.CentreOfMassArm, hinge.Static.Mass * gravity));
			var ballTorque = math.dot(hinge.Static.Axis,
				math.cross(ball.Position - hinge.Static.Pivot, ball.Mass * gravity));
			Assert.That(ProjectedAngularMomentum(in ball, in hinge),
				Is.EqualTo(step * (toyTorque + ballTorque)).Within(2e-4f));
		}

		[TestCase(1f)]
		[TestCase(2f)]
		public void LoadedOscillationPeriodConvergesToPointMassIdentity(float radius)
		{
			var coarse = MeasureLoadedPeriod(radius, 0.0002f);
			var fine = MeasureLoadedPeriod(radius, 0.0001f);
			var expected = math.TAU * math.sqrt((4f + radius * radius) / 200f);

			Assert.That(coarse, Is.EqualTo(expected).Within(expected * 0.02f));
			Assert.That(fine, Is.EqualTo(expected).Within(expected * 0.012f));
			Assert.That(math.abs(fine - expected), Is.LessThanOrEqualTo(math.abs(coarse - expected) + 0.002f));
		}

		[Test]
		public void ActiveStopBlocksOutwardHoldButAllowsReleaseDirection()
		{
			const float step = 0.01f;
			var hinge = CreateHinge(inertia: 2f);
			hinge.Movement.ActiveStop = 1;
			SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, float3.zero, step);
			var magnet = CreateMagnet(stiffness: 100f, damping: 0f, maxForce: 10000f);
			var target = new float3(2f, 0f, 0f);
			var ball = CreateBall(1, target + new float3(0f, 1f, 0f), float3.zero);

			OwnedMagnetPhysics.SolveHold(ref ball, ref hinge, in magnet, in target,
				step, out _);

			Assert.That(hinge.Movement.AngularVelocity, Is.Zero);
			Assert.That(hinge.Movement.ActiveStop, Is.EqualTo(1));

			hinge = CreateHinge(inertia: 2f);
			hinge.Movement.ActiveStop = 1;
			SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, float3.zero, step);
			ball = CreateBall(1, target - new float3(0f, 1f, 0f), float3.zero);

			OwnedMagnetPhysics.SolveHold(ref ball, ref hinge, in magnet, in target,
				step, out _);

			Assert.That(hinge.Movement.AngularVelocity, Is.LessThan(0f));
			Assert.That(hinge.Movement.ActiveStop, Is.Zero);
		}

		[Test]
		public void CaptureArbitrationChoosesNearestThenStableBallId()
		{
			using var harness = new PhysicsStateHarness();
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var hinge = CreateHinge(inertia: 10f);
				harness.SpringHingeStates.Add(hinge.AnimationItemId, hinge);
				references.Add(CreateCollider());
				harness.SetStaticColliders(ref references);
				var magnet = CreateMagnet(stiffness: 100f, damping: 10f, maxForce: 10000f);
				harness.MagnetStates.Add(20, magnet);
				harness.Balls.Add(2, CreateBall(2, new float3(10.1f, 3f, 0f), float3.zero));
				harness.Balls.Add(1, CreateBall(1, new float3(9.9f, 3f, 0f), float3.zero));
				var state = harness.CreateState();
				ref var stateHinge = ref state.SpringHingeStates.GetValueByRef(hinge.AnimationItemId);
				SpringHingeVelocityPhysics.PrepareVelocity(ref stateHinge, float3.zero, 0.01f);

				OwnedMagnetPhysics.Update(ref state, 0.01f);

				Assert.That(state.MagnetStates[20].AttachedBallId, Is.EqualTo(1));
				Assert.That(state.Balls[1].AttachedMagnetId, Is.EqualTo(20));
				Assert.That(state.Balls[2].AttachedMagnetId, Is.Zero);
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void CaptureRejectsHeldTargetAwayFromProxySurface()
		{
			using var harness = new PhysicsStateHarness();
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var hinge = CreateHinge(inertia: 10f);
				harness.SpringHingeStates.Add(hinge.AnimationItemId, hinge);
				references.Add(CreateCollider());
				harness.SetStaticColliders(ref references);
				var magnet = CreateMagnet(stiffness: 100f, damping: 10f, maxForce: 10000f);
				magnet.LocalHeldCentreArm = new float3(10f, 10f, 0f);
				harness.MagnetStates.Add(20, magnet);
				harness.Balls.Add(1, CreateBall(1, new float3(10f, 10f, 0f), float3.zero));
				var state = harness.CreateState();
				ref var stateHinge = ref state.SpringHingeStates.GetValueByRef(hinge.AnimationItemId);
				SpringHingeVelocityPhysics.PrepareVelocity(ref stateHinge, float3.zero, 0.01f);

				OwnedMagnetPhysics.Update(ref state, 0.01f);

				Assert.That(state.MagnetStates[20].AttachedBallId, Is.Zero);
				Assert.That(state.Balls[1].AttachedMagnetId, Is.Zero);
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void CaptureRequiresAvailableWorkEvenAtZeroRelativeSpeed()
		{
			var hinge = CreateHinge(inertia: 10f);
			SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, float3.zero, 0.01f);
			var magnet = CreateMagnet(stiffness: 100f, damping: 10f, maxForce: 10000f);
			var pole = new float3(10f, 0f, 0f);
			var target = new float3(10f, 3f, 0f);
			var ball = CreateBall(1, target, float3.zero);

			magnet.EffectiveCurrent = 0f;
			magnet.EffectiveStrength = 0f;
			Assert.That(OwnedMagnetPhysics.CanCapture(in ball, in magnet, in hinge,
				in pole, in target), Is.False);

			magnet.EffectiveCurrent = 1f;
			magnet.EffectiveStrength = magnet.Strength;
			Assert.That(OwnedMagnetPhysics.CanCapture(in ball, in magnet, in hinge,
				in pole, in target), Is.True);
		}

		[Test]
		public void SchedulerAdvancesOwnedCoilAndCommitsHingeExactlyOnce()
		{
			using var harness = new PhysicsStateHarness();
			var hinge = CreateHinge(inertia: 4f, stiffness: 2f, angle: 0.2f);
			harness.SpringHingeStates.Add(12, hinge);
			var magnet = CreateMagnet(stiffness: 100f, damping: 10f, maxForce: 1000f);
			magnet.EffectiveCurrent = 0f;
			magnet.EffectiveStrength = 0f;
			magnet.RiseTime = 1f;
			harness.MagnetStates.Add(20, magnet);
			var state = harness.CreateState();
			PhysicsUpdate.UpdateSpringHingeVelocities(ref state, float3.zero, float2.zero, 0.1f);

			PhysicsUpdate.UpdateMagnetsAndCommitSpringHinges(ref state, 0.1f);

			var expectedCurrent = 0.1f / 1.1f;
			Assert.That(state.MagnetStates[20].EffectiveCurrent,
				Is.EqualTo(expectedCurrent).Within(1e-6f));
			Assert.That(state.SpringHingeStates[12].Movement.VelocityCommitted, Is.True);
			var expectedOmega = -0.1f * 2f * 0.2f / (4f + 0.01f * 2f);
			Assert.That(state.SpringHingeStates[12].Movement.AngularVelocity,
				Is.EqualTo(expectedOmega).Within(1e-6f));
		}

		[Test]
		public void SchedulerPreservesLegacyMagnetMembership()
		{
			using var harness = new PhysicsStateHarness();
			var magnet = CreateMagnet(stiffness: 0f, damping: 0f, maxForce: 0f);
			magnet.CoupleToHinge = false;
			magnet.GrabRadius = 0f;
			magnet.LocalPoleArm = float3.zero;
			magnet.Position = float2.zero;
			magnet.Height = 0f;
			harness.MagnetStates.Add(20, magnet);
			harness.Balls.Add(1, CreateBall(1, new float3(2f, 0f, 0f), float3.zero));
			var state = harness.CreateState();

			PhysicsUpdate.UpdateMagnetsAndCommitSpringHinges(ref state, 0.01f);

			Assert.That(harness.InsideOfs.IsInsideOf(20, 1), Is.True);
		}

		[Test]
		public void ReleasePreservesBallAndHingeMotionAndFiresOnce()
		{
			using var harness = CreateAttachedHarness(out var references, out var transforms);
			try {
				ref var magnet = ref harness.MagnetStates.GetValueByRef(20);
				magnet.EffectiveCurrent = 0f;
				magnet.EffectiveStrength = 0f;
				ref var ball = ref harness.Balls.GetValueByRef(1);
				ball.Velocity = new float3(1f, 2f, 3f);
				ball.AngularMomentum = new float3(4f, 5f, 6f);
				var oldVelocity = ball.Velocity;
				var oldSpin = ball.AngularMomentum;
				ref var hinge = ref harness.SpringHingeStates.GetValueByRef(12);
				hinge.Movement.AngularVelocity = 0.75f;
				SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, float3.zero, 0.01f);
				var state = harness.CreateState();

				OwnedMagnetPhysics.Update(ref state, 0.01f);

				AssertFloat3(state.Balls[1].Velocity, oldVelocity);
				AssertFloat3(state.Balls[1].AngularMomentum, oldSpin);
				Assert.That(state.SpringHingeStates[12].Movement.AngularVelocity,
					Is.EqualTo(0.75f).Within(1e-6f));
				Assert.That(state.MagnetStates[20].AttachedBallId, Is.Zero);
				Assert.That(state.Balls[1].AttachedMagnetId, Is.Zero);
				Assert.That(CountEvents(harness, EventId.MagnetEventsBallReleased), Is.EqualTo(1));
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void HardSecondBallHitReleasesHeldBallBeforeCapturingReplacement()
		{
			using var harness = CreateAttachedHarness(out var references, out var transforms);
			try {
				ref var heldBall = ref harness.Balls.GetValueByRef(1);
				heldBall.Velocity = new float3(0f, 100f, 0f);
				harness.Balls.Add(2, CreateBall(2, new float3(10f, 3f, 0f), float3.zero));
				ref var hinge = ref harness.SpringHingeStates.GetValueByRef(12);
				SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, float3.zero, 0.01f);
				var state = harness.CreateState();

				OwnedMagnetPhysics.Update(ref state, 0.01f);

				Assert.That(state.Balls[1].AttachedMagnetId, Is.Zero);
				Assert.That(state.Balls[2].AttachedMagnetId, Is.EqualTo(20));
				Assert.That(state.MagnetStates[20].AttachedBallId, Is.EqualTo(2));
				var released = 0;
				var grabbed = 0;
				while (harness.EventQueue.TryDequeue(out var eventData)) {
					released += eventData.EventId == EventId.MagnetEventsBallReleased ? 1 : 0;
					grabbed += eventData.EventId == EventId.MagnetEventsBallGrabbed ? 1 : 0;
				}
				Assert.That(released, Is.EqualTo(1));
				Assert.That(grabbed, Is.EqualTo(1));
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		[Test]
		public void SustainedSeparatingCapSaturationReleasesOnce()
		{
			using var harness = new PhysicsStateHarness();
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var hinge = CreateHinge(inertia: 10f);
				harness.SpringHingeStates.Add(hinge.AnimationItemId, hinge);
				references.Add(CreateCollider());
				harness.SetStaticColliders(ref references);
				var magnet = CreateMagnet(stiffness: 1000f, damping: 100f, maxForce: 0.01f);
				magnet.AttachedBallId = 1;
				var ball = CreateBall(1, new float3(10f, 3.1f, 0f), new float3(0f, 10f, 0f));
				ball.AttachedMagnetId = 20;
				harness.Balls.Add(1, ball);
				var bitIndex = harness.InsideOfs.GetOrCreateBitIndex(1);
				magnet.GrabbedBalls.SetBits(bitIndex, true);
				harness.MagnetStates.Add(20, magnet);
				var state = harness.CreateState();

				for (var i = 0; i < 3; i++) {
					ref var stateHinge = ref state.SpringHingeStates.GetValueByRef(hinge.AnimationItemId);
					SpringHingeVelocityPhysics.PrepareVelocity(ref stateHinge, float3.zero, 0.01f);
					OwnedMagnetPhysics.Update(ref state, 0.01f);
					ref var stateBall = ref state.Balls.GetValueByRef(1);
					stateBall.Position += stateBall.Velocity * 0.01f;
				}

				Assert.That(state.MagnetStates[20].AttachedBallId, Is.Zero);
				Assert.That(state.Balls[1].AttachedMagnetId, Is.Zero);
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		private static MagnetState CreateMagnet(float stiffness, float damping, float maxForce)
		{
			return new MagnetState {
				Radius = 30f,
				Strength = 1000f,
				EffectiveCurrent = 1f,
				EffectiveStrength = 1000f,
				PoleRadius = 5f,
				GrabRadius = 5f,
				MagnetType = MagnetType.Spatial,
				Profile = MagnetForceProfile.Physical,
				CoupleToHinge = true,
				HingeOwnerId = 12,
				LocalPoleArm = new float3(10f, 0f, 0f),
				LocalHeldCentreArm = new float3(10f, 3f, 0f),
				HoldStiffness = stiffness,
				HoldDamping = damping,
				MaxHoldForce = maxForce,
				IsEnabled = true,
				CommandedPower = 1f
			};
		}

		private static float MeasureLoadedPeriod(float radius, float step)
		{
			var hinge = CreateHinge(inertia: 4f, stiffness: 200f, angle: 0.05f);
			var magnet = CreateMagnet(stiffness: 5e5f, damping: 1400f, maxForce: 1e9f);
			var referenceArm = new float3(radius, 0f, 0f);
			var initialTarget = SpringHingeVelocityPhysics.RotateAroundAxis(in referenceArm,
				hinge.Static.Axis, hinge.Movement.Angle);
			var ball = CreateBall(1, initialTarget, float3.zero);
			var previousAngle = hinge.Movement.Angle;
			var firstDownwardCrossing = -1f;
			var elapsed = 0f;
			for (var i = 0; i < 25000; i++) {
				SpringHingeVelocityPhysics.PrepareVelocity(ref hinge, float3.zero, step);
				var target = SpringHingeVelocityPhysics.RotateAroundAxis(in referenceArm,
					hinge.Static.Axis, hinge.Movement.Angle);
				OwnedMagnetPhysics.SolveHold(ref ball, ref hinge, in magnet, in target,
					step, out var saturated);
				Assert.That(saturated, Is.False);
				ball.Position += ball.Velocity * step;
				SpringHingeDisplacementPhysics.UpdateDisplacement(ref hinge, step);
				elapsed += step;
				if (previousAngle > 0f && hinge.Movement.Angle <= 0f) {
					var crossing = elapsed - step * (-hinge.Movement.Angle)
						/ (previousAngle - hinge.Movement.Angle);
					if (firstDownwardCrossing >= 0f) {
						return crossing - firstDownwardCrossing;
					}
					firstDownwardCrossing = crossing;
				}
				previousAngle = hinge.Movement.Angle;
			}
			Assert.Fail("Loaded hinge did not complete a measured period.");
			return float.NaN;
		}

		private static SpringHingeState CreateHinge(float inertia, float stiffness = 0f,
			float angle = 0f)
		{
			return new SpringHingeState(12, new SpringHingeStaticState {
				OwnerId = 12,
				Pivot = float3.zero,
				Axis = new float3(0f, 0f, 1f),
				Mass = 1f,
				Inertia = inertia,
				EquilibriumAngle = 0f,
				Stiffness = stiffness,
				MinimumAngle = -math.PI,
				MaximumAngle = math.PI
			}, new SpringHingeMovementState { Angle = angle });
		}

		private static SpringHingeCollider CreateCollider()
		{
			var pivot = float3.zero;
			var centre = new float3(10f, 0f, 0f);
			var extents = new float3(5f, 2f, 2f);
			var x = new float3(1f, 0f, 0f);
			var y = new float3(0f, 1f, 0f);
			var z = new float3(0f, 0f, 1f);
			return new SpringHingeCollider(12, in pivot, in centre, in extents,
				in x, in y, in z, new ColliderInfo { ItemId = 12 });
		}

		private static PhysicsStateHarness CreateAttachedHarness(out ColliderReference references,
			out NativeParallelHashMap<int, float4x4> transforms)
		{
			var harness = new PhysicsStateHarness();
			transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			references = new ColliderReference(ref transforms, Allocator.Temp);
			var hinge = CreateHinge(inertia: 10f);
			harness.SpringHingeStates.Add(hinge.AnimationItemId, hinge);
			references.Add(CreateCollider());
			harness.SetStaticColliders(ref references);
			var magnet = CreateMagnet(stiffness: 1000f, damping: 100f, maxForce: 10000f);
			magnet.AttachedBallId = 1;
			var ball = CreateBall(1, new float3(10f, 3f, 0f), float3.zero);
			ball.AttachedMagnetId = 20;
			harness.Balls.Add(1, ball);
			var bitIndex = harness.InsideOfs.GetOrCreateBitIndex(1);
			magnet.GrabbedBalls.SetBits(bitIndex, true);
			harness.MagnetStates.Add(20, magnet);
			return harness;
		}

		private static int CountEvents(PhysicsStateHarness harness, EventId eventId)
		{
			var count = 0;
			while (harness.EventQueue.TryDequeue(out var eventData)) {
				if (eventData.EventId == eventId) {
					count++;
				}
			}
			return count;
		}

		private static BallState CreateBall(int id, in float3 position, in float3 velocity)
			=> new() { Id = id, Position = position, Velocity = velocity, Mass = 1f, Radius = 1f };

		private static float ProjectedAngularMomentum(in BallState ball, in SpringHingeState hinge)
			=> math.dot(hinge.Static.Axis, math.cross(ball.Position - hinge.Static.Pivot,
				ball.Mass * ball.Velocity) + ball.AngularMomentum)
			   + hinge.Static.Inertia * hinge.Movement.AngularVelocity;

		private static void AssertFloat3(in float3 actual, in float3 expected)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(2e-5f));
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(2e-5f));
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(2e-5f));
		}
	}
}
