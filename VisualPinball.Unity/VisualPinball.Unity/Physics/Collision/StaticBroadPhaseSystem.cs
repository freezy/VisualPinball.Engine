using Unity.Entities;
using UnityEngine.Profiling;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class StaticBroadPhaseSystem : SystemBase
	{
		protected override void OnUpdate()
		{
			// retrieve reference to static quad tree data
			var collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(QuadTreeData));
			var collEntity = collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<QuadTreeData>(collEntity);

			Entities.WithName("StaticBroadPhaseJob").ForEach((ref DynamicBuffer<OverlappingStaticColliderBufferElement> matchedColliders, in BallData ballData) => {

				// Profiler.BeginSample("StaticBroadPhaseJob");

				ref var quadTree = ref collData.Value.Value.QuadTree;
				matchedColliders.Clear();
				quadTree.GetAabbOverlaps(in ballData, ref matchedColliders);

				// Profiler.EndSample();

			}).Run();
		}
	}
}
