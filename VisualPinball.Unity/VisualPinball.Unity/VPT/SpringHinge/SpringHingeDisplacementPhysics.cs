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
	internal static class SpringHingeDisplacementPhysics
	{
		private const float StopTolerance = 1e-6f;

		internal static float GetStopTime(in SpringHingeState state)
		{
			var angle = state.Movement.Angle;
			var angularVelocity = state.Movement.AngularVelocity;
			if (angularVelocity > 0f && angle < state.Static.MaximumAngle - StopTolerance) {
				return (state.Static.MaximumAngle - angle) / angularVelocity;
			}
			if (angularVelocity < 0f && angle > state.Static.MinimumAngle + StopTolerance) {
				return (state.Static.MinimumAngle - angle) / angularVelocity;
			}
			return -1f;
		}

		internal static void UpdateDisplacement(ref SpringHingeState state, float step)
		{
			ref var movement = ref state.Movement;
			movement.Angle += movement.AngularVelocity * step;

			if (movement.Angle > state.Static.MaximumAngle
			    || movement.AngularVelocity > 0f
			    && movement.Angle >= state.Static.MaximumAngle - StopTolerance) {
				var movingOutward = movement.AngularVelocity > 0f;
				movement.Angle = state.Static.MaximumAngle;
				if (movingOutward) {
					movement.AngularVelocity = 0f;
					movement.ActiveStop = 1;
				} else {
					movement.ActiveStop = 0;
				}
			} else if (movement.Angle < state.Static.MinimumAngle
			           || movement.AngularVelocity < 0f
			           && movement.Angle <= state.Static.MinimumAngle + StopTolerance) {
				var movingOutward = movement.AngularVelocity < 0f;
				movement.Angle = state.Static.MinimumAngle;
				if (movingOutward) {
					movement.AngularVelocity = 0f;
					movement.ActiveStop = -1;
				} else {
					movement.ActiveStop = 0;
				}
			} else if (movement.ActiveStop > 0
			           && math.abs(movement.Angle - state.Static.MaximumAngle) <= StopTolerance
			           && math.abs(movement.AngularVelocity) <= StopTolerance) {
				movement.Angle = state.Static.MaximumAngle;
			} else if (movement.ActiveStop < 0
			           && math.abs(movement.Angle - state.Static.MinimumAngle) <= StopTolerance
			           && math.abs(movement.AngularVelocity) <= StopTolerance) {
				movement.Angle = state.Static.MinimumAngle;
			} else {
				movement.ActiveStop = 0;
			}

			SpringHingeVelocityPhysics.RefreshContinuousAcceleration(ref state);
		}
	}
}
