using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.HitTest
{
	[UpdateInGroup(typeof(SimulateCycleSystemGroup))]
	[UpdateBefore(typeof(UpdateDisplacementSystemGroup))]
	public class BallBroadPhaseSystem : JobComponentSystem
	{
		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var collisionData = GetSingleton<CollisionData>();
			return Entities.ForEach((ref BallData ballData) => {
				ref var quadTree = ref collisionData.QuadTree.Value;
				var colliders = quadTree.GetAabbOverlaps(ballData, new NativeList<Collider.Collider>());

				if (colliders.Length > 0) {
					Debug.Log($"Found {colliders.Length} overlaps.");
				}

			}).Schedule(inputDeps);
		}
	}
}
