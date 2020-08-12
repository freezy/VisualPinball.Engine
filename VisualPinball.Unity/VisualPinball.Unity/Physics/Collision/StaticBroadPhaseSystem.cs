using Unity.Entities;
using Unity.Profiling;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class StaticBroadPhaseSystem : SystemBase
	{
		private EntityQuery _quadTreeEntityQuery;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticBroadPhaseSystem");

		protected override void OnCreate()
		{
			_quadTreeEntityQuery = EntityManager.CreateEntityQuery(typeof(QuadTreeData));
		}

		protected override void OnUpdate()
		{
			// retrieve reference to static quad tree data
			var collEntity = _quadTreeEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<QuadTreeData>(collEntity);
			var marker = PerfMarker;

			Entities
				.WithName("StaticBroadPhaseJob")
				.ForEach((ref DynamicBuffer<OverlappingStaticColliderBufferElement> colliderIds, in BallData ballData) => {

				// don't play with frozen balls
				if (ballData.IsFrozen) {
					return;
				}

				marker.Begin();

				ref var quadTree = ref collData.Value.Value.QuadTree;
				colliderIds.Clear();
				quadTree.GetAabbOverlaps(in ballData, ref colliderIds);

				marker.End();

			}).Run();
		}
	}
}
