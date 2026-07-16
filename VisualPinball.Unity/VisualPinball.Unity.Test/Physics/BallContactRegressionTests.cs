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
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	public class BallContactRegressionTests
	{
		private static readonly float3 ContactNormal = new(1f, 0f, 0f);

		[Test]
		public void NonUnitMassFlipperContactIsMassInvariant()
		{
			var lightBall = SolveFlipperContact(0.5f);
			var heavyBall = SolveFlipperContact(2f);

			AssertFloat3(lightBall.Velocity, heavyBall.Velocity);
			AssertFloat3(lightBall.AngularMomentum / lightBall.Inertia,
				heavyBall.AngularMomentum / heavyBall.Inertia);
		}

		[Test]
		public void ClearlySeparatingContactIsIgnoredAfterAnotherCollision()
		{
			var initialVelocity = new float3(4f, 0f, PhysicsConstants.ContactVel + 0.001f);
			var ball = CreateBall(1f, initialVelocity);
			var collEvent = new CollisionEventData {
				IsContact = true,
				HitNormal = new float3(0f, 0f, 1f),
				HitOrgNormalVelocity = -0.05f
			};
			var gravity = new float3(0f, 0f, -1f);

			BallCollider.HandleStaticContact(ref ball, in collEvent, 0.2f,
				PhysicsConstants.PhysFactor, in gravity, float3.zero);

			AssertFloat3(ball.Velocity, initialVelocity);
			AssertFloat3(ball.AngularMomentum, float3.zero);
		}

		[Test]
		public void LowNormalVelocityRubberContactUsesKineticFriction()
		{
			const float initialTangentialSpeed = 4f;
			var normal = new float3(0f, 0f, 1f);
			var tangent = new float3(1f, 0f, 0f);
			var ball = CreateBall(1f, initialTangentialSpeed * tangent + 0.02f * normal);
			var header = new ColliderHeader {
				Type = ColliderType.Triangle,
				ItemType = ItemType.Rubber,
				Material = new PhysicsMaterialData { Friction = 0.8f }
			};
			var collEvent = new CollisionEventData {
				IsContact = true,
				HitNormal = normal,
				HitOrgNormalVelocity = math.dot(ball.Velocity, normal)
			};
			var gravity = new float3(0f, 0f, -0.2f);

			Collider.Contact(in header, ref ball, in collEvent, PhysicsConstants.PhysFactor,
				in gravity, float3.zero);

			var surfacePoint = -ball.Radius * normal;
			var surfaceVelocity = BallState.SurfaceVelocity(in ball, in surfacePoint);
			var tangentialSlip = surfaceVelocity - normal * math.dot(surfaceVelocity, normal);
			Assert.That(math.length(tangentialSlip), Is.LessThan(initialTangentialSpeed));
		}

		[TestCase(0f)]
		[TestCase(-1f)]
		[TestCase(float.NaN)]
		[TestCase(float.PositiveInfinity)]
		public void BallStateCreationRejectsNonPositiveRadius(float radius)
		{
			var gameObject = new GameObject("Invalid ball radius");
			try {
				var component = gameObject.AddComponent<BallComponent>();
				component.Radius = radius;
				Assert.Throws<ArgumentOutOfRangeException>(() => component.CreateState());
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[TestCase(0f)]
		[TestCase(-1f)]
		[TestCase(float.NaN)]
		[TestCase(float.PositiveInfinity)]
		public void BallStateCreationRejectsNonPositiveMass(float mass)
		{
			var gameObject = new GameObject("Invalid ball mass");
			try {
				var component = gameObject.AddComponent<BallComponent>();
				component.Mass = mass;
				Assert.Throws<ArgumentOutOfRangeException>(() => component.CreateState());
			} finally {
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		private static BallState SolveFlipperContact(float mass)
		{
			// Align the contact arm with the normal and disable friction so this
			// regression isolates the ball surface-acceleration units.
			var info = new ColliderInfo {
				Id = 1,
				ItemId = 1,
				Material = new PhysicsMaterialData { Friction = 0f }
			};
			var flipper = new FlipperCollider(50f, 100f, 20f, 10f, 0f, 60f, info);
			var ball = CreateBall(mass, float3.zero);
			ball.Position = new float3(45f, 0f, 25f);
			var gravity = -ContactNormal;
			BallVelocityPhysics.UpdateVelocities(ref ball, gravity, float2.zero);
			var collEvent = new CollisionEventData {
				IsContact = true,
				HitNormal = ContactNormal,
				HitOrgNormalVelocity = math.dot(ball.Velocity, ContactNormal)
			};
			var movement = new FlipperMovementState();
			var staticData = new FlipperStaticData {
				Inertia = 1f,
				EndRadius = 10f,
				FlipperRadius = 100f
			};
			var velocityData = new FlipperVelocityData();

			flipper.Contact(ref ball, ref movement, in collEvent, in staticData,
				in velocityData, PhysicsConstants.PhysFactor, in gravity);
			return ball;
		}

		private static BallState CreateBall(float mass, in float3 velocity)
		{
			return new BallState {
				Id = 1,
				Position = new float3(0f, 0f, 25f),
				Velocity = velocity,
				Radius = 25f,
				Mass = mass
			};
		}

		private static void AssertFloat3(in float3 actual, in float3 expected)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-5f));
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-5f));
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-5f));
		}
	}
}
