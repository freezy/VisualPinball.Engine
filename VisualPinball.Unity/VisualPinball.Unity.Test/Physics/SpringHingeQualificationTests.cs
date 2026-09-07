// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	public class SpringHingeQualificationTests
	{
		private const int Iterations = 100000;
		private const float Tick = 0.1f;

		[Test]
		public void PassiveHingeDoesNotGainEnergyOverTenSeconds()
		{
			var hinge = CreateHinge(1);
			hinge.Static.Damping = 0f;
			hinge.Static.MinimumAngle = -math.PI;
			hinge.Static.MaximumAngle = math.PI;
			hinge.Movement.Angle = 0.2f;
			var initialEnergy = MechanicalEnergy(in hinge);
			var maximumEnergy = initialEnergy;

			for (var tick = 0; tick < 10000; tick++) {
				SpringHingeVelocityPhysics.UpdateVelocity(ref hinge, float3.zero, Tick);
				SpringHingeDisplacementPhysics.UpdateDisplacement(ref hinge, Tick);
				maximumEnergy = math.max(maximumEnergy, MechanicalEnergy(in hinge));
			}

			var finalEnergy = MechanicalEnergy(in hinge);
			Assert.That(maximumEnergy, Is.LessThanOrEqualTo(initialEnergy * 1.00001f));
			Assert.That(finalEnergy, Is.LessThan(initialEnergy),
				"the implicit step may damp passive motion but must not create energy");
		}

		[TestCase(8f)]
		[TestCase(18f)]
		[TestCase(30f)]
		public void ShotEnvelopeKeepsAcceptedToiPenetrationBelowHalfPercentRadius(float speed)
		{
			const float radius = 25f;
			var pivot = float3.zero;
			var centreArm = new float3(50f, 0f, 0f);
			var halfExtents = new float3(25f, 25f, 10f);
			var axisX = new float3(1f, 0f, 0f);
			var axisY = new float3(0f, 1f, 0f);
			var axisZ = new float3(0f, 0f, 1f);
			var info = new ColliderInfo {
				ItemId = 1,
				Material = new PhysicsMaterialData { Elasticity = 0.1f, Friction = 0.3f }
			};
			var collider = new SpringHingeCollider(1, in pivot, in centreArm, in halfExtents,
				in axisX, in axisY, in axisZ, info);
			var hinge = CreateHinge(1);
			var ball = new BallState {
				Id = 2,
				Position = new float3(50f, 50f + speed * Tick * 0.5f, 0f),
				Velocity = new float3(0f, -speed, 0f),
				Mass = 1f,
				Radius = radius
			};
			var collision = new CollisionEventData();

			var time = collider.HitTest(ref collision, in hinge, in ball, Tick);

			Assert.That(time, Is.EqualTo(Tick * 0.5f).Within(2e-4f));
			Assert.That(collision.HitDistance,
				Is.GreaterThanOrEqualTo(-radius * 0.005f).And.LessThanOrEqualTo(radius * 0.005f));
			ball.Position += ball.Velocity * time;
			SpringHingeDisplacementPhysics.UpdateDisplacement(ref hinge, time);
			var distanceAtImpact = collider.Distance(in hinge, in ball.Position, radius);
			Assert.That(distanceAtImpact.Separation, Is.GreaterThanOrEqualTo(-radius * 0.005f));
		}

		[Test]
		public void TickAndHoldLoopsStayAllocationFreeWithinBoundedRuntime()
		{
			using var emptyHarness = new PhysicsStateHarness();
			var emptyState = emptyHarness.CreateState();
			using var oneHarness = new PhysicsStateHarness();
			oneHarness.SpringHingeStates.Add(100, CreateHinge(100));
			var oneState = oneHarness.CreateState();
			using var loadedHarness = new PhysicsStateHarness();
			for (var i = 0; i < 8; i++) {
				loadedHarness.SpringHingeStates.Add(100 + i, CreateHinge(100 + i));
			}
			var loadedState = loadedHarness.CreateState();
			var hinge = CreateHinge(1);
			var ball = new BallState {
				Id = 2,
				Position = new float3(0f, 50f, 0f),
				Velocity = new float3(0.1f, -0.2f, 0.05f),
				Mass = 1f,
				Radius = 25f
			};
			var magnet = new MagnetState {
				EffectiveCurrent = 1f,
				HoldStiffness = 25f,
				HoldDamping = 10f,
				MaxHoldForce = 1000f
			};

			// Warm JIT and native-container enumerators before allocation measurement.
			PhysicsUpdate.UpdateSpringHingeVelocities(ref emptyState, float3.zero, float2.zero, 0.1f);
			PhysicsUpdate.UpdateSpringHingeVelocities(ref oneState, float3.zero, float2.zero, 0.1f);
			PhysicsUpdate.UpdateSpringHingeVelocities(ref loadedState, float3.zero, float2.zero, 0.1f);
			OwnedMagnetPhysics.SolveHold(ref ball, ref hinge, in magnet,
				new float3(0f, 50f, 0f), 0.1f, out _);

			var empty = MeasureTickLoop(ref emptyState);
			var one = MeasureTickLoop(ref oneState);
			var several = MeasureTickLoop(ref loadedState);
			var hold = MeasureHoldLoop(ref ball, ref hinge, in magnet);

			UnityEngine.Debug.Log(
				$"spring-hinge benchmark ({Iterations} iterations): empty={empty.ElapsedMilliseconds:0.###}ms/{empty.AllocatedBytes}B, " +
				$"one={one.ElapsedMilliseconds:0.###}ms/{one.AllocatedBytes}B, " +
				$"eight={several.ElapsedMilliseconds:0.###}ms/{several.AllocatedBytes}B, hold={hold.ElapsedMilliseconds:0.###}ms/{hold.AllocatedBytes}B");
			Assert.That(empty.AllocatedBytes, Is.Zero, "empty hinge scan must not allocate per tick");
			Assert.That(one.AllocatedBytes, Is.Zero, "one-hinge scan must not allocate per tick");
			Assert.That(several.AllocatedBytes, Is.Zero, "several-hinge scan must not allocate per tick");
			Assert.That(hold.AllocatedBytes, Is.Zero, "owned hold solve must not allocate per tick");
			Assert.That(empty.ElapsedMilliseconds, Is.LessThan(5000f));
			Assert.That(one.ElapsedMilliseconds, Is.LessThan(5000f));
			Assert.That(several.ElapsedMilliseconds, Is.LessThan(5000f));
			Assert.That(hold.ElapsedMilliseconds, Is.LessThan(5000f));
		}

		private static Measurement MeasureTickLoop(ref PhysicsState state)
		{
			var before = GC.GetAllocatedBytesForCurrentThread();
			var started = Stopwatch.GetTimestamp();
			for (var i = 0; i < Iterations; i++) {
				PhysicsUpdate.UpdateSpringHingeVelocities(ref state,
					float3.zero, float2.zero, 0.1f);
			}
			var elapsed = Stopwatch.GetTimestamp() - started;
			return new Measurement(elapsed * 1000.0 / Stopwatch.Frequency,
				GC.GetAllocatedBytesForCurrentThread() - before);
		}

		private static Measurement MeasureHoldLoop(ref BallState ball,
			ref SpringHingeState hinge, in MagnetState magnet)
		{
			// Re-seeding is deliberately timed, so this is a conservative solve cost rather than a solver-only number.
			var before = GC.GetAllocatedBytesForCurrentThread();
			var started = Stopwatch.GetTimestamp();
			for (var i = 0; i < Iterations; i++) {
				ball.Position = new float3(0f, 50f, 0f);
				ball.Velocity = new float3(0.1f, -0.2f, 0.05f);
				hinge.Movement = new SpringHingeMovementState {
					TickStartAngularVelocity = 0.01f,
					TickStep = 0.1f
				};
				OwnedMagnetPhysics.SolveHold(ref ball, ref hinge, in magnet,
					new float3(0f, 50f, 0f), 0.1f, out _);
			}
			var elapsed = Stopwatch.GetTimestamp() - started;
			return new Measurement(elapsed * 1000.0 / Stopwatch.Frequency,
				GC.GetAllocatedBytesForCurrentThread() - before);
		}

		private static SpringHingeState CreateHinge(int id)
			=> new(id, new SpringHingeStaticState {
				OwnerId = id,
				Pivot = float3.zero,
				Axis = new float3(0f, 0f, 1f),
				CentreOfMassArm = new float3(0f, 50f, 0f),
				Mass = 1f,
				Inertia = 2500f,
				Stiffness = 100f,
				Damping = 5f,
				MinimumAngle = -math.PI,
				MaximumAngle = math.PI
			}, default);

		private static float MechanicalEnergy(in SpringHingeState hinge)
		{
			var angleError = hinge.Movement.Angle - hinge.Static.EquilibriumAngle;
			return 0.5f * hinge.Static.Inertia * hinge.Movement.AngularVelocity * hinge.Movement.AngularVelocity
			       + 0.5f * hinge.Static.Stiffness * angleError * angleError;
		}

		private readonly struct Measurement
		{
			internal readonly double ElapsedMilliseconds;
			internal readonly long AllocatedBytes;

			internal Measurement(double elapsedMilliseconds, long allocatedBytes)
			{
				ElapsedMilliseconds = elapsedMilliseconds;
				AllocatedBytes = allocatedBytes;
			}
		}
	}
}
