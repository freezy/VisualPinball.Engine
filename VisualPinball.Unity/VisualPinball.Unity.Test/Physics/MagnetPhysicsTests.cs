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

namespace VisualPinball.Unity.Test
{
	public class MagnetPhysicsTests
	{
		[Test]
		public void GrabbedBallSurvivesMovingHeightWindow()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(0f, 0f, 10f)
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				Strength = 20f,
				GrabRadius = 20f,
				PlanarDamping = 0.985f,
				HeightRange = 25f,
				IsEnabled = true
			};

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);
			Assert.That(magnet.GrabbedBalls.Value, Is.Not.EqualTo(0UL), "ball should be grabbed");

			// the (kinematic) magnet moves up; the held ball must not be dropped
			// when the height window leaves it behind
			magnet.Height = 100f;
			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);
			Assert.That(magnet.GrabbedBalls.Value, Is.Not.EqualTo(0UL), "ball should stay held");
		}

		[Test]
		public void VpxCompatibleForceScalesToOneMillisecondTicks()
		{
			var oneTickBall = CreateBall();
			var tenTickBall = CreateBall();
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = 10f,
				PlanarDamping = 1f
			};

			MagnetPhysics.ApplyVpxCompatibleForce(ref oneTickBall, in magnet, 1f);
			for (var i = 0; i < 10; i++) {
				MagnetPhysics.ApplyVpxCompatibleForce(ref tenTickBall, in magnet, 0.1f);
			}

			Assert.That(tenTickBall.Velocity.x, Is.EqualTo(oneTickBall.Velocity.x).Within(1e-5f));
			Assert.That(tenTickBall.Velocity.y, Is.EqualTo(oneTickBall.Velocity.y).Within(1e-5f));
		}

		[Test]
		public void PlanarDampingUsesFrameFractionExponent()
		{
			var ball = CreateBall();
			ball.Velocity = new float3(3f, -4f, 5f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = 0f,
				PlanarDamping = 0.985f
			};

			for (var i = 0; i < 10; i++) {
				MagnetPhysics.ApplyVpxCompatibleForce(ref ball, in magnet, 0.1f);
			}

			Assert.That(ball.Velocity.x, Is.EqualTo(3f * 0.985f).Within(1e-5f));
			Assert.That(ball.Velocity.y, Is.EqualTo(-4f * 0.985f).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.EqualTo(5f).Within(1e-5f));
		}

		[Test]
		public void VpxCompatibleForceMatchesCoreVbsAttractBall()
		{
			// One cvpmMagnet.AttractBall update (core.vbs), ball at (50, 0), magnet at
			// origin, Size = 100, Strength = 12, resting ball:
			//   ratio = 50 / (1.5 * 100) = 1/3
			//   force = 12 * exp(-0.6) / ((1/9) * 56) * 1.5 = 1.587634
			//   VelX  = (0 - 50 * force / 50) * 0.985 = -1.563819
			// Ten 1ms ticks must integrate to the same velocity within ~1% (the damping
			// is applied fractionally per tick, which compounds slightly differently).
			var ball = CreateBall();
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = 12f,
				PlanarDamping = 0.985f
			};

			for (var i = 0; i < 10; i++) {
				MagnetPhysics.ApplyVpxCompatibleForce(ref ball, in magnet, 0.1f);
			}

			Assert.That(ball.Velocity.x, Is.EqualTo(-1.563819f).Within(0.02f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
		}

		[Test]
		public void VpxCompatibleForceRepelsWithNegativeStrength()
		{
			var ball = CreateBall();
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = -10f,
				PlanarDamping = 1f
			};

			MagnetPhysics.ApplyVpxCompatibleForce(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.GreaterThan(0f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
		}

		[Test]
		public void PhysicalForceSaturatesInsideCoreRadius()
		{
			var nearBall = CreateBall();
			var coreBall = CreateBall();
			nearBall.Position = new float3(1f, 0f, 10f);
			coreBall.Position = new float3(20f, 0f, 10f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = 400f
			};

			MagnetPhysics.ApplyPhysicalForce(ref nearBall, in magnet, 1f);
			MagnetPhysics.ApplyPhysicalForce(ref coreBall, in magnet, 1f);

			Assert.That(nearBall.Velocity.x, Is.EqualTo(coreBall.Velocity.x).Within(1e-5f));
			Assert.That(nearBall.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(coreBall.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
		}

		[Test]
		public void PhysicalForceRepelsWithNegativeStrength()
		{
			var ball = CreateBall();
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = -400f
			};

			MagnetPhysics.ApplyPhysicalForce(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.GreaterThan(0f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
		}

		[Test]
		public void VpxCompatibleGrabClampsBallToMagnetCenter()
		{
			var ball = CreateBall();
			ball.EventPosition = new float3(49f, -2f, 10f);
			ball.Velocity = new float3(3f, -4f, 5f);
			ball.OldVelocity = new float3(2f, 1f, -1f);
			ball.AngularMomentum = new float3(1f, 2f, 3f);
			var magnet = new MagnetState {
				Position = new float2(12f, -8f)
			};

			MagnetPhysics.ApplyVpxCompatibleGrab(ref ball, in magnet);

			Assert.That(ball.Position.xy, Is.EqualTo(magnet.Position));
			Assert.That(ball.Position.z, Is.EqualTo(10f));
			Assert.That(ball.EventPosition.xy, Is.EqualTo(magnet.Position));
			Assert.That(ball.Velocity, Is.EqualTo(new float3(0f, 0f, 5f)));
			Assert.That(ball.OldVelocity, Is.EqualTo(new float3(0f, 0f, -1f)));
			Assert.That(ball.AngularMomentum, Is.EqualTo(float3.zero));
		}

		[Test]
		public void VpxCompatibleGrabCarriesKinematicMagnetVelocity()
		{
			var ball = CreateBall();
			ball.EventPosition = new float3(49f, -2f, 10f);
			ball.Velocity = new float3(3f, -4f, 5f);
			ball.OldVelocity = new float3(2f, 1f, -1f);
			var magnet = new MagnetState {
				Position = new float2(12f, -8f)
			};
			var magnetVelocity = new float2(6f, -3f);

			MagnetPhysics.ApplyVpxCompatibleGrab(ref ball, in magnet, magnetVelocity);

			Assert.That(ball.Position.xy, Is.EqualTo(magnet.Position));
			Assert.That(ball.EventPosition.xy, Is.EqualTo(magnet.Position));
			Assert.That(ball.Velocity, Is.EqualTo(new float3(6f, -3f, 5f)));
			Assert.That(ball.OldVelocity, Is.EqualTo(new float3(6f, -3f, -1f)));
		}

		[Test]
		public void PhysicalHoldPullsBallWithoutTeleporting()
		{
			var ball = CreateBall();
			ball.Position = new float3(10f, 0f, 10f);
			ball.EventPosition = new float3(10f, 0f, 10f);
			ball.AngularMomentum = new float3(0f, 1f, 0f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Strength = 20f,
				GrabRadius = 20f
			};

			MagnetPhysics.ApplyPhysicalHold(ref ball, in magnet, 0.1f);

			Assert.That(ball.Position.x, Is.EqualTo(10f).Within(1e-5f));
			Assert.That(ball.EventPosition.x, Is.EqualTo(10f).Within(1e-5f));
			Assert.That(ball.Velocity.x, Is.LessThan(0f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(ball.AngularMomentum.y, Is.LessThan(1f));
		}

		[Test]
		public void PhysicalHoldDampsRelativeToKinematicMagnetVelocity()
		{
			var ball = CreateBall();
			ball.Position = new float3(0f, 0f, 10f);
			ball.Velocity = new float3(7f, -2f, 5f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Strength = 20f,
				GrabRadius = 20f
			};
			var magnetVelocity = new float2(7f, -2f);

			MagnetPhysics.ApplyPhysicalHold(ref ball, in magnet, 0.1f, magnetVelocity);

			Assert.That(ball.Velocity.x, Is.EqualTo(7f).Within(1e-5f));
			Assert.That(ball.Velocity.y, Is.EqualTo(-2f).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.EqualTo(5f).Within(1e-5f));
		}

		[Test]
		public void KinematicTransformUpdatesMagnetCenterAndHeight()
		{
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 1f
			};
			var matrix = float4x4.Translate(new float3(12f, -8f, 3f));

			MagnetPhysics.ApplyKinematicTransform(ref magnet, in matrix);

			Assert.That(magnet.Position, Is.EqualTo(new float2(12f, -8f)));
			Assert.That(magnet.Height, Is.EqualTo(3f).Within(1e-5f));
		}

		[Test]
		public void PlanarEjectUsesKickerAngleConvention()
		{
			var ball = CreateBall();
			ball.Velocity = new float3(0f, 0f, 5f);
			ball.OldVelocity = new float3(0f, 0f, -1f);
			ball.AngularMomentum = new float3(1f, 2f, 3f);

			MagnetPhysics.ApplyPlanarEject(ref ball, 20f, 90f);

			Assert.That(ball.Velocity.x, Is.EqualTo(20f).Within(1e-5f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.EqualTo(5f).Within(1e-5f));
			Assert.That(ball.OldVelocity.x, Is.EqualTo(20f).Within(1e-5f));
			Assert.That(ball.OldVelocity.y, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(ball.OldVelocity.z, Is.EqualTo(-1f).Within(1e-5f));
			Assert.That(ball.AngularMomentum, Is.EqualTo(float3.zero));
		}

		private static BallState CreateBall()
		{
			return new BallState {
				Id = 1,
				Position = new float3(50f, 0f, 10f),
				Velocity = new float3(0f, 0f, 0f)
			};
		}
	}

	/// <summary>
	/// A minimal <see cref="PhysicsState"/> over hand-created containers, so
	/// tests can drive the real update/state wiring instead of only the pure
	/// force helpers. Containers a magnet/turntable update never touches stay
	/// default.
	/// </summary>
	internal sealed class PhysicsStateHarness : IDisposable
	{
		internal NativeParallelHashMap<int, BallState> Balls;
		internal NativeParallelHashMap<int, float4x4> KinematicTransforms;
		internal NativeParallelHashMap<int, KinematicVelocityState> KinematicVelocities;
		internal InsideOfs InsideOfs;
		internal NativeQueue<EventData> EventQueue;

		private PhysicsEnv _env;
		private NativeOctree<int> _octree;
		private NativeColliders _colliders;
		private NativeColliders _kinematicColliders;
		private NativeColliders _kinematicCollidersAtIdentity;
		private NativeParallelHashMap<int, float4x4> _kinematicTargetTransforms;
		private NativeParallelHashMap<int, float4x4> _nonTransformableColliderTransforms;
		private NativeParallelHashMap<int, NativeColliderIds> _kinematicColliderLookups;
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

		internal PhysicsStateHarness()
		{
			Balls = new NativeParallelHashMap<int, BallState>(4, Allocator.Persistent);
			KinematicTransforms = new NativeParallelHashMap<int, float4x4>(4, Allocator.Persistent);
			KinematicVelocities = new NativeParallelHashMap<int, KinematicVelocityState>(4, Allocator.Persistent);
			InsideOfs = new InsideOfs(Allocator.Persistent);
			EventQueue = new NativeQueue<EventData>(Allocator.Persistent);
		}

		internal PhysicsState CreateState()
		{
			var events = EventQueue.AsParallelWriter();
			return new PhysicsState(ref _env, ref _octree, ref _colliders, ref _kinematicColliders,
				ref _kinematicCollidersAtIdentity, ref KinematicTransforms, ref _kinematicTargetTransforms,
				ref _nonTransformableColliderTransforms, ref _kinematicColliderLookups, ref events,
				ref InsideOfs, ref Balls, ref _bumperStates, ref _dropTargetStates, ref _flipperStates, ref _gateStates,
				ref _hitTargetStates, ref _kickerStates, ref _magnetStates, ref _plungerStates, ref _spinnerStates,
				ref _surfaceStates, ref _turntableStates, ref _triggerStates, ref _disabledCollisionItems, ref _swapBallCollisionHandling,
				ref _elasticityLuts, ref _frictionLuts, ref KinematicVelocities);
		}

		public void Dispose()
		{
			Balls.Dispose();
			KinematicTransforms.Dispose();
			KinematicVelocities.Dispose();
			InsideOfs.Dispose();
			EventQueue.Dispose();
		}
	}
}
