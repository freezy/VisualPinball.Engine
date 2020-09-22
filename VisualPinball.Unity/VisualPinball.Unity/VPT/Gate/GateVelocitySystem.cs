// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	internal class GateVelocitySystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("FlipperVelocitySystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities
				.WithName("GateVelocityJob")
				.ForEach((ref GateMovementData movementData, in GateStaticData data) => {

				marker.Begin();

				if (!movementData.IsOpen) {
					if (math.abs(movementData.Angle) < data.AngleMin + 0.01f && math.abs(movementData.AngleSpeed) < 0.01f) {
						// stop a bit earlier to prevent a nearly endless animation (especially for slow balls)
						movementData.Angle = data.AngleMin;
						movementData.AngleSpeed = 0.0f;
					}
					if (math.abs(movementData.AngleSpeed) != 0.0f && movementData.Angle != data.AngleMin) {
						movementData.AngleSpeed -= math.sin(movementData.Angle) * data.GravityFactor * (float)(PhysicsConstants.PhysFactor / 100.0); // Center of gravity towards bottom of object, makes it stop vertical
						movementData.AngleSpeed *= data.Damping;
					}
				}

				marker.End();

			}).Run();
		}
	}
}
