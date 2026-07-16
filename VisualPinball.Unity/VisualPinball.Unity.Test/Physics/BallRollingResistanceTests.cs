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

using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Test
{
	public class BallRollingResistanceTests
	{
		private const float GravityMagnitude = 1f;
		private const float Mass = 1f;
		private const float Radius = 25f;
		private const float RollingResistance = 0.02f;
		private const float RollingTolerance = 1e-4f;

		private static readonly float3 Gravity = new(0f, 0f, -GravityMagnitude);
		private static readonly float3 Normal = new(0f, 0f, 1f);
		private static readonly float3 Tangent = new(1f, 0f, 0f);
		private static readonly PhysicsMaterialData Material = new() {
			Friction = 0.2f,
			RollingResistance = RollingResistance,
		};

		[Test]
		public void LevelRollingDeceleratesAnalytically()
		{
			const float initialSpeed = 3f;
			const int frames = 100;
			var ball = CreateRollingBall(initialSpeed);

			for (var frame = 0; frame < frames; frame++) {
				StepFrame(ref ball, in Material);
			}

			var elapsed = frames * PhysicsConstants.PhysFactor;
			var expectedSpeed = initialSpeed
				- 5f / 7f * RollingResistance * GravityMagnitude * elapsed;
			Assert.That(math.dot(ball.Velocity, Tangent),
				Is.EqualTo(expectedSpeed).Within(expectedSpeed * 0.005f));
		}

		[Test]
		public void RollingResistancePreservesNoSlip()
		{
			var ball = CreateRollingBall(3f);

			for (var frame = 0; frame < 100; frame++) {
				StepFrame(ref ball, in Material);
				Assert.That(TangentialSlipSpeed(in ball), Is.LessThanOrEqualTo(RollingTolerance));
			}
		}

		[Test]
		public void CoplanarSeamDoesNotMultiplyRollingResistance()
		{
			var oneContact = CreateRollingBall(3f);
			var duplicateContact = oneContact;
			var candidate = CreateContact(RollingResistance, SupportImpulse(), in Normal);

			ApplySelectedContact(ref oneContact, candidate);
			ApplySelectedContact(ref duplicateContact, candidate, candidate);

			AssertFloat3(duplicateContact.Velocity, oneContact.Velocity);
			AssertFloat3(duplicateContact.AngularMomentum, oneContact.AngularMomentum);
		}

		[Test]
		public void CornerSelectsLargestRollingImpulseLimit()
		{
			var cornerNormal = new float3(0f, 1f, 0f);
			var ball = CreateCornerRollingBall(3f, in cornerNormal);
			var weaker = CreateContact(0.01f, 0.02f, in Normal);
			var stronger = CreateContact(0.02f, 0.02f, in cornerNormal);

			var selected = SelectContact(in ball, weaker, stronger);
			var selectedInReverseOrder = SelectContact(in ball, stronger, weaker);

			Assert.That(selected.RollingResistance, Is.EqualTo(stronger.RollingResistance));
			Assert.That(selected.SupportImpulse, Is.EqualTo(stronger.SupportImpulse));
			AssertFloat3(selected.ContactNormal, stronger.ContactNormal);
			AssertFloat3(selectedInReverseOrder.ContactNormal, stronger.ContactNormal);
		}

		[Test]
		public void MixedMaterialContactsSelectLargestRollingImpulseLimit()
		{
			var ball = CreateRollingBall(3f);
			var highCoefficient = CreateContact(0.04f, 0.01f, in Normal);
			var highSupport = CreateContact(0.02f, 0.03f, in Normal);

			var selected = SelectContact(in ball, highCoefficient, highSupport);
			var selectedInReverseOrder = SelectContact(in ball, highSupport, highCoefficient);

			Assert.That(selected.RollingResistance, Is.EqualTo(highSupport.RollingResistance));
			Assert.That(selected.SupportImpulse, Is.EqualTo(highSupport.SupportImpulse));
			Assert.That(selectedInReverseOrder.RollingResistance,
				Is.EqualTo(highSupport.RollingResistance));
		}

		[Test]
		public void IneligibleContactDoesNotShadowRollingSupport()
		{
			const float speed = 3f;
			var staticOnly = CreateRollingBall(speed);
			var mixedContacts = staticOnly;
			var staticContact = CreateContact(RollingResistance, SupportImpulse(), in Normal);
			var movingContact = staticContact;
			movingContact.ColliderVelocity = speed * Tangent;

			ApplySelectedContact(ref staticOnly, staticContact);
			ApplySelectedContact(ref mixedContacts, staticContact, movingContact);

			AssertFloat3(mixedContacts.Velocity, staticOnly.Velocity);
			AssertFloat3(mixedContacts.AngularMomentum, staticOnly.AngularMomentum);
		}

		private static void StepFrame(ref BallState ball, in PhysicsMaterialData material)
		{
			BallVelocityPhysics.UpdateVelocities(ref ball, Gravity, float2.zero);
			var collEvent = new CollisionEventData {
				IsContact = true,
				HitNormal = Normal,
				HitOrgNormalVelocity = math.dot(ball.Velocity, Normal),
			};
			var supportImpulse = BallCollider.HandleStaticContact(ref ball, in collEvent, in material,
				PhysicsConstants.PhysFactor, in Gravity, float3.zero);
			var rollingContact = CreateContact(material.RollingResistance, supportImpulse, in Normal);
			BallCollider.ApplyRollingResistance(ref ball, in rollingContact);
		}

		private static void ApplySelectedContact(ref BallState ball,
			params RollingContactData[] rollingContacts)
		{
			var selected = SelectContact(in ball, rollingContacts);
			BallCollider.ApplyRollingResistance(ref ball, in selected);
		}

		private static RollingContactData SelectContact(in BallState ball,
			params RollingContactData[] rollingContacts)
		{
			using var contacts = new NativeList<ContactBufferElement>(Allocator.Temp);
			foreach (var rollingContact in rollingContacts) {
				contacts.Add(new ContactBufferElement(1, default) {
					RollingContact = rollingContact,
				});
			}
			return ContactPhysics.SelectRollingContact(in contacts, 0, contacts.Length, in ball);
		}

		private static RollingContactData CreateContact(float rollingResistance, float supportImpulse,
			in float3 normal)
		{
			return new RollingContactData {
				IsContact = true,
				RollingResistance = rollingResistance,
				SupportImpulse = supportImpulse,
				ContactNormal = normal,
				ColliderVelocity = float3.zero,
			};
		}

		private static BallState CreateRollingBall(float speed)
		{
			var ball = new BallState {
				Id = 1,
				Position = Radius * Normal,
				Velocity = speed * Tangent,
				Radius = Radius,
				Mass = Mass,
			};
			var rollingAxis = math.normalize(math.cross(Normal, Tangent));
			ball.AngularMomentum = ball.Inertia * speed / Radius * rollingAxis;
			return ball;
		}

		private static BallState CreateCornerRollingBall(float speed, in float3 cornerNormal)
		{
			var ball = CreateRollingBall(speed);
			var floorAxis = math.normalize(math.cross(Normal, Tangent));
			var cornerAxis = math.normalize(math.cross(cornerNormal, Tangent));
			ball.AngularMomentum = ball.Inertia * speed / Radius * (floorAxis + cornerAxis);
			return ball;
		}

		private static float SupportImpulse()
			=> Mass * GravityMagnitude * PhysicsConstants.PhysFactor;

		private static float TangentialSlipSpeed(in BallState ball)
		{
			var contactPoint = -ball.Radius * Normal;
			var surfaceVelocity = BallState.SurfaceVelocity(in ball, in contactPoint);
			var tangentialSlip = surfaceVelocity - Normal * math.dot(surfaceVelocity, Normal);
			return math.length(tangentialSlip);
		}

		private static void AssertFloat3(in float3 actual, in float3 expected)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-6f));
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-6f));
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-6f));
		}
	}
}
