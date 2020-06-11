using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Profiling;
using Unity.Rendering;
using UnityEngine;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Plunger
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class PlungerTransformationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlungerTransformationSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;

			Entities.WithoutBurst().ForEach((Entity entity, ref PlungerAnimationData animationData) => {

				if (!animationData.IsDirty) {
					return;
				}
				animationData.IsDirty = false;

				marker.Begin();

				var meshComponent = EntityManager.GetSharedComponentData<RenderMesh>(entity);

				var frame = animationData.CurrentFrame;
				var count = meshComponent.mesh.vertices.Length;
				var startPos = frame * count;

				var vector3Buffer = EntityManager.GetBuffer<PlungerMeshBufferElement>(entity).Reinterpret<Vector3>();
				var vertices = new NativeSlice<Vector3>(vector3Buffer.AsNativeArray(), startPos, count);

				meshComponent.mesh.SetVertices(vertices.ToArray());

				marker.End();

			}).Run();
		}
	}
}
