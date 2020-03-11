using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace VisualPinball.Unity.VPT.Flipper
{
	[UpdateAfter(typeof(ExportPhysicsWorld))]
	public class FlipperRotateSystem : JobComponentSystem
	{
		//[BurstCompile]
		struct MoveForwardRotation : IJobForEach<Rotation, FlipperMovementData>
		{
			public void Execute(ref Rotation rot, [ReadOnly] ref FlipperMovementData movement)
			{
				rot.Value = quaternion.EulerXYZ(0, 0, movement.Angle);
			}
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var moveForwardRotationJob = new MoveForwardRotation();
			return moveForwardRotationJob.Schedule(this, inputDeps);
		}
	}
}
