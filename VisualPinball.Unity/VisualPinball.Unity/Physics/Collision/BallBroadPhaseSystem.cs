using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class BallBroadPhaseSystem : JobComponentSystem
	{
		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			// retrieve static collision data
			var collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(CollisionData));
			var collEntity = collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<CollisionData>(collEntity);

			return Entities.ForEach((ref DynamicBuffer<ColliderBufferElement> colliders, in BallData ballData) => {
				ref var quadTree = ref collData.QuadTree.Value;
				colliders.Clear();
				colliders.Add(new ColliderBufferElement { Value = collData.PlayfieldCollider}); // todo check if not covered by playfield mesh
				//colliders.Add(new ColliderBufferElement { Value = collData.GlassCollider});

				quadTree.GetAabbOverlaps(ballData, colliders);

			}).Schedule(inputDeps);
		}
	}
}
