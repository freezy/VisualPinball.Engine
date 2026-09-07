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
	internal struct SpringHingeState
	{
		internal readonly int AnimationItemId;
		internal SpringHingeStaticState Static;
		internal SpringHingeMovementState Movement;

		internal SpringHingeState(int animationItemId, SpringHingeStaticState @static,
			SpringHingeMovementState movement)
		{
			AnimationItemId = animationItemId;
			Static = @static;
			Movement = movement;
		}
	}

	internal struct SpringHingeStaticState
	{
		internal int OwnerId;
		internal float3 Pivot;
		internal float3 Axis;
		internal float3 CentreOfMassArm;
		internal float Mass;
		internal float Inertia;
		internal float EquilibriumAngle;
		internal float Stiffness;
		internal float Damping;
		internal float MinimumAngle;
		internal float MaximumAngle;
	}

	internal struct SpringHingeMovementState
	{
		internal float Angle;
		internal float AngularVelocity;
		internal float TickStartAngularVelocity;
		internal float TickStartAngleError;
		internal float3 EffectiveGravity;
		internal float GravityTorque;
		internal float CommittedMagneticTorque;
		internal float ContinuousAngularAcceleration;
		internal float BlockedTorque;
		internal float TickStep;
		internal sbyte ActiveStop;
	}
}
