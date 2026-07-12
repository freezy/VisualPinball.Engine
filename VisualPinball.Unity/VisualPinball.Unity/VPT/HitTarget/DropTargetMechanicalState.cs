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

namespace VisualPinball.Unity
{
	internal enum DropTargetMechanismState : byte
	{
		Latched,
		Released,
		Dropping,
		Down,
		Resetting,
		Settling,
		ForcedDrop,
	}

	internal enum DropTargetImpactOutcome : byte
	{
		None,
		FaceDrop,
		BrickRelatch,
		SideDeflection,
		BacksideDrop,
		BacksideBounce,
		ForcedDrop,
	}

	internal struct DropTargetMechanicalState
	{
		internal DropTargetMechanismState State;
		internal DropTargetImpactOutcome LastImpactOutcome;
		internal float Q;
		internal float QDot;
		internal float D;
		internal float DDot;
		internal float ResetStartD;
		internal float ResetElapsedMs;
		internal float SettleElapsedMs;
		internal bool DroppedSwitchClosed;
		internal bool HitEventFired;
		internal bool PoseInitialized;
		internal float4x4 BaseTransform;
	}

	internal readonly struct DropTargetImpactResult
	{
		internal readonly bool Applied;
		internal readonly float NormalImpulse;
		internal readonly float TangentImpulse;

		internal DropTargetImpactResult(bool applied, float normalImpulse, float tangentImpulse)
		{
			Applied = applied;
			NormalImpulse = normalImpulse;
			TangentImpulse = tangentImpulse;
		}
	}

	internal struct MechanicalDropTargetContact
	{
		internal int BallId;
		internal BallState Ball;
		internal float3 Normal;
		internal float3 Tangent;
		internal float Restitution;
		internal float Friction;
		internal float ApproachSpeed;
		internal float NormalImpulse;
		internal float TangentImpulse;
		internal byte Applied;
		internal byte HasTangent;
	}
}
