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
using UnityEngine;

namespace VisualPinball.Unity
{
	public enum DropTargetPhysicsMode : byte
	{
		Legacy,
		RothCompatible,
		Mechanical,
	}

	public enum DropTargetDeflectionKind : byte
	{
		SlidingBlade,
		HingedBlade,
	}

	[Serializable]
	public struct DropTargetMechanicalConfig
	{
		public float EffectiveFaceMass;
		public float MinimumFaceImpulse;

		public DropTargetDeflectionKind DeflectionKind;
		public Vector3 DeflectionAxis;
		public Vector3 DeflectionPivot;
		public Vector3 ReferenceContactPoint;
		public float LatchReleaseTravel;
		public float LatchRelatchTravel;
		public float LatchEscapeDrop;
		public float RearStopTravel;
		public float RearSpringFrequencyHz;
		public float RearDampingRatio;
		public float RearStopRestitution;

		public float DropMass;
		public float DropTravel;
		public float DropSpringForce;
		public float GuideDamping;
		public float GuideFriction;
		public float GuideVelocityDeadband;
		public float DownStopRestitution;
		public float DroppedSwitchTravel;

		public float ResetDurationMs;
		public float ResetEffectiveMass;
		public float ResetOvershootTravel;
		public float ResetSettleDelayMs;
		public float RaisedSwitchTravel;

		public bool EnableBacksideRelease;
		public float BacksideReleaseImpulse;
		public float MechanicalVariation;

		public static DropTargetMechanicalConfig Default => new DropTargetMechanicalConfig {
			EffectiveFaceMass = 0.2f,
			DeflectionKind = DropTargetDeflectionKind.SlidingBlade,
			DeflectionAxis = Vector3.back,
			LatchReleaseTravel = 2f,
			LatchRelatchTravel = 1f,
			LatchEscapeDrop = 2f,
			RearStopTravel = 4f,
			RearSpringFrequencyHz = 70f,
			RearDampingRatio = 0.25f,
			RearStopRestitution = 0.65f,
			DropMass = 0.2f,
			DropTravel = 52f,
			DropSpringForce = 40f,
			GuideDamping = 0.2f,
			GuideFriction = 0.1f,
			GuideVelocityDeadband = 0.001f,
			DownStopRestitution = 0.1f,
			DroppedSwitchTravel = 20f,
			ResetDurationMs = 40f,
			ResetEffectiveMass = 1f,
			ResetOvershootTravel = 10f,
			ResetSettleDelayMs = 20f,
			RaisedSwitchTravel = 2f,
		};
	}

	[Serializable]
	public struct DropTargetRothConfig
	{
		public float TargetMass;
		public bool EnableBrick;
		public float BrickVelocity;
		public float BrickCenterDistance;
		public bool EnableBacksideDrop;
		public float BacksideVelocity;
		public bool EnableVerticalBouncer;
		public float VerticalBouncerFactor;
		public float VerticalBouncerDeflection;
		public int DeterministicSeed;
		public float DropDelayMs;
		public float DropDurationMs;
		public float RaiseDelayMs;
		public float RaiseDurationMs;
		public float DropTravel;
		public float ResetOvershootTravel;

		public static DropTargetRothConfig Default => new DropTargetRothConfig {
			TargetMass = 0.2f,
			BrickVelocity = 30f,
			BrickCenterDistance = 8f,
			BacksideVelocity = 15f,
			VerticalBouncerFactor = 0.9f,
			VerticalBouncerDeflection = 1f,
			DeterministicSeed = 1,
			DropDelayMs = 20f,
			DropDurationMs = 90f,
			RaiseDelayMs = 40f,
			RaiseDurationMs = 40f,
			DropTravel = 52f,
			ResetOvershootTravel = 10f,
		};
	}

	[PackAs("DropTargetPhysicsProfile")]
	[CreateAssetMenu(fileName = "DropTargetPhysicsProfile", menuName = "Pinball/Drop Target Physics Profile", order = 101)]
	public class DropTargetPhysicsProfile : ScriptableObject
	{
		public DropTargetMechanicalConfig Config = DropTargetMechanicalConfig.Default;
	}
}
