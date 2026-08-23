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
using System.Text;
using NativeTrees;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Test
{
	public class MagnetPhysicsTests
	{
		[Test]
		public void ComponentDistancesAreAuthoredDirectlyInVpxUnits()
		{
			var gameObject = new GameObject("VPX Magnet Unit Test");
			try {
				var component = gameObject.AddComponent<MagnetComponent>();
				component.Radius = 61.91f;
				component.PoleRadius = 10f;
				component.GrabBall = true;
				component.GrabRadius = 10.8f;
				component.CylinderRadius = 25.16466f;
				component.CylinderHeight = 49.92385f;
				component.HeightRange = 50f;

				var state = component.CreateState();

				Assert.That(state.Radius, Is.EqualTo(component.Radius));
				Assert.That(state.PoleRadius, Is.EqualTo(component.PoleRadius));
				Assert.That(state.GrabRadius, Is.EqualTo(component.GrabRadius));
				Assert.That(state.CylinderRadius, Is.EqualTo(component.CylinderRadius));
				Assert.That(state.CylinderHeight, Is.EqualTo(component.CylinderHeight));
				Assert.That(state.HeightRange, Is.EqualTo(component.HeightRange));
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void CylindricalDampingIsStoredInPhysicsState()
		{
			var gameObject = new GameObject("Cylindrical Magnet Damping State Test");
			try {
				var component = gameObject.AddComponent<MagnetComponent>();
				gameObject.transform.localScale = new Vector3(0.232208654f, 0.232208654f, 0.232208654f);
				component.MagnetType = MagnetType.Cylindrical;
				component.CylindricalDamping = 0.35f;

				var state = component.CreateState();

				Assert.That(state.CylindricalDamping, Is.EqualTo(0.35f));
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void CylindricalDampingRoundTripsAndLegacyPackagesUseTheDefault()
		{
			var gameObject = new GameObject("Cylindrical Magnet Damping Pack Test");
			try {
				var component = gameObject.AddComponent<MagnetComponent>();
				component.CylindricalDamping = 0.35f;
				component.GenerateCylinderCollider = true;

				var bytes = component.Pack();
				component.CylindricalDamping = 2f;
				component.Unpack(bytes);

				Assert.That(component.CylindricalDamping, Is.EqualTo(0.35f));
				Assert.That(component.GenerateCylinderCollider, Is.True);

				component.CylindricalDamping = 0f;
				component.GenerateCylinderCollider = true;
				component.Unpack(Encoding.UTF8.GetBytes("{}"));
				Assert.That(component.CylindricalDamping, Is.EqualTo(MagnetComponent.DefaultCylindricalDamping));
				Assert.That(component.GenerateCylinderCollider, Is.False);
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void GeneratedCylinderColliderUsesAuthoredVpxDimensions()
		{
			var gameObject = new GameObject("Generated Cylindrical Magnet Collider Test");
			var nonTransformableTransforms = new NativeParallelHashMap<int, float4x4>(0, Allocator.Temp);
			var colliders = new ColliderReference(ref nonTransformableTransforms, Allocator.Temp);
			try {
				var component = gameObject.AddComponent<MagnetComponent>();
				component.MagnetType = MagnetType.Cylindrical;
				component.CylinderRadius = 25.16466f;
				component.CylinderHeight = 49.92385f;

				var collidable = (ICollidableComponent)component;
				Assert.That(collidable.IsCollidable, Is.True,
					"an unused optional collider must not disable another collider sharing the magnet's item ID");
				collidable.GetColliders(null, null, ref colliders, float4x4.identity, 0.1f);
				Assert.That(colliders.Count, Is.Zero);

				component.GenerateCylinderCollider = true;
				var matrix = component.GetLocalToPlayfieldMatrixInVpx(float4x4.identity);
				collidable.GetColliders(null, null, ref colliders, matrix, 0.1f);

				Assert.That(collidable.IsCollidable, Is.True);
				Assert.That(colliders.Count, Is.EqualTo(1));
				var collider = (CircleCollider)colliders[0];
				Assert.That(collider.Center, Is.EqualTo(float2.zero));
				Assert.That(collider.Radius, Is.EqualTo(component.CylinderRadius));
				Assert.That(collider.ZLow, Is.Zero);
				Assert.That(collider.ZHigh, Is.EqualTo(component.CylinderHeight));
				Assert.That(collider.Header.ItemId, Is.EqualTo(component.ItemId));
				Assert.That(matrix.GetScale(), Is.EqualTo(new float3(1f)),
					"visual transform scale must not resize dimensions authored directly in VPX units");
			} finally {
				colliders.Dispose();
				nonTransformableTransforms.Dispose();
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void CylindricalGrabUsesAutomaticContactTolerance()
		{
			var gameObject = new GameObject("Cylindrical Magnet Grab Test");
			try {
				var component = gameObject.AddComponent<MagnetComponent>();
				component.MagnetType = MagnetType.Cylindrical;
				component.GrabBall = true;
				component.GrabRadius = 500f;

				var state = component.CreateState();

				Assert.That(state.GrabRadius, Is.EqualTo(MagnetPhysics.CylindricalContactTolerance));
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void NewComponentDefaultsPreserveTheFormerMillimeterDimensionsInVpx()
		{
			var gameObject = new GameObject("Magnet Default Unit Test");
			try {
				var component = gameObject.AddComponent<MagnetComponent>();

				Assert.That(component.Radius, Is.EqualTo(50f * 1.85271f).Within(1e-5f));
				Assert.That(component.PoleRadius, Is.EqualTo(10f * 1.85271f).Within(1e-5f));
				Assert.That(component.GrabRadius, Is.EqualTo(10.8f * 1.85271f).Within(1e-5f));
				Assert.That(component.HeightRange, Is.EqualTo(50f * 1.85271f).Within(1e-5f));
				Assert.That(component.PhysicsFriction, Is.Zero,
					"the generated sidewall leaves settling to cylindrical damping by default");
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

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
		public void VpxCompatibleForceReportsTheFieldWithoutVelocityDamping()
		{
			var ball = CreateBall();
			ball.Velocity = new float3(4f, 0f, 0f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Radius = 100f,
				EffectiveStrength = 12f
			};

			MagnetPhysics.ApplyVpxCompatibleForce(ref ball, in magnet, 1f, 0.5f);

			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(-1.587634f).Within(1e-5f));
			Assert.That(ball.ExternalAcceleration.x, Is.Not.EqualTo(ball.Velocity.x - 4f).Within(0.1f),
				"velocity damping is not a sustained load for the contact solver");
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
		public void CylindricalSurfaceMeasuresSidewallFromBallSkin()
		{
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 10f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};
			var contactPositions = new[] {
				new float3(25f, 0f, 30f),
				new float3(-25f, 0f, 30f),
				new float3(0f, 25f, 30f),
				new float3(0f, -25f, 30f)
			};

			foreach (var position in contactPositions) {
				var ball = CreateBall();
				ball.Position = position;
				ball.Radius = 5f;
				var surface = MagnetPhysics.CylinderSurface(in ball, in magnet);

				Assert.That(surface.AirGap, Is.EqualTo(0f).Within(1e-5f), $"{position} should touch the cylinder");
				Assert.That(surface.ExteriorWeight, Is.EqualTo(1f), "true sidewall contact must receive the full exterior field");
			}
		}

		[Test]
		public void CylindricalSurfaceDoesNotTreatCapFaceAsSidewallContact()
		{
			var ball = CreateBall();
			ball.Position = new float3(0f, 0f, 55f);
			ball.Radius = 5f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 10f,
				Radius = 100f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};

			var surface = MagnetPhysics.CylinderSurface(in ball, in magnet);

			Assert.That(surface.AirGap, Is.GreaterThan(15f));
			Assert.That(surface.ExteriorWeight, Is.Zero);
			Assert.That(MagnetPhysics.IsBallInRange(in ball, in magnet), Is.False);
		}

		[Test]
		public void CylindricalSurfaceHasNoExteriorFieldInsideTheWall()
		{
			var ball = CreateBall();
			ball.Position = new float3(10f, 0f, 20f);
			ball.Radius = 5f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};

			var surface = MagnetPhysics.CylinderSurface(in ball, in magnet);

			Assert.That(surface.RadialDistance, Is.LessThan(magnet.CylinderRadius));
			Assert.That(surface.ExteriorWeight, Is.Zero, "a ball centre inside the sidewall radius is not in the exterior field");
			Assert.That(MagnetPhysics.HasCylindricalField(in surface), Is.False);
		}

		[Test]
		public void CylindricalRimContactReportsItsInwardAcceleration()
		{
			var ball = CreateBall();
			ball.Position = new float3(22.5f, 0f, 44.330127f);
			ball.Radius = 5f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};
			var surface = MagnetPhysics.CylinderSurface(in ball, in magnet);

			MagnetPhysics.ApplyCylindricalPhysicalForce(ref ball, in magnet, 1f);

			Assert.That(surface.AirGap, Is.Zero.Within(1e-5f));
			Assert.That(surface.ExteriorWeight, Is.EqualTo(0.5f).Within(1e-5f));
			Assert.That(ball.Velocity.x, Is.LessThan(0f));
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(ball.Velocity.x).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.Zero);
			Assert.That(ball.ExternalAcceleration.z, Is.Zero);
		}

		[Test]
		public void CylindricalRangeExtendsOutwardFromSurface()
		{
			var ball = CreateBall();
			ball.Position = new float3(30f, 0f, 20f);
			ball.Radius = 5f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 6f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};

			Assert.That(MagnetPhysics.IsBallInRange(in ball, in magnet), Is.True, "the ball skin is five units from the cylinder");
			magnet.Radius = 4f;
			Assert.That(MagnetPhysics.IsBallInRange(in ball, in magnet), Is.False);
		}

		[Test]
		public void CylindricalForceIsPlanarAndDoesNotCreateACapField()
		{
			var sideBall = CreateBall();
			sideBall.Position = new float3(26f, 0f, 20f);
			sideBall.Radius = 5f;
			var capBall = CreateBall();
			capBall.Position = new float3(0f, 0f, 45f);
			capBall.Radius = 5f;
			capBall.Velocity = new float3(0f, 0f, 3f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				Strength = 100f,
				EffectiveStrength = 100f,
				PoleRadius = 20f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.ApplyCylindricalPhysicalForce(ref sideBall, in magnet, 1f);
			MagnetPhysics.ApplyCylindricalPhysicalForce(ref capBall, in magnet, 1f);

			Assert.That(sideBall.Velocity.x, Is.LessThan(0f));
			Assert.That(sideBall.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(sideBall.Velocity.z, Is.Zero);
			Assert.That(capBall.Velocity.x, Is.Zero);
			Assert.That(capBall.Velocity.y, Is.Zero);
			Assert.That(capBall.Velocity.z, Is.EqualTo(3f).Within(1e-5f));
		}

		[Test]
		public void CylindricalDampingSmoothlyReducesPlanarMotionAndAxialSpinWithoutChangingHeight()
		{
			var undampedBall = CreateBall();
			undampedBall.Position = new float3(25f, 0f, 20f);
			undampedBall.Radius = 5f;
			undampedBall.Velocity = new float3(50f, 20f, 3f);
			undampedBall.AngularMomentum = new float3(0f, 4f, 6f);
			var dampedBall = undampedBall;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical,
				CylindricalDamping = 0f
			};

			MagnetPhysics.ApplyCylindricalPhysicalHold(ref undampedBall, in magnet, 1f);
			magnet.CylindricalDamping = 1f;
			MagnetPhysics.ApplyCylindricalPhysicalHold(ref dampedBall, in magnet, 1f);

			Assert.That(math.abs(dampedBall.Velocity.x), Is.LessThan(math.abs(undampedBall.Velocity.x)));
			Assert.That(undampedBall.Velocity.y, Is.EqualTo(20f).Within(1e-5f));
			Assert.That(dampedBall.Velocity.y, Is.GreaterThan(0f).And.LessThan(20f), "viscous damping must reduce motion without reversing it");
			Assert.That(undampedBall.Velocity.z, Is.EqualTo(3f));
			Assert.That(dampedBall.Velocity.z, Is.EqualTo(3f));
			Assert.That(undampedBall.AngularMomentum.y, Is.EqualTo(4f).Within(1e-5f));
			Assert.That(dampedBall.AngularMomentum.y, Is.EqualTo(4f).Within(1e-5f));
			Assert.That(undampedBall.AngularMomentum.z, Is.EqualTo(6f).Within(1e-5f));
			Assert.That(dampedBall.AngularMomentum.z, Is.GreaterThan(0f).And.LessThan(6f));
		}

		[Test]
		public void CylindricalTangentialDampingDoesNotBrakeFlyBys()
		{
			var ball = CreateBall();
			ball.Position = new float3(35f, 0f, 20f);
			ball.Radius = 5f;
			ball.Velocity = new float3(0f, 20f, 3f);
			ball.AngularMomentum = new float3(1f, 2f, 3f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical,
				CylindricalDamping = 1f
			};

			MagnetPhysics.ApplyCylindricalPhysicalForce(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.LessThan(0f));
			Assert.That(ball.Velocity.y, Is.EqualTo(20f).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.EqualTo(3f));
			Assert.That(ball.AngularMomentum, Is.EqualTo(new float3(1f, 2f, 3f)));
		}

		[Test]
		public void CylindricalGrabDampingControlsTangentialMotionAcrossPhysicsTicks()
		{
			var undampedSpeed = SimulateCylindricalGrabTangentialSpeed(0f);
			var defaultSpeed = SimulateCylindricalGrabTangentialSpeed(1f);
			var strongerDampingSpeed = SimulateCylindricalGrabTangentialSpeed(2f);

			Assert.That(undampedSpeed, Is.EqualTo(20f).Within(1e-5f));
			Assert.That(defaultSpeed, Is.GreaterThan(0f).And.LessThan(undampedSpeed));
			Assert.That(strongerDampingSpeed, Is.GreaterThan(0f).And.LessThan(defaultSpeed));
		}

		[Test]
		public void CylindricalHoldReportsInwardAccelerationToContactSolver()
		{
			var ball = CreateBall();
			ball.Position = new float3(25f, 0f, 20f);
			ball.Radius = 5f;
			ball.Velocity = new float3(0f, 0f, 2f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical,
				CylindricalDamping = 1f
			};

			MagnetPhysics.ApplyCylindricalPhysicalHold(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.LessThan(0f));
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(-1000f * 1.5f / 56f).Within(1e-5f),
				"the contact solver must see the magnetic field without viscous damping");
			Assert.That(ball.Velocity.z, Is.EqualTo(2f).Within(1e-5f));
			Assert.That(ball.ExternalAcceleration.z, Is.Zero);
		}

		[Test]
		public void CylindricalHoldDoesNotClampSeparatingVelocityToAnAnalyticSurface()
		{
			var ball = CreateBall();
			ball.Position = new float3(25f, 0f, 20f);
			ball.Radius = 5f;
			ball.Velocity = new float3(0.1f, 0f, 0f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.ApplyCylindricalPhysicalHold(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.LessThan(0f));
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(ball.Velocity.x - 0.1f).Within(1e-5f));
		}

		[Test]
		public void CylindricalAttractionAppliesFullForceAtAuthoredContact()
		{
			var ball = CreateBall();
			ball.Position = new float3(25f, 0f, 20f);
			ball.Radius = 5f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.ApplyCylindricalPhysicalForce(ref ball, in magnet, 1f);

			var expectedAcceleration = -1000f * 1.5f / 56f;
			Assert.That(ball.Velocity.x, Is.EqualTo(expectedAcceleration).Within(1e-5f));
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(expectedAcceleration).Within(1e-5f));
		}

		[Test]
		public void CylindricalForceFallsOffSmoothlyOutsideAuthoredContact()
		{
			var contactBall = CreateBall();
			contactBall.Position = new float3(25f, 0f, 20f);
			contactBall.Radius = 5f;
			var nearBall = contactBall;
			nearBall.Position = new float3(25.5f, 0f, 20f);
			var edgeBall = nearBall;
			edgeBall.Position.x = 27f;
			var outsideBall = nearBall;
			outsideBall.Position.x = 27.01f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 4f,
				EffectiveStrength = 56f / 1.5f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.ApplyCylindricalPhysicalForce(ref contactBall, in magnet, 1f);
			MagnetPhysics.ApplyCylindricalPhysicalForce(ref nearBall, in magnet, 1f);
			MagnetPhysics.ApplyCylindricalPhysicalForce(ref edgeBall, in magnet, 1f);
			MagnetPhysics.ApplyCylindricalPhysicalForce(ref outsideBall, in magnet, 1f);

			Assert.That(contactBall.Velocity.x, Is.EqualTo(-1f).Within(1e-5f));
			Assert.That(math.abs(nearBall.Velocity.x), Is.LessThan(math.abs(contactBall.Velocity.x)));
			Assert.That(math.abs(edgeBall.Velocity.x), Is.LessThan(math.abs(nearBall.Velocity.x)));
			Assert.That(math.abs(outsideBall.Velocity.x), Is.LessThan(math.abs(edgeBall.Velocity.x)));
			Assert.That(math.abs(outsideBall.Velocity.x - edgeBall.Velocity.x), Is.LessThan(0.01f));
		}

		[Test]
		public void CylindricalForceDoesNotUseAuthoredContactAsACollisionConstraint()
		{
			var ball = CreateBall();
			ball.Position = new float3(24.5f, 0f, 20f);
			ball.Radius = 5f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 10000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.ApplyCylindricalPhysicalForce(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.LessThan(0f),
				"the real collider, not the field radius, owns contact resolution");
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(ball.Velocity.x).Within(1e-5f));
		}

		[Test]
		public void CylindricalContactPreservesGravityAlongTheSidewall()
		{
			var ball = CreateBall();
			ball.Position = new float3(-25f, 0f, 20f);
			ball.Radius = 5f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical,
				CylindricalDamping = 1f
			};

			BallVelocityPhysics.UpdateVelocities(ref ball, new float3(0f, 1f, 0f), float2.zero);
			var gravitySpeed = ball.Velocity.y;
			MagnetPhysics.ApplyCylindricalPhysicalForce(ref ball, in magnet, 1f);

			Assert.That(ball.Velocity.x, Is.GreaterThan(0f));
			Assert.That(ball.Velocity.y, Is.GreaterThan(0f).And.LessThanOrEqualTo(gravitySpeed),
				"damping may slow downhill motion but must not stop or reverse gravity");
			Assert.That(ball.ExternalAcceleration.x, Is.GreaterThan(0f));
		}

		[Test]
		public void StaticContactBalancesCylindricalMagnetAtTheDownhillPoint()
		{
			var ball = CreateBall();
			ball.Position = new float3(0f, 25f, 20f);
			ball.Radius = 5f;
			ball.Mass = 1f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical,
				CylindricalDamping = 1f
			};

			var gravity = new float3(0f, 1f, 0f);
			BallVelocityPhysics.UpdateVelocities(ref ball, gravity, float2.zero);
			Assert.That(ball.Velocity.y, Is.GreaterThan(0f));
			MagnetPhysics.ApplyCylindricalPhysicalForce(ref ball, in magnet, PhysicsConstants.PhysFactor);
			Assert.That(ball.Velocity.y, Is.LessThan(0f));
			var collEvent = new CollisionEventData {
				HitNormal = new float3(0f, 1f, 0f),
				HitOrgNormalVelocity = ball.Velocity.y
			};
			BallCollider.HandleStaticContact(ref ball, in collEvent, 0f, PhysicsConstants.PhysFactor, gravity, float3.zero);

			Assert.That(ball.Velocity.x, Is.Zero.Within(1e-5f));
			Assert.That(ball.Velocity.y, Is.GreaterThan(0f),
				"the collider must balance both gravity and the sustained magnetic load");
		}

		[Test]
		public void CylindricalDownhillCornerDoesNotSinkUnderMultiContactFriction()
		{
			const float dt = PhysicsConstants.PhysFactor;
			const float contactRadius = 50.1f;
			var ball = new BallState {
				Id = 1,
				Position = new float3(0f, contactRadius, 25f),
				Radius = 25f,
				Mass = 1f
			};
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 75f,
				EffectiveStrength = 100f,
				CylinderRadius = 25.1f,
				CylinderHeight = 50f,
				CylindricalDamping = 0.15f,
				MagnetType = MagnetType.Cylindrical
			};
			var gravity = new float3(0f, 0.184f, -1.753f);
			var playfieldNormal = new float3(0f, 0f, 1f);

			for (var i = 0; i < 60_000; i++) {
				BallVelocityPhysics.UpdateVelocities(ref ball, gravity, float2.zero);
				MagnetPhysics.ApplyCylindricalPhysicalForce(ref ball, in magnet, dt);
				ball.Position += ball.Velocity * dt;

				var frictionVelocity = ball.Velocity;
				var frictionAngularMomentum = ball.AngularMomentum;
				var acceleration = gravity + ball.ExternalAcceleration;
				var cylinderNormal = new float3(math.normalizesafe(ball.Position.xy), 0f);
				var cylinderFrictionAcceleration = acceleration -
				                                  ContactPhysics.SupportedAcceleration(in acceleration, in playfieldNormal);
				var playfieldFrictionAcceleration = acceleration -
				                                   ContactPhysics.SupportedAcceleration(in acceleration, in cylinderNormal);
				var cylinderContact = new CollisionEventData {
					HitNormal = cylinderNormal,
					HitOrgNormalVelocity = math.dot(frictionVelocity, cylinderNormal)
				};
				var playfieldContact = new CollisionEventData {
					HitNormal = playfieldNormal,
					HitOrgNormalVelocity = math.dot(frictionVelocity, playfieldNormal)
				};

				BallCollider.HandleStaticContact(ref ball, in cylinderContact, 0.3f, dt, in gravity,
					float3.zero, in cylinderFrictionAcceleration, in frictionVelocity, in frictionAngularMomentum);
				BallCollider.HandleStaticContact(ref ball, in playfieldContact, 0.075f, dt, in gravity,
					float3.zero, in playfieldFrictionAcceleration, in frictionVelocity, in frictionAngularMomentum);
			}

			Assert.That(math.length(ball.Position.xy), Is.GreaterThanOrEqualTo(contactRadius - PhysicsConstants.PhysTouch));
			Assert.That(ball.Position.z, Is.GreaterThanOrEqualTo(25f - PhysicsConstants.PhysTouch));
		}

		[Test]
		public void CylindricalForceAlwaysPointsRadiallyTowardTheCylinder()
		{
			var radialDirection = new float2(math.cos(math.radians(2f)), math.sin(math.radians(2f)));
			var ball = CreateBall();
			ball.Position = new float3(radialDirection * 25f, 20f);
			ball.Radius = 5f;
			ball.Mass = 1f;
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical,
				CylindricalDamping = 0f
			};
			MagnetPhysics.ApplyCylindricalPhysicalForce(ref ball, in magnet, PhysicsConstants.PhysFactor);

			var forceDirection = math.normalizesafe(ball.ExternalAcceleration.xy);
			Assert.That(forceDirection.x, Is.EqualTo(-radialDirection.x).Within(1e-5f));
			Assert.That(forceDirection.y, Is.EqualTo(-radialDirection.y).Within(1e-5f));
		}

		[Test]
		public void OutwardExternalLoadCannotTurnContactFrictionIntoAcceleration()
		{
			var ball = CreateBall();
			ball.Mass = 1f;
			ball.Radius = 1f;
			ball.Velocity = new float3(1f, 0f, 0f);
			ball.ExternalAcceleration = new float3(0f, 0f, 3f);
			var collEvent = new CollisionEventData {
				HitNormal = new float3(0f, 0f, 1f),
				HitOrgNormalVelocity = 0f
			};

			BallCollider.HandleStaticContact(ref ball, in collEvent, 1f, PhysicsConstants.PhysFactor,
				new float3(0f, 0f, -1f), float3.zero);

			Assert.That(ball.Velocity.x, Is.EqualTo(1f).Within(1e-5f),
				"a separating load has no normal force and therefore no friction budget");
			Assert.That(ball.AngularMomentum, Is.EqualTo(float3.zero));
		}

		[Test]
		public void CylindricalForceReportsAccelerationRelativeToMovingMagnet()
		{
			var ball = CreateBall();
			ball.Position = new float3(25f, 0f, 20f);
			ball.Radius = 5f;
			var magnetVelocity = new float3(-2f, 0f, 4f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 1000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.ApplyCylindricalPhysicalForce(ref ball, in magnet, 1f, magnetVelocity);

			Assert.That(ball.Velocity.x, Is.LessThan(magnetVelocity.x));
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(ball.Velocity.x).Within(1e-5f));
			Assert.That(ball.Velocity.z, Is.Zero, "vertical magnet velocity must not leak into the ball");
			Assert.That(ball.ExternalAcceleration.z, Is.Zero);
		}

		[Test]
		public void BallVelocityStepClearsPreviousExternalAcceleration()
		{
			var ball = CreateBall();
			ball.ExternalAcceleration = new float3(1f, 2f, 3f);

			BallVelocityPhysics.UpdateVelocities(ref ball, float3.zero, float2.zero);

			Assert.That(ball.ExternalAcceleration, Is.EqualTo(float3.zero));
		}

		[Test]
		public void CylindricalInfluenceDistanceDefinesTheCompleteFalloff()
		{
			var magnet = new MagnetState {
				Radius = 100f,
				EffectiveStrength = 56f / 1.5f,
				PoleRadius = 1f,
				MagnetType = MagnetType.Cylindrical
			};

			Assert.That(MagnetPhysics.CylindricalSurfaceForceMagnitude(0f, in magnet), Is.EqualTo(1f).Within(1e-5f));
			Assert.That(MagnetPhysics.CylindricalSurfaceForceMagnitude(50f, in magnet), Is.EqualTo(0.5f).Within(1e-5f));
			Assert.That(MagnetPhysics.CylindricalSurfaceForceMagnitude(100f, in magnet), Is.Zero);

			magnet.PoleRadius = 1000f;
			Assert.That(MagnetPhysics.CylindricalSurfaceForceMagnitude(50f, in magnet), Is.EqualTo(0.5f).Within(1e-5f), "Pole Radius is not a cylindrical control");
		}

		[Test]
		public void CylindricalGrabCapturesAtColliderContact()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(25f, 0f, 20f),
				Radius = 5f,
				Velocity = new float3(0f, 1000f, 0f)
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				Strength = 100f,
				CommandedPower = 1f,
				PoleRadius = 20f,
				GrabRadius = 1f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				IsEnabled = true,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			var ball = harness.Balls[1];
			Assert.That(magnet.GrabbedBalls.Value, Is.Not.EqualTo(0UL));
			Assert.That(ball.Velocity.x, Is.LessThan(0f));
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(ball.Velocity.x / 0.1f).Within(1e-4f),
				"a held ball must report its magnetic load to the contact solver");
			Assert.That(ball.Velocity.y, Is.EqualTo(1000f).Within(1e-5f), "tangential motion must not prevent capture or be magnetically stopped");
		}

		[Test]
		public void CylindricalAttractionOnlyUpdateReportsItsMagneticLoad()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(25f, 0f, 20f),
				Radius = 5f
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				Strength = 1000f,
				CommandedPower = 1f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				IsEnabled = true,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			var ball = harness.Balls[1];
			Assert.That(magnet.GrabbedBalls.Value, Is.Zero);
			Assert.That(ball.Velocity.x, Is.LessThan(0f));
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(ball.Velocity.x / 0.1f).Within(1e-4f));
		}

		[Test]
		public void DisablingCylindricalGrabReleasesHeldBall()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(25f, 0f, 20f),
				Radius = 5f
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				Strength = 100f,
				CommandedPower = 1f,
				GrabRadius = MagnetPhysics.CylindricalContactTolerance,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				IsEnabled = true,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);
			Assert.That(magnet.GrabbedBalls.Value, Is.Not.Zero);

			magnet.GrabRadius = 0f;
			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			Assert.That(magnet.GrabbedBalls.Value, Is.Zero);
		}

		[Test]
		public void CylindricalCaptureDependsOnStrengthAndSeparatingSpeed()
		{
			var ball = CreateBall();
			ball.Position = new float3(25f, 0f, 20f);
			ball.Radius = 5f;
			ball.Velocity = new float3(50f, 1000f, 0f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 100f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};
			var surface = MagnetPhysics.CylinderSurface(in ball, in magnet);

			Assert.That(MagnetPhysics.CanCaptureCylindrical(in ball, in magnet, in surface, float3.zero), Is.False);
			magnet.EffectiveStrength = 10000f;
			Assert.That(MagnetPhysics.CanCaptureCylindrical(in ball, in magnet, in surface, float3.zero), Is.True);
		}

		[Test]
		public void CylindricalCaptureRejectsVerticalEscapeItCannotRetain()
		{
			var ball = CreateBall();
			ball.Position = new float3(25f, 0f, 40f);
			ball.Radius = 5f;
			ball.Velocity = new float3(0f, 0f, PhysicsConstants.ContactVel * 2f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 10000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};
			var surface = MagnetPhysics.CylinderSurface(in ball, in magnet);

			Assert.That(MagnetPhysics.CanCaptureCylindrical(in ball, in magnet, in surface, float3.zero), Is.False);
			ball.Velocity.z *= -1f;
			Assert.That(MagnetPhysics.CanCaptureCylindrical(in ball, in magnet, in surface, float3.zero), Is.True,
				"motion from the top rim back along the sidewall must be capturable");
		}

		[Test]
		public void CylindricalCaptureUsesVerticalVelocityRelativeToMovingMagnet()
		{
			var ball = CreateBall();
			ball.Position = new float3(25f, 0f, 40f);
			ball.Radius = 5f;
			ball.Velocity = new float3(0f, 0f, 10f);
			var magnetVelocity = new float3(0f, 0f, 10f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 10000f,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				MagnetType = MagnetType.Cylindrical
			};
			var surface = MagnetPhysics.CylinderSurface(in ball, in magnet);

			Assert.That(MagnetPhysics.CanCaptureCylindrical(in ball, in magnet, in surface, magnetVelocity), Is.True);
		}

		[Test]
		public void CylindricalUnlimitedHeightAllowsVerticalSidewallMotion()
		{
			var ball = CreateBall();
			ball.Position = new float3(25f, 0f, 100f);
			ball.Radius = 5f;
			ball.Velocity = new float3(0f, 0f, 10f);
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				EffectiveStrength = 10000f,
				CylinderRadius = 20f,
				CylinderHeight = 0f,
				MagnetType = MagnetType.Cylindrical
			};
			var surface = MagnetPhysics.CylinderSurface(in ball, in magnet);

			Assert.That(surface.AirGap, Is.Zero);
			Assert.That(MagnetPhysics.CanCaptureCylindrical(in ball, in magnet, in surface, float3.zero), Is.True);
		}

		[Test]
		public void CylindricalGodzillaContactUsesRealForceInsteadOfAnalyticConstraint()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(50.16466f, 0f, 24.961925f),
				Radius = 25f,
				Velocity = float3.zero
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 166f,
				Strength = 100f,
				CommandedPower = 64f / 255f,
				RiseTime = 2f,
				FallTime = 2f,
				CylinderRadius = 25.1f,
				CylinderHeight = 49.92385f,
				IsEnabled = true,
				MagnetType = MagnetType.Cylindrical
			};

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			var ball = harness.Balls[1];
			Assert.That(ball.Velocity.x, Is.LessThan(0f),
				"a small field/collider radius mismatch must not turn the magnet into an analytic constraint");
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(ball.Velocity.x / 0.1f).Within(1e-4f));
		}

		[Test]
		public void CylindricalGodzillaDiagnosticPulseDeflectsBallAcrossAirGap()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(60.16466f, 0f, 24.961925f),
				Radius = 25f,
				Velocity = float3.zero
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 166f,
				Strength = 100f,
				CommandedPower = 64f / 255f,
				RiseTime = 2f,
				FallTime = 2f,
				CylinderRadius = 25.16466f,
				CylinderHeight = 49.92385f,
				IsEnabled = true,
				MagnetType = MagnetType.Cylindrical
			};

			for (var i = 0; i < 120; i++) {
				MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);
			}

			Assert.That(harness.Balls[1].Velocity.x, Is.LessThan(-1f),
				"the real diagnostic pulse should produce a clearly measurable inward deflection across an air gap");
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
		public void PhysicalHoldReportsOnlyItsSustainedSpringLoad()
		{
			var ball = CreateBall();
			ball.Position = new float3(10f, 0f, 10f);
			ball.Velocity = new float3(-5f, 0f, 0f);
			var magnet = new MagnetState {
				Position = float2.zero,
				EffectiveStrength = 20f,
				GrabRadius = 20f
			};

			MagnetPhysics.ApplyPhysicalHold(ref ball, in magnet, 0.1f);

			Assert.That(ball.Velocity.x, Is.EqualTo(-5f).Within(1e-5f),
				"spring and velocity damping cancel for this state");
			Assert.That(ball.ExternalAcceleration.x, Is.EqualTo(-10f).Within(1e-5f),
				"the sustained spring load remains visible to contact resolution");
		}

		[Test]
		public void PhysicalHoldDoesNotAmplifyWeakCurrent()
		{
			var ball = CreateBall();
			ball.Position = new float3(10f, 0f, 10f);
			ball.Velocity = float3.zero;
			var magnet = new MagnetState {
				Position = float2.zero,
				EffectiveStrength = 0.1f,
				PoleRadius = 20f,
				GrabRadius = 20f
			};

			MagnetPhysics.ApplyPhysicalHold(ref ball, in magnet, 0.1f);

			Assert.That(math.abs(ball.Velocity.x), Is.LessThanOrEqualTo(0.01f), "the hold cap must not exceed the effective field");
		}

		[Test]
		public void PhysicalGrabRejectsFastFlyby()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(10f, 0f, 10f),
				Velocity = new float3(100f, 0f, 0f)
			});
			var magnet = CreatePhysicalGrabMagnet();

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			Assert.That(magnet.GrabbedBalls.Value, Is.EqualTo(0UL));
		}

		[Test]
		public void PhysicalGrabCapturesRetainableBall()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(10f, 0f, 10f),
				Velocity = new float3(1f, 0f, 0f)
			});
			var magnet = CreatePhysicalGrabMagnet();

			MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);

			Assert.That(magnet.GrabbedBalls.Value, Is.Not.EqualTo(0UL));
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

		private static float SimulateCylindricalGrabTangentialSpeed(float damping)
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			harness.Balls.Add(1, new BallState {
				Id = 1,
				Position = new float3(25f, 0f, 20f),
				Radius = 5f,
				Velocity = new float3(0f, 20f, 0f)
			});
			var magnet = new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				Strength = 1000f,
				CommandedPower = 1f,
				GrabRadius = MagnetPhysics.CylindricalContactTolerance,
				CylinderRadius = 20f,
				CylinderHeight = 40f,
				CylindricalDamping = damping,
				IsEnabled = true,
				MagnetType = MagnetType.Cylindrical
			};

			for (var i = 0; i < 10; i++) {
				MagnetPhysics.Update(17, ref magnet, ref state, 0.1f);
			}

			Assert.That(magnet.GrabbedBalls.Value, Is.Not.Zero);
			return harness.Balls[1].Velocity.y;
		}

		private static MagnetState CreatePhysicalGrabMagnet()
		{
			return new MagnetState {
				Position = float2.zero,
				Height = 0f,
				Radius = 100f,
				HeightRange = 50f,
				Strength = 20f,
				CommandedPower = 1f,
				PoleRadius = 20f,
				GrabRadius = 20f,
				IsEnabled = true,
				Profile = MagnetForceProfile.Physical
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
