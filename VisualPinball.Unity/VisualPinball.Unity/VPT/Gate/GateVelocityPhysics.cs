// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Unity;

namespace VisualPinballUnity
{
	internal static class GateVelocityPhysics
	{
		internal static void UpdateVelocities(ref GateMovementState movementState, in GateStaticState state)
		{
			if (!movementState.IsOpen) {
				if (math.abs(movementState.Angle) < state.AngleMin + 0.01f && math.abs(movementState.AngleSpeed) < 0.01f) {
					// stop a bit earlier to prevent a nearly endless animation (especially for slow balls)
					movementState.Angle = state.AngleMin;
					movementState.AngleSpeed = 0.0f;
				}
				if (math.abs(movementState.AngleSpeed) != 0.0f && movementState.Angle != state.AngleMin) {
					movementState.AngleSpeed -= math.sin(movementState.Angle) * state.GravityFactor * (float)(PhysicsConstants.PhysFactor / 100.0); // Center of gravity towards bottom of object, makes it stop vertical
					movementState.AngleSpeed *= state.Damping;
				}
			}
		}
	}
}
