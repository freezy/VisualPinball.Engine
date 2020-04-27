using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class StaticBroadPhaseSystem : JobComponentSystem
	{
		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			// retrieve reference to static quad tree data
			var collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(QuadTreeData));
			var collEntity = collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<QuadTreeData>(collEntity);

			return Entities.WithoutBurst().WithName("StaticBroadPhaseJob").ForEach((ref DynamicBuffer<MatchedColliderBufferElement> matchedColliders, in BallData ballData) => {
				ref var quadTree = ref collData.Value.Value.QuadTree;
				matchedColliders.Clear();
				quadTree.GetAabbOverlaps(in ballData, ref matchedColliders);

			}).Schedule(inputDeps);
		}
	}
}
