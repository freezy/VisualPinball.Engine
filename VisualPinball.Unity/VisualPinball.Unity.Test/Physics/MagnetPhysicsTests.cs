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
				CommandedPower = 1f,
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
				EffectiveStrength = 10f,
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
				EffectiveStrength = 12f,
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
				EffectiveStrength = -10f,
				PlanarDamping = 1f
			};

			MagnetPhysics.ApplyVpxCompatibleForce(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.GreaterThan(0f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
		}

		[Test]
		public void PhysicalForcePeaksAroundFinitePoleAndDecays()
		{
			var axisBall = CreateBall();
			var poleBall = CreateBall();
			var farBall = CreateBall();
			axisBall.Position = new float3(0f, 0f, 0f);
			poleBall.Position = new float3(9f, 0f, 0f);
			farBall.Position = new float3(60f, 0f, 0f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = 400f,
				EffectiveStrength = 400f,
				PoleRadius = 20f
			};

			MagnetPhysics.ApplyPhysicalForce(ref axisBall, in magnet, 1f);
			MagnetPhysics.ApplyPhysicalForce(ref poleBall, in magnet, 1f);
			MagnetPhysics.ApplyPhysicalForce(ref farBall, in magnet, 1f);

			Assert.That(axisBall.Velocity.x, Is.EqualTo(0f), "lateral force is zero on the symmetry axis");
			Assert.That(poleBall.Velocity.x, Is.LessThan(0f));
			Assert.That(math.abs(poleBall.Velocity.x), Is.GreaterThan(math.abs(farBall.Velocity.x)));
		}

		[Test]
		public void PhysicalForceWeakensWithAirGap()
		{
			var nearBall = CreateBall();
			var highBall = CreateBall();
			nearBall.Position = new float3(10f, 0f, 5f);
			highBall.Position = new float3(10f, 0f, 40f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				HeightRange = 100f,
				Radius = 100f,
				Strength = 400f,
				EffectiveStrength = 400f,
				PoleRadius = 20f
			};

			MagnetPhysics.ApplyPhysicalForce(ref nearBall, in magnet, 1f);
			MagnetPhysics.ApplyPhysicalForce(ref highBall, in magnet, 1f);

			Assert.That(math.abs(nearBall.Velocity.x), Is.GreaterThan(math.abs(highBall.Velocity.x)));
		}

		[Test]
		public void PhysicalForceAttractsWithNegativeAuthoredStrength()
		{
			var ball = CreateBall();
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = -400f,
				EffectiveStrength = -400f,
				PoleRadius = 20f
			};

			MagnetPhysics.ApplyPhysicalForce(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.LessThan(0f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
		}

		[Test]
		public void PhysicalForceHasCompactRadiusCutoff()
		{
			var ball = CreateBall();
			ball.Position = new float3(100f, 0f, 0f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				Strength = 400f,
				EffectiveStrength = 400f,
				PoleRadius = 20f
			};

			MagnetPhysics.ApplyPhysicalForce(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity, Is.EqualTo(float3.zero));
		}

		[Test]
		public void SpatialRangeUsesSphericalDistance()
		{
			var ball = CreateBall();
			ball.Position = new float3(0f, 0f, 30f);
			var playfieldMagnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 40f,
				HeightRange = 10f
			};
			var spatialMagnet = playfieldMagnet;
			spatialMagnet.MagnetType = MagnetType.Spatial;

			Assert.That(MagnetPhysics.IsBallInRange(in ball, in playfieldMagnet), Is.False);
			Assert.That(MagnetPhysics.IsBallInRange(in ball, in spatialMagnet), Is.True);
		}

		[Test]
		public void SpatialPhysicalForcePullsInThreeDimensions()
		{
			var ball = CreateBall();
			ball.Position = new float3(0f, 0f, 50f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				Strength = 400f,
				EffectiveStrength = 400f,
				PoleRadius = 20f,
				MagnetType = MagnetType.Spatial
			};

			MagnetPhysics.ApplySpatialPhysicalForce(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.LessThan(0f));
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
				EffectiveStrength = 20f,
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
		public void SpatialGrabHoldsBallWithForceNotFreeze()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(5f, -2f, 14f),
				Velocity = float3.zero
			});
			var magnet = new MagnetState {
				Position = new float2(4f, -3f),
				Height = 12f,
				Radius = 100f,
				Strength = 20f,
				CommandedPower = 1f,
				GrabRadius = 20f,
				IsEnabled = true,
				MagnetType = MagnetType.Spatial
			};

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			var ball = harness.Balls[1];
			var offset = new float3(5f, -2f, 14f) - MagnetPhysics.Center3D(in magnet);
			// the ball stays a live physics object; the hold is a force, not a freeze
			Assert.That(ball.IsFrozen, Is.False, "a spatial magnet holds with a force, it must not freeze the ball");
			Assert.That(magnet.GrabbedBalls.Value, Is.Not.EqualTo(0UL), "ball should be grabbed");
			// the ball started at rest, so its velocity is the hold impulse, which pulls
			// toward the hold point (opposes the offset)
			Assert.That(math.dot(ball.Velocity, offset), Is.LessThan(0f), "the hold must pull the ball toward the hold point");
		}

		[Test]
		public void SpatialHoldTracksMovingHoldPoint()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(0f, 0f, 10f),
				Velocity = float3.zero
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 10f,
				Radius = 100f,
				Strength = 20f,
				CommandedPower = 1f,
				GrabRadius = 20f,
				IsEnabled = true,
				MagnetType = MagnetType.Spatial
			};

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);
			Assert.That(magnet.GrabbedBalls.Value, Is.Not.EqualTo(0UL));

			// move the hold point; the held ball (still near the origin in the harness,
			// which does not integrate displacement) is pulled toward the new point
			magnet.Position = new float2(8f, -5f);
			magnet.Height = 16f;
			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			var ball = harness.Balls[1];
			var offset = ball.Position - MagnetPhysics.Center3D(in magnet);
			Assert.That(ball.IsFrozen, Is.False);
			Assert.That(magnet.GrabbedBalls.Value, Is.Not.EqualTo(0UL), "ball stays held as the point moves");
			Assert.That(math.dot(ball.Velocity, offset), Is.LessThan(0f), "the hold pulls toward the moved hold point");
		}

		[Test]
		public void SpatialGrabReleasesBallKnockedOutsideGrabRadius()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			// the ball sits inside the outer radius but well outside the grab radius, as
			// if a hard hit pushed it out of the hold — it must be released, because the
			// hold is a force that can be overcome, not a rigid lock
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(50f, 0f, 10f),
				Velocity = new float3(60f, 0f, 0f)
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 10f,
				Radius = 100f,
				Strength = 20f,
				CommandedPower = 1f,
				GrabRadius = 20f,
				IsEnabled = true,
				MagnetType = MagnetType.Spatial
			};
			var bitIndex = harness.InsideOfs.GetOrCreateBitIndex(1);
			magnet.GrabbedBalls.SetBits(bitIndex, true);

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			var ball = harness.Balls[1];
			Assert.That(magnet.GrabbedBalls.Value, Is.EqualTo(0UL), "a ball knocked outside the grab radius is released");
			Assert.That(ball.IsFrozen, Is.False);
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
				EffectiveStrength = 20f,
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

		[Test]
		public void SpatialEjectAddsVerticalAngle()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(0f, 0f, 10f)
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 10f,
				Radius = 100f,
				Strength = 20f,
				GrabRadius = 20f,
				MagnetType = MagnetType.Spatial
			};
			var bitIndex = harness.InsideOfs.GetOrCreateBitIndex(1);
			magnet.GrabbedBalls.SetBits(bitIndex, true);

			MagnetPhysics.EjectGrabbedBalls(17, ref magnet, ref state, 20f, 90f, 30f);

			var ball = harness.Balls[1];
			Assert.That(ball.Velocity.x, Is.EqualTo(20f * math.cos(math.radians(30f))).Within(1e-5f));
			Assert.That(ball.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.EqualTo(10f).Within(1e-5f));
			Assert.That(magnet.GrabbedBalls.Value, Is.EqualTo(0UL));
		}

		[Test]
		public void KinematicRefreshFollowsTransformOnlyWhenKinematicAndSeeded()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.KinematicTransforms.Add(1, float4x4.Translate(new float3(120f, 80f, 30f)));

			var kinematic = new MagnetState { IsKinematic = true };
			MagnetPhysics.RefreshKinematicState(1, ref kinematic, ref state);
			Assert.That(kinematic.Position, Is.EqualTo(new float2(120f, 80f)));
			Assert.That(kinematic.Height, Is.EqualTo(30f).Within(1e-5f));

			var nonKinematic = new MagnetState { Position = new float2(5f, 5f) };
			MagnetPhysics.RefreshKinematicState(1, ref nonKinematic, ref state);
			Assert.That(nonKinematic.Position, Is.EqualTo(new float2(5f, 5f)), "non-kinematic magnets must not follow the transform");

			var unseeded = new MagnetState { IsKinematic = true, Position = new float2(5f, 5f) };
			MagnetPhysics.RefreshKinematicState(99, ref unseeded, ref state);
			Assert.That(unseeded.Position, Is.EqualTo(new float2(5f, 5f)), "unseeded items must keep their baked position");
		}

		[Test]
		public void KinematicRefreshDerivesVelocityFromStateMaps()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.KinematicTransforms.Add(1, float4x4.Translate(new float3(120f, 80f, 30f)));
			harness.KinematicVelocities.Add(1, new KinematicVelocityState {
				LinearVelocity = new float3(2f, -1f, 0f),
				Pivot = new float3(120f, 80f, 30f)
			});

			var magnet = new MagnetState { IsKinematic = true };
			var velocity = MagnetPhysics.RefreshKinematicState(1, ref magnet, ref state);

			Assert.That(velocity.x, Is.EqualTo(2f).Within(1e-5f));
			Assert.That(velocity.y, Is.EqualTo(-1f).Within(1e-5f));
			Assert.That(velocity.z, Is.EqualTo(0f).Within(1e-5f));
		}

		[Test]
		public void KinematicRefreshSubstitutesStepVelocityDuringCatchUp()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.KinematicTransforms.Add(1, float4x4.Translate(new float3(120f, 80f, 30f)));
			harness.KinematicVelocities.Add(1, new KinematicVelocityState {
				LinearVelocity = new float3(1f, 0f, 0f),
				StepVelocity = new float3(3f, 0f, 0f),
				Pivot = new float3(120f, 80f, 30f)
			});

			var magnet = new MagnetState { IsKinematic = true };
			var velocity = MagnetPhysics.RefreshKinematicState(1, ref magnet, ref state);

			Assert.That(velocity.x, Is.EqualTo(3f).Within(1e-5f), "rate-limited catch-up must expose the step rate");
		}

		[Test]
		public void PlanarEjectAddsCarrierVelocity()
		{
			var ball = CreateBall();

			MagnetPhysics.ApplyPlanarEject(ref ball, 20f, 90f, new float2(5f, -3f));

			Assert.That(ball.Velocity.x, Is.EqualTo(25f).Within(1e-5f));
			Assert.That(ball.Velocity.y, Is.EqualTo(-3f).Within(1e-5f));
		}

		[Test]
		public void SpatialCoilOffReleasesHeldBall()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(0f, 0f, 10f),
				Velocity = new float3(3f, -2f, 5f)
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 10f,
				Radius = 100f,
				Strength = 20f,
				GrabRadius = 20f,
				MagnetType = MagnetType.Spatial,
				IsEnabled = false
			};
			var bitIndex = harness.InsideOfs.GetOrCreateBitIndex(1);
			magnet.GrabbedBalls.SetBits(bitIndex, true);

			// coil off -> the disabled magnet releases its ball, which keeps its velocity
			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			var ball = harness.Balls[1];
			Assert.That(magnet.GrabbedBalls.Value, Is.EqualTo(0UL));
			Assert.That(ball.IsFrozen, Is.False);
			Assert.That(ball.Velocity, Is.EqualTo(new float3(3f, -2f, 5f)), "a released ball keeps its live velocity");
		}

		[Test]
		public void PhysicalCoilRampsAndDecaysWithTimeConstants()
		{
			var magnet = new MagnetState {
				Strength = 100f,
				CommandedPower = 1f,
				RiseTime = 2f,
				FallTime = 1f,
				IsEnabled = true,
				Profile = MagnetForceProfile.Physical
			};

			for (var i = 0; i < 20; i++) {
				MagnetPhysics.AdvanceCoil(ref magnet, 0.1f);
			}
			var currentAfterOneRiseConstant = magnet.EffectiveCurrent;
			Assert.That(currentAfterOneRiseConstant, Is.EqualTo(0.623f).Within(0.01f));
			Assert.That(magnet.EffectiveStrength, Is.EqualTo(100f * currentAfterOneRiseConstant * currentAfterOneRiseConstant).Within(1e-4f));

			magnet.IsEnabled = false;
			for (var i = 0; i < 10; i++) {
				MagnetPhysics.AdvanceCoil(ref magnet, 0.1f);
			}
			Assert.That(magnet.EffectiveCurrent, Is.EqualTo(currentAfterOneRiseConstant * 0.386f).Within(0.01f));
			Assert.That(magnet.EffectiveStrength, Is.GreaterThan(0f), "the field decays instead of disappearing instantly");
		}

		[Test]
		public void VpxCompatibleCoilResponseRemainsInstantaneous()
		{
			var magnet = new MagnetState {
				Strength = 20f,
				CommandedPower = 0.5f,
				RiseTime = 10f,
				FallTime = 10f,
				IsEnabled = true,
				Profile = MagnetForceProfile.VpxCompatible
			};

			MagnetPhysics.AdvanceCoil(ref magnet, 0.1f);

			Assert.That(magnet.EffectiveCurrent, Is.EqualTo(0.5f));
			Assert.That(magnet.EffectiveStrength, Is.EqualTo(10f));
		}

		[Test]
		public void SimulationThreadCoilDeliversEveryPwmValue()
		{
			var enableCount = 0;
			var valueCount = 0;
			var lastValue = 0f;
			var coil = new DeviceCoil(null,
				onEnableSimulationThread: () => enableCount++,
				onValueSimulationThread: value => {
					valueCount++;
					lastValue = value;
				});

			coil.OnCoilSimulationThread(0.25f);
			coil.OnCoilSimulationThread(0.75f);

			Assert.That(enableCount, Is.EqualTo(1), "both values keep the coil enabled");
			Assert.That(valueCount, Is.EqualTo(2), "PWM changes must not be collapsed into a bool");
			Assert.That(lastValue, Is.EqualTo(0.75f));
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
