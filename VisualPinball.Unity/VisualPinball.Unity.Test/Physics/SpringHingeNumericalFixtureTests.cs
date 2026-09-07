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
	public class SpringHingeNumericalFixtureTests
	{
		[Test]
		public void UnitContractUsesBallRelativeVpuAndNormalizedTime()
		{
			const float ballMass = 1f;
			const float armVpu = 50f;
			var pointMassInertia = ballMass * armVpu * armVpu;
			var accelerationScale = PhysicsConstants.MToVpu
				* PhysicsConstants.DefaultStepTimeS * PhysicsConstants.DefaultStepTimeS;

			Assert.That(PhysicsConstants.PhysicsStepTimeS, Is.EqualTo(0.001d).Within(1e-12d));
			Assert.That(PhysicsConstants.PhysFactor, Is.EqualTo(0.1f).Within(1e-7f));
			Assert.That(pointMassInertia, Is.EqualTo(2500f));
			Assert.That(PhysicsConstants.Ms2ToVpuVpt2, Is.EqualTo(accelerationScale).Within(1e-7f));
			Assert.That(new float3(0f, -accelerationScale, 0f).y, Is.LessThan(0f),
				"positive cabinet acceleration produces the opposite inertial acceleration");
		}

		[Test]
		public void ImplicitSpringPeriodAndDampingMatchIndependentReferences()
		{
			const float inertia = 2500f;
			const float stiffness = 100f;
			var expectedPeriod = 2f * math.PI * math.sqrt(inertia / stiffness);
			var coarse = MeasureOscillator(0.1f, inertia, stiffness);
			var fine = MeasureOscillator(0.05f, inertia, stiffness);
			var coarseError = math.abs(coarse.Period - expectedPeriod);
			var fineError = math.abs(fine.Period - expectedPeriod);
			var naturalFrequency = math.sqrt(stiffness / inertia);
			var expectedCycleDecay = math.exp(-math.PI * 0.1f * naturalFrequency);

			Assert.That(coarseError / expectedPeriod, Is.LessThan(0.0002f));
			Assert.That(coarseError / fineError, Is.GreaterThan(3f));
			Assert.That(coarse.AmplitudeRatio, Is.EqualTo(expectedCycleDecay).Within(0.002f));
			Assert.That(0.1f * naturalFrequency / 2f, Is.EqualTo(0.01f).Within(1e-6f),
				"implicit Euler contributes approximately h*omegaN/2 damping ratio");
		}

		[Test]
		public void ReciprocalImpactPinsRestitutionAndAngularMomentum()
		{
			var input = CreateImpactInput();
			var arm = input.Witness - input.Pivot;
			var velocityBefore = math.dot(input.BallVelocity
				- input.HingeAngularVelocity * math.cross(input.Axis, arm), input.Normal);
			var momentumBefore = ProjectedAngularMomentum(1f / input.BallInverseMass, input.BallVelocity,
				input.Axis, input.Pivot, input.Witness, input.HingeInertia, input.HingeAngularVelocity);

			var result = SpringHingeNumericalFixtures.SolveImpact(input);
			var velocityAfter = math.dot(result.BallVelocity
				- result.HingeAngularVelocity * math.cross(input.Axis, arm), input.Normal);
			var momentumAfter = ProjectedAngularMomentum(1f / input.BallInverseMass, result.BallVelocity,
				input.Axis, input.Pivot, input.Witness, input.HingeInertia, result.HingeAngularVelocity);

			Assert.That(result.Impulse, Is.GreaterThan(0f));
			Assert.That(velocityAfter, Is.EqualTo(-input.Restitution * velocityBefore).Within(1e-5f));
			Assert.That(momentumAfter, Is.EqualTo(momentumBefore).Within(math.abs(momentumBefore) * 0.001f));
		}

		[Test]
		public void LocalHoldSatisfiesBothCoupledEquations()
		{
			var input = CreateCoupledHoldInput();
			var result = SpringHingeNumericalFixtures.SolveHold(input);
			var relativeVelocity = result.BallVelocity - input.ArmJacobian * result.HingeAngularVelocity;
			var expectedImpulse = -input.Step * input.HoldStiffness
				* (input.PositionError + input.Step * relativeVelocity)
				- input.Step * input.HoldDamping * relativeVelocity;
			var hingeResidual = input.HingeInertia * (result.HingeAngularVelocity - input.HingeAngularVelocity)
				- (input.Step * input.ExternalTorque + input.OtherAngularImpulse
					- input.Step * input.HingeStiffness * (input.AngleError + input.Step * result.HingeAngularVelocity)
					- input.Step * input.HingeDamping * result.HingeAngularVelocity
					- math.dot(input.ArmJacobian, result.Impulse));
			var momentumBefore = input.HingeInertia * input.HingeAngularVelocity
				+ math.dot(input.ArmJacobian, input.BallMass * input.BallVelocity);
			var momentumAfter = input.HingeInertia * result.HingeAngularVelocity
				+ math.dot(input.ArmJacobian, input.BallMass * result.BallVelocity);
			var externalAngularImpulse = input.Step * input.ExternalTorque + input.OtherAngularImpulse
				- input.Step * input.HingeStiffness * (input.AngleError + input.Step * result.HingeAngularVelocity)
				- input.Step * input.HingeDamping * result.HingeAngularVelocity;

			Assert.That(result.IsCapped, Is.False);
			Assert.That(math.length(result.Impulse - expectedImpulse), Is.LessThan(2e-5f));
			Assert.That(math.abs(hingeResidual), Is.LessThan(2e-5f));
			Assert.That(momentumAfter, Is.EqualTo(momentumBefore + externalAngularImpulse).Within(2e-5f));
		}

		[TestCase(0f, 0f)]
		[TestCase(80f, 0f)]
		public void ZeroHoldDoesNotDoubleCommitFreeHingeStep(float holdStiffness, float maximumForce)
		{
			var input = CreateCoupledHoldInput();
			input.HoldStiffness = holdStiffness;
			input.HoldDamping = 0f;
			input.MaximumHoldForce = maximumForce;
			var expected = SpringHingeNumericalFixtures.StepUnconstrained(CreateFreeInput(input));

			var result = SpringHingeNumericalFixtures.SolveHold(input);

			Assert.That(result.Impulse, Is.EqualTo(float3.zero));
			Assert.That(result.HingeAngularVelocity,
				Is.EqualTo(expected.AngularVelocity).Within(1e-6f));
		}

		[Test]
		public void LocalHoldUsesOneVectorCapAndPublishesFullImpulseConversions()
		{
			var input = CreateCoupledHoldInput();
			input.MaximumHoldForce = 100f;
			var result = SpringHingeNumericalFixtures.SolveHold(input);
			var cap = input.MaximumHoldForce * input.Step;

			Assert.That(result.IsCapped, Is.True);
			Assert.That(math.length(result.Impulse), Is.EqualTo(cap).Within(1e-5f));
			Assert.That(result.ConstitutiveResidual / cap, Is.LessThan(10f),
				"single projection is qualified only while the constitutive residual is below ten caps");
			Assert.That(result.ExternalAcceleration,
				Is.EqualTo(result.Impulse / (input.BallMass * input.Step)));
			Assert.That(result.CommittedMagneticTorque,
				Is.EqualTo((input.OtherAngularImpulse - math.dot(input.ArmJacobian, result.Impulse)) / input.Step).Within(1e-5f));
		}

		[TestCase(0.02f)]
		[TestCase(20000f)]
		public void HoldRemainsFiniteForVeryLightAndHeavyHinges(float inertia)
		{
			var input = CreateCoupledHoldInput();
			input.HingeInertia = inertia;
			input.HoldDamping = 40f;
			var result = SpringHingeNumericalFixtures.SolveHold(input);

			Assert.That(math.all(math.isfinite(result.BallVelocity)), Is.True);
			Assert.That(math.isfinite(result.HingeAngularVelocity), Is.True);
		}

		[Test]
		public void DegenerateHoldFallsBackToFreeHingeStep()
		{
			var input = CreateCoupledHoldInput();
			input.BallMass = 0f;
			var expected = SpringHingeNumericalFixtures.StepUnconstrained(CreateFreeInput(input));

			var result = SpringHingeNumericalFixtures.SolveHold(input);

			Assert.That(result.Impulse, Is.EqualTo(float3.zero));
			Assert.That(result.HingeAngularVelocity, Is.EqualTo(expected.AngularVelocity));
		}

		[Test]
		public void StopHoldUsesOnlyAUnilateralBearingReaction()
		{
			var input = CreateCoupledHoldInput();
			input.HingeAngularVelocity = 0f;
			input.ActiveStop = 1;
			input.ExternalTorque = 25f;
			input.PositionError = float3.zero;
			input.BallVelocity = float3.zero;
			var held = SpringHingeNumericalFixtures.SolveHold(input);
			Assert.That(held.IsStopHeld, Is.True);
			Assert.That(held.HingeAngularVelocity, Is.Zero);
			Assert.That(held.BearingImpulse, Is.LessThanOrEqualTo(0f));

			input.ExternalTorque = -25f;
			var leaving = SpringHingeNumericalFixtures.SolveHold(input);
			Assert.That(leaving.IsStopHeld, Is.False);
			Assert.That(leaving.HingeAngularVelocity, Is.LessThan(0f));
		}

		[Test]
		public void SupportLagEnvelopeIsBoundedAtQualifiedHoldStep()
		{
			var input = CreateCoupledHoldInput();
			input.PositionError = float3.zero;
			input.ArmJacobian = float3.zero;
			input.BallVelocity = new float3(0f, 0f, -0.18f);
			input.ExternalTorque = 0f;
			input.OtherAngularImpulse = 0f;
			input.HingeStiffness = 0f;
			input.HingeDamping = 0f;
			input.HoldStiffness = 4f;
			input.HoldDamping = 0.4f;
			input.MaximumHoldForce = 100f;
			var result = SpringHingeNumericalFixtures.SolveHold(input);

			Assert.That(math.abs(result.BallVelocity.z), Is.LessThan(math.abs(input.BallVelocity.z)));
			Assert.That(math.abs(result.ExternalAcceleration.z), Is.LessThan(0.75f));
		}

		[Test]
		public void FrequencyEnvelopePublishesMinimumLoadedPeriod()
		{
			const float step = PhysicsConstants.PhysFactor;
			var minimumHingePeriod = 2f * math.PI * SpringHingeNumericalFixtures.MinimumHoldToHingeFrequencyRatio
				* step / SpringHingeNumericalFixtures.MaximumQualifiedHoldFrequencyStep;

			Assert.That(minimumHingePeriod * PhysicsConstants.DefaultStepTimeS,
				Is.EqualTo(0.314159f).Within(1e-5f));
		}

		[Test]
		public void StopArrivalClampsOvershootAndMakesZeroTimeProgressExplicit()
		{
			const float maximumAngle = 0.35f;
			var hitTime = SpringHingeNumericalFixtures.TimeToStop(0.34f, 0.5f, -0.1f, maximumAngle);
			Assert.That(hitTime, Is.EqualTo(0.02f).Within(1e-6f));
			Assert.That(SpringHingeNumericalFixtures.TimeToStop(maximumAngle, 0.5f, -0.1f, maximumAngle), Is.EqualTo(-1f));
			Assert.That(SpringHingeNumericalFixtures.TimeToStop(0.36f, -0.2f, -0.1f, maximumAngle), Is.EqualTo(-1f));

			var overshot = SpringHingeNumericalFixtures.ApplyStop(
				new SpringHingeNumericalFixtures.HingeStep(0.36f, -0.2f), -0.1f, maximumAngle);
			Assert.That(overshot.Angle, Is.EqualTo(maximumAngle));
			Assert.That(overshot.AngularVelocity, Is.EqualTo(-0.2f));
		}

		private static SpringHingeNumericalFixtures.ImpactInput CreateImpactInput()
		{
			var normal = math.normalizesafe(new float3(-3f, 5f, 1f));
			return new SpringHingeNumericalFixtures.ImpactInput {
				Axis = math.normalizesafe(new float3(1f, 2f, 3f)),
				Pivot = new float3(-7f, 4f, 2f),
				Witness = new float3(18f, -5f, 11f),
				Normal = normal,
				BallVelocity = -12f * normal,
				BallInverseMass = 1f / 1.35f,
				HingeInertia = 4800f,
				HingeAngularVelocity = 0.17f,
				Restitution = 0.2f
			};
		}

		private static SpringHingeNumericalFixtures.HoldInput CreateCoupledHoldInput()
		{
			return new SpringHingeNumericalFixtures.HoldInput {
				BallVelocity = new float3(9f, -7f, 5f), BallMass = 1.2f,
				AngleError = 0.2f, HingeAngularVelocity = -0.5f, HingeInertia = 20f,
				ExternalTorque = 1f, OtherAngularImpulse = 0.15f,
				ArmJacobian = new float3(3f, -1f, 2f), PositionError = new float3(4f, -3f, 2f),
				HingeStiffness = 5f, HingeDamping = 0.3f,
				HoldStiffness = 120f, HoldDamping = 18f, MaximumHoldForce = 10000f, Step = 0.1f
			};
		}

		private static SpringHingeNumericalFixtures.HingeInput CreateFreeInput(
			SpringHingeNumericalFixtures.HoldInput input)
		{
			return new SpringHingeNumericalFixtures.HingeInput {
				Angle = input.AngleError, AngularVelocity = input.HingeAngularVelocity,
				Inertia = input.HingeInertia, Stiffness = input.HingeStiffness,
				Damping = input.HingeDamping,
				ExternalTorque = input.ExternalTorque + input.OtherAngularImpulse / input.Step,
				Step = input.Step
			};
		}

		private static (float Period, float AmplitudeRatio) MeasureOscillator(float step, float inertia, float stiffness)
		{
			var input = new SpringHingeNumericalFixtures.HingeInput { Angle = 0.05f, Inertia = inertia, Stiffness = stiffness, Step = step };
			var previousAngle = input.Angle;
			var firstCrossing = -1f;
			var firstCrossingSpeed = -1f;
			for (var i = 1; i < 20000; i++) {
				var state = SpringHingeNumericalFixtures.StepUnconstrained(input);
				input.Angle = state.Angle;
				input.AngularVelocity = state.AngularVelocity;
				if (previousAngle <= 0f && state.Angle > 0f) {
					var crossing = (i - 1 - previousAngle / (state.Angle - previousAngle)) * step;
					if (firstCrossing < 0f) {
						firstCrossing = crossing;
						firstCrossingSpeed = state.AngularVelocity;
					} else {
						return (crossing - firstCrossing, state.AngularVelocity / firstCrossingSpeed);
					}
				}
				previousAngle = state.Angle;
			}
			Assert.Fail("oscillator did not complete two measured cycles");
			return default;
		}

		private static float ProjectedAngularMomentum(float ballMass, float3 ballVelocity, float3 axis,
			float3 pivot, float3 witness, float hingeInertia, float hingeAngularVelocity)
		{
			return math.dot(axis, math.cross(witness - pivot, ballMass * ballVelocity))
				+ hingeInertia * hingeAngularVelocity;
		}
	}
}
