using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class BumperSkirtMovementSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BumperSkirtMovementSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.WithName("BumperSkirtMovementJob").ForEach((ref Rotation rot, in BumperSkirtAnimationData data) => {

				marker.Begin();

				rot.Value = quaternion.EulerXYZ(math.radians(data.Rotation.x), math.radians(data.Rotation.y), 0f);

				marker.End();

			}).Run();
		}
	}
}
