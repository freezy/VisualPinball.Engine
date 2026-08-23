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

		[Test]
		public void InclineRollingIncludesRollingResistance()
		{
			const float angleDeg = 10f;
			const float initialSpeed = 2f;
			const int frames = 100;
			Incline(angleDeg, out var normal, out var tangent);
			var ball = CreateRollingBall(Mass, Radius, in normal, in tangent, initialSpeed,
				float3.zero);
			var zeroRollingBall = ball;
			var zeroRollingMaterial = Material;
			zeroRollingMaterial.RollingResistance = 0f;

			for (var frame = 0; frame < frames; frame++) {
				StepFrame(ref ball, in normal, in Gravity, in Material, float3.zero,
					PhysicsConstants.PhysFactor);
				StepFrame(ref zeroRollingBall, in normal, in Gravity, in zeroRollingMaterial,
					float3.zero, PhysicsConstants.PhysFactor);
			}

			var angle = math.radians(angleDeg);
			var elapsed = frames * PhysicsConstants.PhysFactor;
			var expectedSpeed = initialSpeed + 5f / 7f * GravityMagnitude
				* (math.sin(angle) - RollingResistance * math.cos(angle)) * elapsed;
			Assert.That(math.dot(ball.Velocity, tangent),
				Is.EqualTo(expectedSpeed).Within(expectedSpeed * 0.005f));
			var expectedRollingLoss = 5f / 7f * RollingResistance * GravityMagnitude
				* math.cos(angle) * elapsed;
			var measuredRollingLoss = math.dot(zeroRollingBall.Velocity - ball.Velocity, tangent);
			Assert.That(measuredRollingLoss,
				Is.EqualTo(expectedRollingLoss).Within(expectedRollingLoss * 0.005f));
			Assert.That(TangentialSlipSpeed(in ball, in normal, float3.zero),
				Is.LessThanOrEqualTo(RollingTolerance));
		}

		[Test]
		public void RollingResistanceDoesNotReverseMotion()
		{
			var oneStepDeceleration = 5f / 7f * RollingResistance * GravityMagnitude
				* PhysicsConstants.PhysFactor;
			var ball = CreateRollingBall(0.5f * oneStepDeceleration);

			StepFrame(ref ball, in Material);

			Assert.That(math.dot(ball.Velocity, Tangent), Is.EqualTo(0f).Within(1e-7f));
			Assert.That(math.dot(ball.AngularMomentum, math.cross(Normal, Tangent)),
				Is.EqualTo(0f).Within(1e-5f));
		}

		[Test]
		public void RollingResistanceDissipatesEnergy()
		{
			var ball = CreateRollingBall(0.02f);
			var previousEnergy = KineticEnergy(in ball);

			for (var frame = 0; frame < 30; frame++) {
				StepFrame(ref ball, in Material);
				var energy = KineticEnergy(in ball);
				Assert.That(energy, Is.LessThanOrEqualTo(previousEnergy + 1e-7f), $"frame {frame}");
				previousEnergy = energy;
			}

			Assert.That(previousEnergy, Is.EqualTo(0f).Within(1e-7f));
		}

		[Test]
		public void ZeroRollingResistanceIsNoOp()
		{
			const float angleDeg = 10f;
			Incline(angleDeg, out var normal, out var tangent);
			var material = Material;
			material.RollingResistance = 0f;
			var withRollingPath = CreateRollingBall(Mass, Radius, in normal, in tangent, 2f,
				float3.zero);
			var coulombOnly = withRollingPath;

			BallVelocityPhysics.UpdateVelocities(ref withRollingPath, Gravity, float2.zero);
			SolveContactAndRolling(ref withRollingPath, in normal, in Gravity, in material,
				float3.zero, PhysicsConstants.PhysFactor);
			BallVelocityPhysics.UpdateVelocities(ref coulombOnly, Gravity, float2.zero);
			SolveContact(ref coulombOnly, in normal, in Gravity, in material, float3.zero,
				PhysicsConstants.PhysFactor);

			AssertFloat3(withRollingPath.Velocity, coulombOnly.Velocity, 0f);
			AssertFloat3(withRollingPath.AngularMomentum, coulombOnly.AngularMomentum, 0f);
		}

		[Test]
		public void RollingResistanceDoesNotAffectVerticalImpact()
		{
			var zeroRolling = ImpactMaterial(0f);
			var nonzeroRolling = ImpactMaterial(0.2f);
			var velocity = new float3(0f, 0f, -10f);

			var baseline = SolveImpact(in zeroRolling, in velocity);
			var withRollingResistance = SolveImpact(in nonzeroRolling, in velocity);

			AssertFloat3(withRollingResistance.Velocity, baseline.Velocity, 0f);
			AssertFloat3(withRollingResistance.AngularMomentum, baseline.AngularMomentum, 0f);
		}

		[Test]
		public void RollingResistanceDoesNotAffectObliqueImpact()
		{
			var zeroRolling = ImpactMaterial(0f);
			var nonzeroRolling = ImpactMaterial(0.2f);
			var velocity = new float3(3f, 0f, -10f);

			var baseline = SolveImpact(in zeroRolling, in velocity);
			var withRollingResistance = SolveImpact(in nonzeroRolling, in velocity);

			AssertFloat3(withRollingResistance.Velocity, baseline.Velocity, 0f);
			AssertFloat3(withRollingResistance.AngularMomentum, baseline.AngularMomentum, 0f);
		}

		[Test]
		public void RollingResistanceUsesRelativeSurfaceMotion()
		{
			const float relativeSpeed = 3f;
			var colliderVelocity = 2f * Tangent;
			var staticSurface = CreateRollingBall(relativeSpeed);
			var movingSurface = CreateRollingBall(Mass, Radius, in Normal, in Tangent,
				relativeSpeed, in colliderVelocity);

			StepFrame(ref staticSurface, in Material);
			StepFrame(ref movingSurface, in Normal, in Gravity, in Material,
				in colliderVelocity, PhysicsConstants.PhysFactor);

			AssertFloat3(movingSurface.Velocity - colliderVelocity, staticSurface.Velocity);
			AssertFloat3(movingSurface.AngularMomentum, staticSurface.AngularMomentum);
		}

		[Test]
		public void RollingResistanceIsMassInvariant()
		{
			const float initialSpeed = 3f;
			const int frames = 100;
			var reference = CreateRollingBall(initialSpeed);
			for (var frame = 0; frame < frames; frame++) {
				StepFrame(ref reference, in Material);
			}

			foreach (var mass in new[] { 0.5f, 2f, 5f }) {
				var ball = CreateRollingBall(mass, Radius, in Normal, in Tangent, initialSpeed,
					float3.zero);
				for (var frame = 0; frame < frames; frame++) {
					StepFrame(ref ball, in Material);
				}
				Assert.That(math.dot(ball.Velocity, Tangent),
					Is.EqualTo(math.dot(reference.Velocity, Tangent)).Within(1e-5f), $"mass {mass}");
			}
		}

		[Test]
		public void RollingResistanceHasExpectedRadiusScaling()
		{
			const float initialSpeed = 3f;
			const int frames = 100;
			var reference = CreateRollingBall(initialSpeed);
			for (var frame = 0; frame < frames; frame++) {
				StepFrame(ref reference, in Material);
			}

			foreach (var radius in new[] { 10f, 50f }) {
				var ball = CreateRollingBall(Mass, radius, in Normal, in Tangent, initialSpeed,
					float3.zero);
				for (var frame = 0; frame < frames; frame++) {
					StepFrame(ref ball, in Material);
				}
				var rollingAxis = math.cross(Normal, Tangent);
				var angularSpeed = math.dot(ball.AngularMomentum / ball.Inertia, rollingAxis);
				Assert.That(math.dot(ball.Velocity, Tangent),
					Is.EqualTo(math.dot(reference.Velocity, Tangent)).Within(1e-5f),
					$"radius {radius}");
				Assert.That(angularSpeed * radius,
					Is.EqualTo(math.dot(ball.Velocity, Tangent)).Within(1e-5f),
					$"radius {radius}");
			}
		}

		[Test]
		public void RollingResistanceIsSubstepInvariant()
		{
			var fullStep = CreateRollingBall(3f);
			var splitStep = fullStep;

			BallVelocityPhysics.UpdateVelocities(ref fullStep, Gravity, float2.zero);
			SolveContactAndRolling(ref fullStep, in Normal, in Gravity, in Material,
				float3.zero, PhysicsConstants.PhysFactor);
			BallVelocityPhysics.UpdateVelocities(ref splitStep, Gravity, float2.zero);
			SolveContactAndRolling(ref splitStep, in Normal, in Gravity, in Material,
				float3.zero, PhysicsConstants.PhysFactor * 0.5f);
			SolveContactAndRolling(ref splitStep, in Normal, in Gravity, in Material,
				float3.zero, PhysicsConstants.PhysFactor * 0.5f);

			AssertFloat3(splitStep.Velocity, fullStep.Velocity);
			AssertFloat3(splitStep.AngularMomentum, fullStep.AngularMomentum);
		}

		private static void StepFrame(ref BallState ball, in PhysicsMaterialData material)
		{
			StepFrame(ref ball, in Normal, in Gravity, in material, float3.zero,
				PhysicsConstants.PhysFactor);
		}

		private static void StepFrame(ref BallState ball, in float3 normal, in float3 gravity,
			in PhysicsMaterialData material, in float3 colliderVelocity, float contactTime)
		{
			BallVelocityPhysics.UpdateVelocities(ref ball, gravity, float2.zero);
			SolveContactAndRolling(ref ball, in normal, in gravity, in material,
				in colliderVelocity, contactTime);
		}

		private static void SolveContactAndRolling(ref BallState ball, in float3 normal,
			in float3 gravity, in PhysicsMaterialData material, in float3 colliderVelocity,
			float contactTime)
		{
			var supportImpulse = SolveContact(ref ball, in normal, in gravity, in material,
				in colliderVelocity, contactTime);
			var rollingContact = CreateContact(material.RollingResistance, supportImpulse, in normal);
			rollingContact.ColliderVelocity = colliderVelocity;
			BallCollider.ApplyRollingResistance(ref ball, in rollingContact);
		}

		private static float SolveContact(ref BallState ball, in float3 normal, in float3 gravity,
			in PhysicsMaterialData material, in float3 colliderVelocity, float contactTime)
		{
			var collEvent = new CollisionEventData {
				IsContact = true,
				HitNormal = normal,
				HitOrgNormalVelocity = math.dot(ball.Velocity - colliderVelocity, normal),
			};
			return BallCollider.HandleStaticContact(ref ball, in collEvent, in material,
				contactTime, in gravity, in colliderVelocity);
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
			return CreateRollingBall(Mass, Radius, in Normal, in Tangent, speed, float3.zero);
		}

		private static BallState CreateRollingBall(float mass, float radius, in float3 normal,
			in float3 tangent, float relativeSpeed, in float3 colliderVelocity)
		{
			var ball = new BallState {
				Id = 1,
				Position = radius * normal,
				Velocity = colliderVelocity + relativeSpeed * tangent,
				Radius = radius,
				Mass = mass,
			};
			var rollingAxis = math.normalize(math.cross(normal, tangent));
			ball.AngularMomentum = ball.Inertia * relativeSpeed / radius * rollingAxis;
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
			return TangentialSlipSpeed(in ball, in Normal, float3.zero);
		}

		private static float TangentialSlipSpeed(in BallState ball, in float3 normal,
			in float3 colliderVelocity)
		{
			var contactPoint = -ball.Radius * normal;
			var surfaceVelocity = BallState.SurfaceVelocity(in ball, in contactPoint)
				- colliderVelocity;
			var tangentialSlip = surfaceVelocity - normal * math.dot(surfaceVelocity, normal);
			return math.length(tangentialSlip);
		}

		private static float KineticEnergy(in BallState ball)
		{
			var angularVelocity = ball.AngularMomentum / ball.Inertia;
			return 0.5f * ball.Mass * math.lengthsq(ball.Velocity)
			       + 0.5f * ball.Inertia * math.lengthsq(angularVelocity);
		}

		private static PhysicsMaterialData ImpactMaterial(float rollingResistance)
		{
			return new PhysicsMaterialData {
				Elasticity = 0.8f,
				Friction = 0.2f,
				RollingResistance = rollingResistance,
			};
		}

		private static BallState SolveImpact(in PhysicsMaterialData material, in float3 velocity)
		{
			var ball = new BallState {
				Id = 1,
				Position = Radius * Normal,
				Velocity = velocity,
				Radius = Radius,
				Mass = Mass,
			};
			var collEvent = new CollisionEventData();
			var state = new PhysicsState();
			BallCollider.Collide3DWall(ref ball, in material, in collEvent, in Normal, ref state);
			return ball;
		}

		private static void Incline(float angleDeg, out float3 normal, out float3 downhillTangent)
		{
			var angle = math.radians(angleDeg);
			normal = new float3(math.sin(angle), 0f, math.cos(angle));
			downhillTangent = new float3(math.cos(angle), 0f, -math.sin(angle));
		}

		private static void AssertFloat3(in float3 actual, in float3 expected,
			float tolerance = 1e-6f)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance));
		}
	}
}
