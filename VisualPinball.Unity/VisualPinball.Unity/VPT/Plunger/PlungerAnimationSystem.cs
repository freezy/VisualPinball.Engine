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
	internal class PlungerAnimationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlungerAnimationSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			var animationDatas = GetComponentDataFromEntity<PlungerAnimationData>();

			Entities
				.WithNativeDisableParallelForRestriction(animationDatas)
				.ForEach((in PlungerMovementData movementData, in PlungerStaticData staticData) =>
			{
				marker.Begin();

				var frame0 = (int)((movementData.Position - staticData.FrameStart) / (staticData.FrameEnd - staticData.FrameStart) * (staticData.NumFrames - 1) + 0.5f);
				var frame = frame0 < 0 ? 0 : frame0 >= staticData.NumFrames ? staticData.NumFrames - 1 : frame0;


				if (animationDatas.HasComponent(staticData.RodEntity)) {
					var rodAnimData = animationDatas[staticData.RodEntity];
					if (rodAnimData.CurrentFrame != frame) {
						rodAnimData.CurrentFrame = frame;
						rodAnimData.IsDirty = true;
						animationDatas[staticData.RodEntity] = rodAnimData;
					}
				}

				if (animationDatas.HasComponent(staticData.SpringEntity)) {
					var springAnimData = animationDatas[staticData.SpringEntity];
					if (springAnimData.CurrentFrame != frame) {
						springAnimData.CurrentFrame = frame;
						springAnimData.IsDirty = true;
						animationDatas[staticData.SpringEntity] = springAnimData;
					}
				}

				if (animationDatas.HasComponent(staticData.FlatEntity)) {
					var flatAnimData = animationDatas[staticData.FlatEntity];
					if (flatAnimData.CurrentFrame != frame) {
						flatAnimData.CurrentFrame = frame;
						flatAnimData.IsDirty = true;
						animationDatas[staticData.FlatEntity] = flatAnimData;
					}
				}

				marker.End();

			}).Run();
		}
	}
}
