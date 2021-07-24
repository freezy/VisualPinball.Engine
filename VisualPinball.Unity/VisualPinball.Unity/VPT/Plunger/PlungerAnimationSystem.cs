// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateAnimationsSystemGroup))]
	internal class PlungerAnimationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlungerAnimationSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;

			Entities.WithName("PlungerAnimationJob")
				.ForEach((ref PlungerAnimationData animationData, in PlungerMovementData movementData, in PlungerStaticData staticData) =>
			{
				marker.Begin();

				var frame0 = (int)((movementData.Position - staticData.FrameStart) / (staticData.FrameEnd - staticData.FrameStart) * (staticData.NumFrames - 1) + 0.5f);
				var frame = frame0 < 0 ? 0 : frame0 >= staticData.NumFrames ? staticData.NumFrames - 1 : frame0;

				//Debug.Log($"[plunger] frame0 = {frame0} frame = {frame}");

				animationData.Position = math.clamp((float)frame / staticData.NumFrames, 0, 1);

				marker.End();

			}).Run();
		}
	}
}
