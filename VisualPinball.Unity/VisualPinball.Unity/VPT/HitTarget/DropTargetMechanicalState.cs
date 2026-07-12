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
		internal bool DroppedSwitchClosed; // reset path must re-arm this after the raised crossing
		internal bool HitEventFired; // reset path must re-arm this before returning to Latched
		internal bool PoseInitialized;
		internal float4x4 BaseTransform;
		internal int EventLimitTrips;
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
}
