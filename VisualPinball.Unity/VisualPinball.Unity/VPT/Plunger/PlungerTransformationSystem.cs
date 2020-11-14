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

			Entities.WithoutBurst().ForEach((Entity entity, ref PlungerAnimationData animationData, ref DynamicBuffer<BlendShapeWeight> blendShapeWeights) => {

				if (!animationData.IsDirty) {
					return;
				}
				animationData.IsDirty = false;

				marker.Begin();

				var blendShapeWeight = blendShapeWeights[0];
				blendShapeWeight.Value = math.clamp(1 / 25f * animationData.CurrentFrame, 0, 1);
				blendShapeWeights[0] = blendShapeWeight;

				Debug.Log("Animating plunger to " + blendShapeWeight.Value + " (" + animationData.CurrentFrame + ")");

				// var meshComponent = EntityManager.GetComponentData<BlendShapeWeight>(entity);
				//
				//
				//
				// var frame = animationData.CurrentFrame;
				// var count = meshComponent.mesh.vertices.Length;
				// var startPos = frame * count;
				//
				// var vector3Buffer = EntityManager.GetBuffer<PlungerMeshBufferElement>(entity).Reinterpret<Vector3>();
				// meshComponent.mesh.SetVertices(vector3Buffer.AsNativeArray(), startPos, count);
				//
				// // a bit dirty, but that means it's a flat mesh, hence update UVs as well.
				// if (count == 4) {
				// 	var uvBuffer = EntityManager.GetBuffer<PlungerUvBufferElement>(entity).Reinterpret<Vector2>();
				// 	meshComponent.mesh.SetUVs(0, uvBuffer.AsNativeArray(), startPos, count);
				// }

				marker.End();

			}).Run();
		}
	}
}
