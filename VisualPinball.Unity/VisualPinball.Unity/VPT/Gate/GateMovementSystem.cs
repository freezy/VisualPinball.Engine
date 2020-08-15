using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class GateMovementSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("GateMovementSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.WithName("GateMovementJob").ForEach((ref Rotation rot, in GateStaticData data, in GateMovementData movementData) => {

				marker.Begin();

				rot.Value = quaternion.RotateX(-movementData.Angle);

				marker.End();

			}).Run();
		}
	}
}
