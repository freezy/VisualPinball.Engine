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
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Test
{
	public class BallFrictionTests
	{
		private const float Friction = 0.2f;
		private const float GravityMagnitude = 1f;
		private const float Radius = 25f;
		private const float Mass = 1f;
		private const float RollingTolerance = 1e-4f;

		private static readonly float3 Gravity = new(0f, 0f, -GravityMagnitude);
		private static readonly float3 LevelNormal = new(0f, 0f, 1f);
		private static readonly float3 LevelTangent = new(1f, 0f, 0f);
		private static readonly PhysicsMaterialData Material = new() { Friction = Friction };

		[Test]
		public void SlidingSphereReachesAnalyticRollingState()
		{
			const float initialSpeed = 4f;
			var ball = CreateBall(Mass, Radius, initialSpeed * LevelTangent);
			var expectedTransitionTime = 2f * initialSpeed / (7f * Friction * GravityMagnitude);
			var transitionTime = float.NaN;

			for (var frame = 0; frame < 100; frame++) {
				StepFrame(ref ball, in LevelNormal, in Gravity, PhysicsConstants.PhysFactor);
				if (TangentialSlipSpeed(in ball, in LevelNormal) <= RollingTolerance) {
					transitionTime = (frame + 1) * PhysicsConstants.PhysFactor;
					break;
				}
			}

			Assert.That(transitionTime, Is.Not.NaN, "the sliding ball never reached rolling contact");
			Assert.That(transitionTime, Is.EqualTo(expectedTransitionTime).Within(PhysicsConstants.PhysFactor));
			Assert.That(math.dot(ball.Velocity, LevelTangent), Is.EqualTo(5f / 7f * initialSpeed).Within(1e-4f));
			Assert.That(math.dot(ball.Velocity, LevelNormal), Is.EqualTo(0f).Within(1e-5f));
		}

		[Test]
		public void PureRollingSphereDoesNotDecayWhenCrrIsZero()
		{
			const float initialSpeed = 3f;
			var ball = CreateRollingBall(Mass, Radius, in LevelNormal, in LevelTangent, initialSpeed);
			var initialVelocity = ball.Velocity;
			var initialAngularMomentum = ball.AngularMomentum;

			for (var frame = 0; frame < 100; frame++) {
				StepFrame(ref ball, in LevelNormal, in Gravity, PhysicsConstants.PhysFactor);
			}

			Assert.That(math.dot(ball.Velocity, LevelTangent),
				Is.EqualTo(math.dot(initialVelocity, LevelTangent)).Within(1e-5f));
			Assert.That(math.dot(ball.Velocity, LevelNormal), Is.EqualTo(0f).Within(1e-5f),
				"sustained contact must not create normal velocity");
			AssertFloat3(ball.AngularMomentum, initialAngularMomentum, 1e-5f);
			Assert.That(TangentialSlipSpeed(in ball, in LevelNormal), Is.LessThanOrEqualTo(RollingTolerance));
		}

		[Test]
		public void PureRollingSphereHasAnalyticInclineAcceleration()
		{
			const float angleDeg = 10f;
			const float initialSpeed = 2f;
			const int frames = 100;
			Incline(angleDeg, out var normal, out var tangent);
			var ball = CreateRollingBall(Mass, Radius, in normal, in tangent, initialSpeed);

			SimulateFrames(ref ball, in normal, in Gravity, frames);

			var elapsed = frames * PhysicsConstants.PhysFactor;
			var expectedSpeed = initialSpeed + 5f / 7f * GravityMagnitude
				* math.sin(math.radians(angleDeg)) * elapsed;
			Assert.That(math.dot(ball.Velocity, tangent), Is.EqualTo(expectedSpeed).Within(expectedSpeed * 0.005f));
			Assert.That(math.dot(ball.Velocity, normal), Is.EqualTo(0f).Within(1e-5f));
			Assert.That(TangentialSlipSpeed(in ball, in normal), Is.LessThanOrEqualTo(RollingTolerance));
		}

		[Test]
		public void ContactResultIsMassInvariant()
		{
			const float gravityMagnitude = 0.2f;
			const float angleDeg = 20f;
			const int frames = 500;
			var gravity = new float3(0f, 0f, -gravityMagnitude);
			Incline(angleDeg, out var normal, out var tangent);
			var reference = SimulateIncline(Mass, Radius, in normal, in tangent, in gravity, frames);
			var slidingReference = SimulateSliding(Mass, Radius, in gravity, frames);

			foreach (var mass in new[] { 0.5f, 2f, 5f }) {
				var result = SimulateIncline(mass, Radius, in normal, in tangent, in gravity, frames);
				var slidingResult = SimulateSliding(mass, Radius, in gravity, frames);
				Assert.That(math.dot(result.Velocity, tangent),
					Is.EqualTo(math.dot(reference.Velocity, tangent)).Within(1e-4f), $"mass {mass}");
				Assert.That(math.dot(result.AngularMomentum / result.Inertia, math.cross(normal, tangent)),
					Is.EqualTo(math.dot(reference.AngularMomentum / reference.Inertia, math.cross(normal, tangent)))
						.Within(1e-4f), $"mass {mass}");
				Assert.That(math.dot(slidingResult.Velocity, LevelTangent),
					Is.EqualTo(math.dot(slidingReference.Velocity, LevelTangent)).Within(1e-4f),
					$"sliding mass {mass}");
				Assert.That(math.dot(slidingResult.AngularMomentum / slidingResult.Inertia, new float3(0f, 1f, 0f)),
					Is.EqualTo(math.dot(slidingReference.AngularMomentum / slidingReference.Inertia,
						new float3(0f, 1f, 0f))).Within(1e-4f), $"sliding mass {mass}");
			}
		}

		[Test]
		public void ContactResultHasExpectedRadiusScaling()
		{
			const float angleDeg = 10f;
			const float initialSpeed = 2f;
			const int frames = 100;
			Incline(angleDeg, out var normal, out var tangent);
			var reference = SimulateIncline(Mass, Radius, in normal, in tangent, in Gravity, frames, initialSpeed);
			var referenceSpeed = math.dot(reference.Velocity, tangent);

			foreach (var radius in new[] { 10f, 50f }) {
				var result = SimulateIncline(Mass, radius, in normal, in tangent, in Gravity, frames, initialSpeed);
				var rollingAxis = math.cross(normal, tangent);
				var angularSpeed = math.dot(result.AngularMomentum / result.Inertia, rollingAxis);
				Assert.That(math.dot(result.Velocity, tangent), Is.EqualTo(referenceSpeed).Within(1e-4f), $"radius {radius}");
				Assert.That(angularSpeed * radius, Is.EqualTo(referenceSpeed).Within(1e-4f), $"radius {radius}");
			}
		}

		[Test]
		public void ContactResultIsSubstepInvariant()
		{
			var fullStep = CreateBall(Mass, Radius, 4f * LevelTangent);
			var splitStep = fullStep;

			BallVelocityPhysics.UpdateVelocities(ref fullStep, Gravity, float2.zero);
			SolveContact(ref fullStep, in LevelNormal, in Gravity, PhysicsConstants.PhysFactor);

			BallVelocityPhysics.UpdateVelocities(ref splitStep, Gravity, float2.zero);
			SolveContact(ref splitStep, in LevelNormal, in Gravity, PhysicsConstants.PhysFactor * 0.5f);
			SolveContact(ref splitStep, in LevelNormal, in Gravity, PhysicsConstants.PhysFactor * 0.5f);

			// Include the normal channel: a second contact slice must not apply the
			// original approach correction again.
			AssertFloat3(splitStep.Velocity, fullStep.Velocity, 1e-5f);
			AssertFloat3(splitStep.AngularMomentum, fullStep.AngularMomentum, 1e-5f);

			Incline(10f, out var normal, out var tangent);
			fullStep = CreateRollingBall(Mass, Radius, in normal, in tangent, 2f);
			splitStep = fullStep;

			BallVelocityPhysics.UpdateVelocities(ref fullStep, Gravity, float2.zero);
			SolveContact(ref fullStep, in normal, in Gravity, PhysicsConstants.PhysFactor);

			BallVelocityPhysics.UpdateVelocities(ref splitStep, Gravity, float2.zero);
			SolveContact(ref splitStep, in normal, in Gravity, PhysicsConstants.PhysFactor * 0.5f);
			SolveContact(ref splitStep, in normal, in Gravity, PhysicsConstants.PhysFactor * 0.5f);

			AssertFloat3(splitStep.Velocity, fullStep.Velocity, 1e-5f);
			AssertFloat3(splitStep.AngularMomentum, fullStep.AngularMomentum, 1e-5f);
		}

		[Test]
		public void SlipClassificationDoesNotDependOnNormalPreload()
		{
			var gravity = new float3(0f, 0f, -0.2f);
			var lowPreload = CreateBall(Mass, Radius, 4f * LevelTangent + 0.02f * LevelNormal);
			var highPreload = CreateBall(Mass, Radius, 4f * LevelTangent + 0.03f * LevelNormal);

			SolveContact(ref lowPreload, in LevelNormal, in gravity, PhysicsConstants.PhysFactor);
			SolveContact(ref highPreload, in LevelNormal, in gravity, PhysicsConstants.PhysFactor);

			var lowTangentialSpeed = math.dot(lowPreload.Velocity, LevelTangent);
			var highTangentialSpeed = math.dot(highPreload.Velocity, LevelTangent);
			Assert.That(lowTangentialSpeed, Is.LessThan(4f));
			Assert.That(highTangentialSpeed, Is.LessThan(4f));
			Assert.That(lowTangentialSpeed, Is.EqualTo(highTangentialSpeed).Within(1e-5f));
		}

		[TestCase(0f)]
		[TestCase(10f)]
		public void RestingContactDoesNotGainEnergy(float angleDeg)
		{
			Incline(angleDeg, out var normal, out _);
			var ball = CreateBall(Mass, Radius, float3.zero);

			BallVelocityPhysics.UpdateVelocities(ref ball, Gravity, float2.zero);
			var energyAfterExternalAcceleration = KineticEnergy(in ball);
			SolveContact(ref ball, in normal, in Gravity, PhysicsConstants.PhysFactor);

			Assert.That(KineticEnergy(in ball), Is.LessThanOrEqualTo(energyAfterExternalAcceleration + 1e-6f));
		}

		private static BallState SimulateIncline(float mass, float radius, in float3 normal, in float3 tangent,
			in float3 gravity, int frames, float initialSpeed = 0f)
		{
			var ball = CreateRollingBall(mass, radius, in normal, in tangent, initialSpeed);
			SimulateFrames(ref ball, in normal, in gravity, frames);
			return ball;
		}

		private static BallState SimulateSliding(float mass, float radius, in float3 gravity, int frames)
		{
			var ball = CreateBall(mass, radius, 4f * LevelTangent);
			SimulateFrames(ref ball, in LevelNormal, in gravity, frames);
			return ball;
		}

		private static void SimulateFrames(ref BallState ball, in float3 normal, in float3 gravity, int frames)
		{
			for (var frame = 0; frame < frames; frame++) {
				StepFrame(ref ball, in normal, in gravity, PhysicsConstants.PhysFactor);
			}
		}

		private static void StepFrame(ref BallState ball, in float3 normal, in float3 gravity, float contactTime)
		{
			BallVelocityPhysics.UpdateVelocities(ref ball, gravity, float2.zero);
			SolveContact(ref ball, in normal, in gravity, contactTime);
		}

		private static void SolveContact(ref BallState ball, in float3 normal, in float3 gravity, float contactTime)
		{
			var collEvent = new CollisionEventData {
				IsContact = true,
				HitNormal = normal,
				HitOrgNormalVelocity = math.dot(ball.Velocity, normal)
			};
			BallCollider.HandleStaticContact(ref ball, in collEvent, in Material, contactTime,
				in gravity, float3.zero);
		}

		private static BallState CreateBall(float mass, float radius, in float3 velocity)
		{
			return new BallState {
				Id = 1,
				Position = radius * LevelNormal,
				Velocity = velocity,
				Radius = radius,
				Mass = mass
			};
		}

		private static BallState CreateRollingBall(float mass, float radius, in float3 normal, in float3 tangent,
			float speed)
		{
			var ball = CreateBall(mass, radius, speed * tangent);
			var rollingAxis = math.normalize(math.cross(normal, tangent));
			ball.AngularMomentum = ball.Inertia * speed / radius * rollingAxis;
			return ball;
		}

		private static float TangentialSlipSpeed(in BallState ball, in float3 normal)
		{
			var surfacePoint = -ball.Radius * normal;
			var surfaceVelocity = BallState.SurfaceVelocity(in ball, in surfacePoint);
			var tangentialSlip = surfaceVelocity - normal * math.dot(surfaceVelocity, normal);
			return math.length(tangentialSlip);
		}

		private static float KineticEnergy(in BallState ball)
		{
			var angularSpeedSq = math.lengthsq(ball.AngularMomentum / ball.Inertia);
			return 0.5f * ball.Mass * math.lengthsq(ball.Velocity)
			       + 0.5f * ball.Inertia * angularSpeedSq;
		}

		private static void Incline(float angleDeg, out float3 normal, out float3 downhillTangent)
		{
			var angle = math.radians(angleDeg);
			normal = new float3(math.sin(angle), 0f, math.cos(angle));
			downhillTangent = new float3(math.cos(angle), 0f, -math.sin(angle));
		}

		private static void AssertFloat3(in float3 actual, in float3 expected, float tolerance)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance));
		}
	}
}
