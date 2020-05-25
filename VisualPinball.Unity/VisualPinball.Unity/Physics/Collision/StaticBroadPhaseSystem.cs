using Unity.Entities;
using UnityEngine.Profiling;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class StaticBroadPhaseSystem : SystemBase
	{
		private EntityQuery _quadTreeEntityQuery;

		protected override void OnCreate()
		{
			_quadTreeEntityQuery = EntityManager.CreateEntityQuery(typeof(QuadTreeData));
		}

		protected override void OnUpdate()
		{
			// retrieve reference to static quad tree data
			var collEntity = _quadTreeEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<QuadTreeData>(collEntity);

			Entities.WithName("StaticBroadPhaseJob").ForEach((ref DynamicBuffer<OverlappingStaticColliderBufferElement> colliderIds, in BallData ballData) => {

				// Profiler.BeginSample("StaticBroadPhaseJob");

				ref var quadTree = ref collData.Value.Value.QuadTree;
				colliderIds.Clear();
				quadTree.GetAabbOverlaps(in ballData, ref colliderIds);

				// Profiler.EndSample();

			}).Run();
		}
	}
}
