using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace VisualPinball.Unity
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class FlipperRotateSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("FlipperRotateSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.WithName("FlipperRotateJob").ForEach((ref Rotation rot, in FlipperMovementData movement) => {

				marker.Begin();

				rot.Value = math.mul(movement.BaseRotation, quaternion.EulerXYZ(0, 0, movement.Angle));

				marker.End();

			}).Run();
		}
	}
}
