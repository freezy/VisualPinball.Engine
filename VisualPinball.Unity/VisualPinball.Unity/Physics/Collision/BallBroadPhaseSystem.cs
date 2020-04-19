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
			var collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
			var collEntity = collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);

			return Entities.ForEach((ref DynamicBuffer<ColliderBufferElement> colliders, in BallData ballData) => {
				ref var quadTree = ref collData.Colliders.Value.QuadTree;
				colliders.Clear();

				// glass and playfield are always added
				colliders.Add(new ColliderBufferElement { Value = collData.Colliders.Value.PlayfieldCollider.Value }); // todo check if not covered by playfield mesh
				//colliders.Add(new ColliderBufferElement { Value = collData.Colliders.Value.GlassCollider.Value});

				quadTree.GetAabbOverlaps(ballData, colliders);

			}).Schedule(inputDeps);
		}
	}
}
