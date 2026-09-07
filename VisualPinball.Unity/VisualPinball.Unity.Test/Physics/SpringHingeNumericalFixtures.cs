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

using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Pure numerical fixtures shared by the spring-hinge integration tests.
	/// They express the accepted kick-then-drift tick contract without requiring
	/// a running physics engine or Unity scene.
	/// </summary>
	internal static class SpringHingeNumericalFixtures
	{
		internal const float MaximumQualifiedHoldFrequencyStep = 0.2f;
		internal const float MinimumHoldToHingeFrequencyRatio = 10f;

		internal struct HingeInput
		{
			internal float Angle;
			internal float AngularVelocity;
			internal float Inertia;
			internal float Stiffness;
			internal float Damping;
			internal float EquilibriumAngle;
			internal float ExternalTorque;
			internal float Step;
		}

		internal readonly struct HingeStep
		{
			internal readonly float Angle;
			internal readonly float AngularVelocity;

			internal HingeStep(float angle, float angularVelocity)
			{
				Angle = angle;
				AngularVelocity = angularVelocity;
			}
		}

		internal struct ImpactInput
		{
			internal float3 BallVelocity;
			internal float BallInverseMass;
			internal float HingeAngularVelocity;
			internal float HingeInertia;
			internal float3 Axis;
			internal float3 Pivot;
			internal float3 Witness;
			internal float3 Normal;
			internal float Restitution;
		}

		internal readonly struct ImpactResult
		{
			internal readonly float3 BallVelocity;
			internal readonly float HingeAngularVelocity;
			internal readonly float Impulse;

			internal ImpactResult(float3 ballVelocity, float hingeAngularVelocity, float impulse)
			{
				BallVelocity = ballVelocity;
				HingeAngularVelocity = hingeAngularVelocity;
				Impulse = impulse;
			}
		}

		internal struct HoldInput
		{
			internal float3 BallVelocity;
			internal float BallMass;
			internal float AngleError;
			internal float HingeAngularVelocity;
			internal float HingeInertia;
			internal float ExternalTorque;
			internal float OtherAngularImpulse;
			internal float3 ArmJacobian;
			internal float3 PositionError;
			internal float HingeStiffness;
			internal float HingeDamping;
			internal float HoldStiffness;
			internal float HoldDamping;
			internal float MaximumHoldForce;
			internal float Step;
			internal sbyte ActiveStop;
		}

		internal readonly struct HoldResult
		{
			internal readonly float3 BallVelocity;
			internal readonly float HingeAngularVelocity;
			internal readonly float3 Impulse;
			internal readonly float3 ExternalAcceleration;
			internal readonly float CommittedMagneticTorque;
			internal readonly float ConstitutiveResidual;
			internal readonly float BearingImpulse;
			internal readonly bool IsCapped;
			internal readonly bool IsStopHeld;

			internal HoldResult(float3 ballVelocity, float hingeAngularVelocity, float3 impulse,
				float3 externalAcceleration, float committedMagneticTorque, float constitutiveResidual,
				float bearingImpulse, bool isCapped, bool isStopHeld)
			{
				BallVelocity = ballVelocity;
				HingeAngularVelocity = hingeAngularVelocity;
				Impulse = impulse;
				ExternalAcceleration = externalAcceleration;
				CommittedMagneticTorque = committedMagneticTorque;
				ConstitutiveResidual = constitutiveResidual;
				BearingImpulse = bearingImpulse;
				IsCapped = isCapped;
				IsStopHeld = isStopHeld;
			}
		}

		internal static HingeStep StepUnconstrained(in HingeInput input)
		{
			var angleError = input.Angle - input.EquilibriumAngle;
			var denominator = input.Inertia + input.Step * input.Damping + input.Step * input.Step * input.Stiffness;
			if (input.Inertia <= 0f || input.Step <= 0f || denominator <= 0f) {
				return new HingeStep(input.Angle, input.AngularVelocity);
			}
			var nextVelocity = (input.Inertia * input.AngularVelocity + input.Step * input.ExternalTorque
				- input.Step * input.Stiffness * angleError) / denominator;
			return new HingeStep(input.Angle + input.Step * nextVelocity, nextVelocity);
		}

		internal static HingeStep ApplyStop(in HingeStep step, float minimumAngle, float maximumAngle)
		{
			var angle = math.clamp(step.Angle, minimumAngle, maximumAngle);
			var velocity = step.AngularVelocity;
			if ((angle <= minimumAngle && velocity < 0f) || (angle >= maximumAngle && velocity > 0f)) {
				velocity = 0f;
			}
			return new HingeStep(angle, velocity);
		}

		internal static float TimeToStop(float angle, float angularVelocity, float minimumAngle, float maximumAngle)
		{
			if (angle < minimumAngle || angle > maximumAngle) {
				return -1f;
			}
			if (angularVelocity > 0f && angle < maximumAngle) {
				return (maximumAngle - angle) / angularVelocity;
			}
			if (angularVelocity < 0f && angle > minimumAngle) {
				return (minimumAngle - angle) / angularVelocity;
			}
			return -1f;
		}

		internal static ImpactResult SolveImpact(in ImpactInput input)
		{
			var arm = input.Witness - input.Pivot;
			var jacobian = math.dot(input.Axis, math.cross(arm, input.Normal));
			var relativeNormalVelocity = math.dot(input.BallVelocity
				- input.HingeAngularVelocity * math.cross(input.Axis, arm), input.Normal);
			if (relativeNormalVelocity >= 0f || input.BallInverseMass <= 0f || input.HingeInertia <= 0f) {
				return new ImpactResult(input.BallVelocity, input.HingeAngularVelocity, 0f);
			}
			var inverseEffectiveMass = input.BallInverseMass + jacobian * jacobian / input.HingeInertia;
			var impulse = -(1f + input.Restitution) * relativeNormalVelocity / inverseEffectiveMass;
			return new ImpactResult(
				input.BallVelocity + impulse * input.Normal * input.BallInverseMass,
				input.HingeAngularVelocity - impulse * jacobian / input.HingeInertia,
				impulse
			);
		}

		internal static HoldResult SolveHold(in HoldInput input)
		{
			var freeStep = StepUnconstrained(new HingeInput {
				Angle = input.AngleError,
				AngularVelocity = input.HingeAngularVelocity,
				Inertia = input.HingeInertia,
				Stiffness = input.HingeStiffness,
				Damping = input.HingeDamping,
				EquilibriumAngle = 0f,
				ExternalTorque = input.ExternalTorque + input.OtherAngularImpulse / math.max(input.Step, float.Epsilon),
				Step = input.Step
			});
			var hingeDenominator = input.HingeInertia + input.Step * input.HingeDamping
				+ input.Step * input.Step * input.HingeStiffness;
			if (input.BallMass <= 0f || input.HingeInertia <= 0f || input.Step <= 0f
				|| input.HoldStiffness < 0f || input.HoldDamping < 0f || input.HingeStiffness < 0f
				|| input.HingeDamping < 0f || hingeDenominator <= 0f) {
				return FreeHoldResult(input, freeStep.AngularVelocity);
			}

			var freeNumerator = input.HingeInertia * input.HingeAngularVelocity
				+ input.Step * input.ExternalTorque + input.OtherAngularImpulse
				- input.Step * input.HingeStiffness * input.AngleError;
			var holdFactor = input.Step * (input.HoldDamping + input.Step * input.HoldStiffness);
			var bias = -input.Step * input.HoldStiffness * input.PositionError;
			var inverseBallMass = 1f / input.BallMass;
			var omegaFree = freeNumerator / hingeDenominator;
			var relativeFreeVelocity = input.BallVelocity - input.ArmJacobian * omegaFree;
			var rhs = bias - holdFactor * relativeFreeVelocity;
			var effectiveInverseMass = inverseBallMass * float3x3.identity
				+ Outer(input.ArmJacobian) / hingeDenominator;
			var matrix = float3x3.identity + holdFactor * effectiveInverseMass;
			var impulse = math.mul(math.inverse(matrix), rhs);
			var isCapped = ProjectToCap(ref impulse, input.MaximumHoldForce, input.Step);
			var nextBallVelocity = input.BallVelocity + impulse * inverseBallMass;
			var nextHingeVelocity = (freeNumerator - math.dot(input.ArmJacobian, impulse)) / hingeDenominator;

			if (input.ActiveStop != 0 && input.ActiveStop * nextHingeVelocity > 0f) {
				var fixedImpulse = SolveFixedOwnerImpulse(input, bias, holdFactor, inverseBallMass);
				var fixedIsCapped = ProjectToCap(ref fixedImpulse, input.MaximumHoldForce, input.Step);
				var bearingImpulse = math.dot(input.ArmJacobian, fixedImpulse) - freeNumerator;
				if (input.ActiveStop * bearingImpulse <= 0f) {
					return CreateHoldResult(input, fixedImpulse, input.BallVelocity + fixedImpulse * inverseBallMass,
						0f, fixedIsCapped, bearingImpulse, true);
				}
			}

			return CreateHoldResult(input, impulse, nextBallVelocity, nextHingeVelocity, isCapped, 0f, false);
		}

		private static HoldResult FreeHoldResult(in HoldInput input, float hingeAngularVelocity)
		{
			return new HoldResult(input.BallVelocity, hingeAngularVelocity, float3.zero, float3.zero,
				input.Step > 0f ? input.OtherAngularImpulse / input.Step : 0f, 0f, 0f, false, false);
		}

		private static HoldResult CreateHoldResult(in HoldInput input, float3 impulse, float3 ballVelocity,
			float hingeAngularVelocity, bool isCapped, float bearingImpulse, bool isStopHeld)
		{
			var relativeVelocity = ballVelocity - input.ArmJacobian * hingeAngularVelocity;
			var requestedImpulse = -input.Step * input.HoldStiffness
				* (input.PositionError + input.Step * relativeVelocity)
				- input.Step * input.HoldDamping * relativeVelocity;
			return new HoldResult(ballVelocity, hingeAngularVelocity, impulse,
				impulse / (input.BallMass * input.Step),
				(input.OtherAngularImpulse - math.dot(input.ArmJacobian, impulse)) / input.Step,
				math.length(impulse - requestedImpulse), bearingImpulse, isCapped, isStopHeld);
		}

		private static float3 SolveFixedOwnerImpulse(in HoldInput input, float3 bias, float holdFactor,
			float inverseBallMass)
		{
			return (bias - holdFactor * input.BallVelocity) / (1f + holdFactor * inverseBallMass);
		}

		private static bool ProjectToCap(ref float3 impulse, float maximumHoldForce, float step)
		{
			var cap = math.max(0f, maximumHoldForce) * step;
			var impulseLength = math.length(impulse);
			if (impulseLength <= cap) {
				return false;
			}
			impulse = impulseLength > 0f ? impulse * (cap / impulseLength) : float3.zero;
			return true;
		}

		private static float3x3 Outer(float3 value)
		{
			return new float3x3(value * value.x, value * value.y, value * value.z);
		}
	}
}
