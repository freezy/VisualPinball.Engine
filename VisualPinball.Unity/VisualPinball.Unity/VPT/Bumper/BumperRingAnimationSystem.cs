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

using Unity.Entities;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateAnimationsSystemGroup))]
	internal class BumperRingAnimationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BumperRingAnimationSystem");

		protected override void OnUpdate()
		{
			var dTime = Time.DeltaTime * 1000;
			var marker = PerfMarker;

			Entities
				.WithName("BumperRingAnimationJob")
				.ForEach((ref BumperRingAnimationData data) => {

					// todo visibility - skip if invisible

					marker.Begin();

					var limit = data.DropOffset + data.HeightScale * 0.5f * data.ScaleZ;
					if (data.IsHit) {
						data.DoAnimate = true;
						data.AnimateDown = true;
						data.IsHit = false;
					}
					if (data.DoAnimate) {
						var step = data.Speed * data.ScaleZ;
						if (data.AnimateDown) {
							step = -step;
						}
						data.Offset += step * dTime;
						if (data.AnimateDown) {
							if (data.Offset <= -limit) {
								data.Offset = -limit;
								data.AnimateDown = false;
							}
						} else {
							if (data.Offset >= 0.0f) {
								data.Offset = 0.0f;
								data.DoAnimate = false;
							}
						}
					}

					marker.End();

				}).Run();
		}
	}
}
