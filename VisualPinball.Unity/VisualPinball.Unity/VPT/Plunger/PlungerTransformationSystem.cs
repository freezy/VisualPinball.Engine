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

using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	internal class PlungerTransformationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlungerTransformationSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;

			Entities.ForEach((Entity entity, ref PlungerAnimationData animationData,
				ref DynamicBuffer<BlendShapeWeight> blendShapeWeights) => {

				if (!animationData.IsDirty) {
					return;
				}
				animationData.IsDirty = false;

				marker.Begin();

				var weight = math.clamp((float)animationData.CurrentFrame / animationData.NumFrames, 0, 1);
				var blendShapeWeight = blendShapeWeights[0];
				blendShapeWeight.Value = weight;
				blendShapeWeights[0] = blendShapeWeight;

				marker.End();

			}).Run();
		}
	}
}
