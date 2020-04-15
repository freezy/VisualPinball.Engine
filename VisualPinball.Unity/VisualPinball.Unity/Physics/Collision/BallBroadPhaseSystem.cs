using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class BallBroadPhaseSystem : JobComponentSystem
	{
		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(CollisionData));
			var collEntity = collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<CollisionData>(collEntity);

			Entities.WithoutBurst().ForEach((ref BallData ballData) => {
				ref var quadTree = ref collData.QuadTree.Value;
				var colliders = quadTree.GetAabbOverlaps(ballData, new NativeList<Collider.Collider>());

				if (colliders.Length > 0) {
					Debug.Log($"Found {colliders.Length} overlaps.");
				}

			}).Run();
			//}).Schedule(inputDeps);

			return default;
		}
	}
}
