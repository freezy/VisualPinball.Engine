using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Physics.Flipper
{
	public class FlipperRotateSystem : JobComponentSystem
	{
		//[BurstCompile]
		struct MoveForwardRotation : IJobForEach<Rotation, FlipperMovementData, LocalToWorld>
		{
			public void Execute(ref Rotation rot, [ReadOnly] ref FlipperMovementData movement, [ReadOnly] ref LocalToWorld ltw)
			{
				var e = rot.Value.ToEuler();

				// nothing applied:
				// left flipper parent: -0.374 / 0.655 / 0.569 / 0.325
				//                base:  0     / 0     / 0     / 1
				//              rubber: -0.374 / 0.655 / 0.569 / 0.325

				rot.Value = quaternion.EulerXYZ(e.x, e.y, movement.Angle);
			}
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var moveForwardRotationJob = new MoveForwardRotation();
			return moveForwardRotationJob.Schedule(this, inputDeps);
		}
	}
}
