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

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;
using VisualPinball.Unity;
using VisualPinball.Unity.VisualPinball.Unity.Game;

namespace VisualPinballUnity
{
	internal partial class SpinnerVelocityPhysics
	{
		internal static void UpdateVelocities(ref SpinnerMovementData movementData, in SpinnerStaticData data)
		{
			// Center of gravity towards bottom of object, makes it stop vertical
			movementData.AngleSpeed -= math.sin(movementData.Angle) * (float)(0.0025 * PhysicsConstants.PhysFactor);
			movementData.AngleSpeed *= data.Damping;
		}
	}
}
