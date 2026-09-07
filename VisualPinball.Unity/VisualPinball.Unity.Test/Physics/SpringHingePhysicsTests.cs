// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using NUnit.Framework;
using NativeTrees;
using Unity.Collections;
using Unity.Mathematics;

using VisualPinball.Engine.Common;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity.Test
{
	public class SpringHingePhysicsTests
	{
		[Test]
		public void VelocityPreparationUsesImplicitSpringStepOnce()
		{
			var state = CreateState(angle: 0.2f, angularVelocity: -0.5f);
			const float step = 0.1f;
			var gravity = new float3(0f, 0f, -0.002f);
			var expectedGravityTorque = -0.1f * math.cos(state.Movement.Angle);
			var expected = (state.Static.Inertia * state.Movement.AngularVelocity
				+ step * expectedGravityTorque
				- step * state.Static.Stiffness * (state.Movement.Angle - state.Static.EquilibriumAngle))
				/ (state.Static.Inertia + step * state.Static.Damping
					+ step * step * state.Static.Stiffness);

			SpringHingeVelocityPhysics.UpdateVelocity(ref state, gravity, step);

			Assert.That(state.Movement.AngularVelocity, Is.EqualTo(expected).Within(1e-6f));
			Assert.That(state.Movement.TickStartAngularVelocity, Is.EqualTo(-0.5f));
			Assert.That(state.Movement.TickStartAngleError, Is.EqualTo(0.2f));
			Assert.That(state.Movement.TickStep, Is.EqualTo(step));
		}

		[Test]
		public void ArbitraryAxisGravityTorqueUsesRotatedCentreOfMass()
		{
			var state = CreateState(angle: math.PI / 2f);
			state.Static.Axis = new float3(1f, 0f, 0f);
			state.Static.CentreOfMassArm = new float3(0f, 50f, 0f);
			state.Static.Mass = 1.5f;
			var gravity = new float3(0f, -0.002f, 0f);
			const float expected = 0.15f;

			SpringHingeVelocityPhysics.UpdateVelocity(ref state, gravity, 0.1f);

			Assert.That(state.Movement.GravityTorque, Is.EqualTo(expected).Within(1e-6f));
		}

		[Test]
		public void PhysicsUpdateAppliesCabinetAccelerationAndEnumeratesHinges()
		{
			using var harness = new PhysicsStateHarness();
			var hinge = CreateState();
			hinge.Static.Axis = new float3(0f, 0f, 1f);
			hinge.Static.Stiffness = 0f;
			hinge.Static.Damping = 0f;
			hinge.Static.CentreOfMassArm = new float3(0f, 50f, 0f);
			harness.SpringHingeStates.Add(hinge.AnimationItemId, hinge);
			var state = harness.CreateState();
			var cabinetAcceleration = new float2(2f, 0f);
			var expectedAcceleration = -2f * PhysicsConstants.MToVpu
				* PhysicsConstants.DefaultStepTimeS * PhysicsConstants.DefaultStepTimeS;

			PhysicsUpdate.UpdateSpringHingeVelocities(ref state, float3.zero, cabinetAcceleration, 0.1f);

			ref var updated = ref state.SpringHingeStates.GetValueByRef(hinge.AnimationItemId);
			Assert.That(updated.Movement.EffectiveGravity.x, Is.EqualTo(expectedAcceleration).Within(1e-7f));
			Assert.That(updated.Movement.GravityTorque, Is.EqualTo(-50f * expectedAcceleration).Within(1e-6f));
		}

		[Test]
		public void StopArrivalShortensStepAndBlocksOutwardVelocity()
		{
			var state = CreateState(angle: 0.24f, angularVelocity: 0.5f);
			state.Static.MaximumAngle = 0.25f;
			var stopTime = SpringHingeDisplacementPhysics.GetStopTime(state);

			SpringHingeDisplacementPhysics.UpdateDisplacement(ref state, stopTime);

			Assert.That(stopTime, Is.EqualTo(0.02f).Within(1e-6f));
			Assert.That(state.Movement.Angle, Is.EqualTo(0.25f));
			Assert.That(state.Movement.AngularVelocity, Is.Zero);
			Assert.That(state.Movement.ActiveStop, Is.EqualTo(1));
			Assert.That(SpringHingeDisplacementPhysics.GetStopTime(state), Is.EqualTo(-1f),
				"an outward resting stop must not create a zero-time scheduler loop");
		}

		[Test]
		public void SpringTorqueCanReleaseHingeFromStopImmediately()
		{
			var state = CreateState(angle: 0.25f);
			state.Static.MaximumAngle = 0.25f;
			state.Static.EquilibriumAngle = 0f;
			state.Movement.ActiveStop = 1;

			SpringHingeVelocityPhysics.UpdateVelocity(ref state, float3.zero, 0.1f);

			Assert.That(state.Movement.AngularVelocity, Is.LessThan(0f));
			Assert.That(state.Movement.ActiveStop, Is.Zero);
		}

		[Test]
		public void RestingStopPreservesBlockedTorqueAcrossDisplacement()
		{
			var state = CreateState(angle: 0.25f);
			state.Static.MaximumAngle = 0.25f;
			state.Static.EquilibriumAngle = 0.5f;
			state.Movement.ActiveStop = 1;

			SpringHingeVelocityPhysics.UpdateVelocity(ref state, float3.zero, 0.1f);
			SpringHingeDisplacementPhysics.UpdateDisplacement(ref state, 0.01f);

			Assert.That(state.Movement.ActiveStop, Is.EqualTo(1));
			Assert.That(state.Movement.BlockedTorque, Is.GreaterThan(0f));
			Assert.That(state.Movement.ContinuousAngularAcceleration, Is.Zero);
		}

		[Test]
		public void SlowInwardMotionEscapesStopToleranceBand()
		{
			var state = CreateState(angle: 0.25f - 0.5e-6f, angularVelocity: -0.25e-6f);
			state.Static.MaximumAngle = 0.25f;

			SpringHingeDisplacementPhysics.UpdateDisplacement(ref state, 1f);

			Assert.That(state.Movement.Angle, Is.EqualTo(0.25f - 0.75e-6f).Within(3e-8f));
			Assert.That(state.Movement.Angle, Is.LessThan(0.25f - 0.5e-6f));
			Assert.That(state.Movement.AngularVelocity, Is.LessThan(0f));
			Assert.That(state.Movement.ActiveStop, Is.Zero);
		}

		[Test]
		public void DegenerateVelocityStepPreservesIncomingMotion()
		{
			var state = CreateState(angle: 0.1f, angularVelocity: -0.4f);

			SpringHingeVelocityPhysics.UpdateVelocity(ref state, float3.zero, 0f);

			Assert.That(state.Movement.AngularVelocity, Is.EqualTo(-0.4f));
		}

		[Test]
		public void PhysicsCycleShortensSubstepAtHingeStop()
		{
			using var harness = new PhysicsStateHarness();
			var hinge = CreateState(angle: 0.24f, angularVelocity: 0.5f);
			hinge.Static.MaximumAngle = 0.25f;
			harness.SpringHingeStates.Add(hinge.AnimationItemId, hinge);
			var state = harness.CreateState();
			var overlapping = new NativeParallelHashSet<int>(1, Allocator.Temp);
			NativeTrees.AABB bounds = new Aabb(new float3(-100f), new float3(100f));
			var kinematicOctree = new NativeOctree<int>(bounds, 16, 4, Allocator.Temp);
			var ballOctree = new NativeOctree<int>(bounds, 16, 4, Allocator.Temp);
			var cycle = new PhysicsCycle(Allocator.Temp);
			try {
				cycle.Simulate(ref state, ref overlapping, ref kinematicOctree, ref ballOctree, 0.1f);

				ref var updated = ref state.SpringHingeStates.GetValueByRef(hinge.AnimationItemId);
				Assert.That(updated.Movement.Angle, Is.EqualTo(0.25f));
				Assert.That(updated.Movement.AngularVelocity, Is.Zero);
				Assert.That(updated.Movement.ActiveStop, Is.EqualTo(1));
			} finally {
				cycle.Dispose();
				ballOctree.Dispose();
				kinematicOctree.Dispose();
				overlapping.Dispose();
			}
		}

		[Test]
		public void StaticProgressRuleCannotOverrunSelectedMechanismStop()
		{
			var hitTime = 0.1f;
			PhysicsCycle.ClampToMechanismStop(ref hitTime, 0.02f);
			Assert.That(hitTime, Is.EqualTo(0.02f));

			hitTime = 0.005f;
			PhysicsCycle.ClampToMechanismStop(ref hitTime, 0.02f);
			Assert.That(hitTime, Is.EqualTo(0.005f));
		}

		[Test]
		public void ExhaustedStaticProgressCannotOverrunAcceptedSpringHingeHit()
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			using var harness = new PhysicsStateHarness();
			try {
				var pivot = float3.zero;
				var centre = new float3(10f, 0f, 0f);
				var extents = new float3(5f, 2f, 2f);
				var x = new float3(1f, 0f, 0f);
				var y = new float3(0f, 1f, 0f);
				var z = new float3(0f, 0f, 1f);
				var colliderId = references.Add(new SpringHingeCollider(12, in pivot, in centre,
					in extents, in x, in y, in z, new ColliderInfo { ItemId = 12 }));
				harness.SetStaticColliders(ref references);
				var state = harness.CreateState();
				var ball = new BallState {
					Id = 1,
					CollisionEvent = new CollisionEventData {
						ColliderId = colliderId,
						HitTime = 0.001f
					}
				};
				var acceptedHingeTime = -1f;
				PhysicsCycle.RecordSpringHingeHitTime(ref acceptedHingeTime, in ball, ref state);
				var hitTime = ball.CollisionEvent.HitTime;
				var exhaustedStaticCount = 0f;

				PhysicsCycle.ApplyStaticTime(ref hitTime, ref exhaustedStaticCount, in ball);
				Assert.That(hitTime, Is.EqualTo(PhysicsConstants.StaticTime));
				PhysicsCycle.ClampToSpringHingeHit(ref hitTime, acceptedHingeTime);

				Assert.That(hitTime, Is.EqualTo(0.001f));
			} finally {
				references.Dispose();
				transforms.Dispose();
			}
		}

		private static SpringHingeState CreateState(float angle = 0f, float angularVelocity = 0f)
		{
			return new SpringHingeState(12, new SpringHingeStaticState {
				OwnerId = 12,
				Axis = new float3(1f, 0f, 0f),
				CentreOfMassArm = new float3(0f, 50f, 0f),
				Mass = 1f,
				Inertia = 20f,
				Stiffness = 5f,
				Damping = 0.3f,
				MinimumAngle = -0.25f,
				MaximumAngle = 0.25f
			}, new SpringHingeMovementState {
				Angle = angle,
				AngularVelocity = angularVelocity
			});
		}
	}
}
