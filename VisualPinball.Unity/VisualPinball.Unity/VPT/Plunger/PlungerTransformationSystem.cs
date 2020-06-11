using Unity.Entities;
using Unity.Profiling;
using Unity.Rendering;
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

			Entities.WithoutBurst().ForEach((Entity entity, in PlungerAnimationData animationData,
				in DynamicBuffer<PlungerMeshBufferElement> vertices) =>
			{
				if (!animationData.IsDirty) {
					return;
				}

				marker.Begin();

				var meshComponent = EntityManager.GetSharedComponentData<RenderMesh>(entity);

				var frame = animationData.CurrentFrame;
				var numVtx = meshComponent.mesh.vertices.Length;
				var startPos = frame * numVtx;
				for (var i = 0; i < numVtx; i++) {
					meshComponent.mesh.vertices[i] = vertices[startPos + i].Value;
				}

				marker.End();

			}).Run();
		}
	}
}
