using Unity.Entities;
using Unity.Profiling;
using Unity.Transforms;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Bumper
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class BumperRingMovementSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BumperRingMovementSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.WithName("BumperRingMovementJob").ForEach((ref Translation trans, in BumperRingAnimationData data) => {

				marker.Begin();

				trans.Value.z = data.Offset;

				marker.End();

			}).Run();
		}
	}
}
