using Unity.Entities;
using Unity.Profiling;
using Unity.Rendering;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Plunger
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class PlungerAnimationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlungerAnimationSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;

			// Entities.WithoutBurst().ForEach((ref RenderMesh mesh, in PlungerAnimationData animationData,
			// 	in DynamicBuffer<PlungerMeshBufferElement> vertices) =>
			// {
			// 	marker.Begin();
			//
			// 	var frame = animationData.CurrentFrame;
			// 	var numVtx = mesh.mesh.vertices.Length;
			// 	var startPos = frame * numVtx;
			// 	for (var i = 0; i < numVtx; i++) {
			// 		mesh.mesh.vertices[i] = vertices[startPos + i].Value;
			// 	}
			//
			// 	marker.End();
			//
			// }).Run();
		}
	}
}
