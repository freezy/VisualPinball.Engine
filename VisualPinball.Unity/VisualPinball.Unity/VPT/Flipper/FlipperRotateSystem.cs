using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using VisualPinball.Unity.Physics;

namespace VisualPinball.Unity.VPT.Flipper
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(VisualPinballTransformSystemGroup))]
	public class FlipperRotateSystem : JobComponentSystem
	{
		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			Entities.ForEach((ref Rotation rot, in FlipperMovementData movement) => {
				rot.Value = math.mul(movement.BaseRotation, quaternion.EulerXYZ(0, 0, movement.Angle));
			}).Run();

			return default;
		}
	}
}
