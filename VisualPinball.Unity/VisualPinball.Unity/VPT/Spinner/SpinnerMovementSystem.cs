using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class SpinnerMovementSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("SpinnerMovementSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.WithName("SpinnerMovementJob").ForEach((ref Rotation rot, in SpinnerStaticData data, in SpinnerMovementData movementData) => {

				marker.Begin();

				rot.Value = quaternion.RotateX(-movementData.Angle);

				marker.End();

			}).Run();
		}
	}
}
