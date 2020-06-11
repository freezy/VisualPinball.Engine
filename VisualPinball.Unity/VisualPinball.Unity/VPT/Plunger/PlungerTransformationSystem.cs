using Unity.Collections;
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

			Entities.WithoutBurst().ForEach((Entity entity, in PlungerAnimationData animationData) =>
			{
				if (!animationData.IsDirty) {
					return;
				}

				marker.Begin();

				var meshComponent = EntityManager.GetSharedComponentData<RenderMesh>(entity);

				var frame = animationData.CurrentFrame;
				var numVtx = meshComponent.mesh.vertices.Length;
				var startPos = frame * numVtx;

				var float3Buffer = EntityManager.GetBuffer<PlungerMeshBufferElement>(entity).Reinterpret<Vector3>();
				var vertices = new NativeSlice<Vector3>(float3Buffer.AsNativeArray(), startPos, numVtx);

				meshComponent.mesh.SetVertices(vertices.ToArray());

				marker.End();

			}).Run();
		}
	}
}
