// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal static class SpringHingeVelocityPhysics
	{
		private const float StopTolerance = 1e-6f;

		internal static void UpdateVelocity(ref SpringHingeState state, in float3 effectiveGravity,
			float step)
		{
			ref var movement = ref state.Movement;
			ref var data = ref state.Static;

			movement.TickStartAngularVelocity = movement.AngularVelocity;
			movement.TickStartAngleError = movement.Angle - data.EquilibriumAngle;
			movement.TickStep = step;
			movement.CommittedMagneticTorque = 0f;
			movement.EffectiveGravity = effectiveGravity;
			movement.GravityTorque = CalculateGravityTorque(in data, movement.Angle, in effectiveGravity);

			var denominator = data.Inertia + step * data.Damping + step * step * data.Stiffness;
			if (data.Inertia <= 0f || step <= 0f || denominator <= 0f || !math.isfinite(denominator)) {
				ApplyStopConstraint(ref movement, in data);
				RefreshContinuousAcceleration(ref state);
				return;
			}

			var numerator = data.Inertia * movement.TickStartAngularVelocity
				+ step * movement.GravityTorque
				- step * data.Stiffness * movement.TickStartAngleError;
			movement.AngularVelocity = numerator / denominator;
			ApplyStopConstraint(ref movement, in data);
			RefreshContinuousAcceleration(ref state);
		}

		internal static void RefreshContinuousAcceleration(ref SpringHingeState state)
		{
			ref var movement = ref state.Movement;
			ref var data = ref state.Static;
			if (data.Inertia <= 0f) {
				movement.ContinuousAngularAcceleration = 0f;
				movement.BlockedTorque = 0f;
				return;
			}

			var currentGravityTorque = CalculateGravityTorque(in data, movement.Angle, in movement.EffectiveGravity);
			var torque = currentGravityTorque + movement.CommittedMagneticTorque
				- data.Stiffness * (movement.Angle - data.EquilibriumAngle)
				- data.Damping * movement.AngularVelocity;
			if (movement.ActiveStop != 0 && movement.ActiveStop * torque > 0f
				&& math.abs(movement.AngularVelocity) <= StopTolerance) {
				movement.BlockedTorque = torque;
				movement.ContinuousAngularAcceleration = 0f;
			} else {
				movement.BlockedTorque = 0f;
				movement.ContinuousAngularAcceleration = torque / data.Inertia;
			}
		}

		internal static float CalculateGravityTorque(in SpringHingeStaticState state, float angle,
			in float3 effectiveGravity)
		{
			var arm = RotateAroundAxis(state.CentreOfMassArm, state.Axis, angle);
			return math.dot(state.Axis, math.cross(arm, state.Mass * effectiveGravity));
		}

		internal static float3 RotateAroundAxis(in float3 vector, in float3 axis, float angle)
		{
			var sine = math.sin(angle);
			var cosine = math.cos(angle);
			return vector * cosine + math.cross(axis, vector) * sine
				+ axis * math.dot(axis, vector) * (1f - cosine);
		}

		private static void ApplyStopConstraint(ref SpringHingeMovementState movement,
			in SpringHingeStaticState data)
		{
			if (movement.Angle <= data.MinimumAngle + StopTolerance && movement.AngularVelocity < 0f) {
				movement.Angle = data.MinimumAngle;
				movement.AngularVelocity = 0f;
				movement.ActiveStop = -1;
			} else if (movement.Angle >= data.MaximumAngle - StopTolerance && movement.AngularVelocity > 0f) {
				movement.Angle = data.MaximumAngle;
				movement.AngularVelocity = 0f;
				movement.ActiveStop = 1;
			} else if (movement.ActiveStop < 0 && math.abs(movement.Angle - data.MinimumAngle) <= StopTolerance
			           && math.abs(movement.AngularVelocity) <= StopTolerance) {
				movement.Angle = data.MinimumAngle;
			} else if (movement.ActiveStop > 0 && math.abs(movement.Angle - data.MaximumAngle) <= StopTolerance
			           && math.abs(movement.AngularVelocity) <= StopTolerance) {
				movement.Angle = data.MaximumAngle;
			} else {
				movement.ActiveStop = 0;
			}
		}
	}
}
