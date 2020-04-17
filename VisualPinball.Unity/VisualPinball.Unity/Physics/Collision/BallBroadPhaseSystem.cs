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
				var colliders = new NativeList<Collider.Collider>(Allocator.Temp) {
					collData.PlayfieldCollider, // todo check if not covered by playfield mesh
					collData.GlassCollider
				};
				quadTree.GetAabbOverlaps(ballData, colliders);

				if (colliders.Length > 2) {
					Debug.Log($"Found {colliders.Length} overlaps.");
				}

				colliders.Dispose();

			}).Run();

			return inputDeps;
			//}).Schedule(inputDeps);
		}
	}
}
