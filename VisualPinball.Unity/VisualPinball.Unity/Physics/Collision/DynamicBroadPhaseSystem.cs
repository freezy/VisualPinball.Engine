using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class DynamicBroadPhaseSystem : SystemBase
	{
		private EntityQuery _ballQuery;
		private static readonly ProfilerMarker PerfMarker1 = new ProfilerMarker("DynamicBroadPhaseSystem.CreateKdTree");
		private static readonly ProfilerMarker PerfMarker2 = new ProfilerMarker("DynamicBroadPhaseSystem.GetAabbOverlaps");

		protected override void OnCreate() {
			_ballQuery = GetEntityQuery(ComponentType.ReadOnly<BallData>());
		}

		protected override void OnUpdate()
		{
			// create kdtree
			PerfMarker1.Begin();
			var ballEntities = _ballQuery.ToEntityArray(Allocator.Temp);
			var balls = GetComponentDataFromEntity<BallData>();
			var ballBounds = new NativeArray<Aabb>(ballEntities.Length, Allocator.Temp);
			for (var i = 0; i < ballEntities.Length; i++) {
				ballBounds[i] = balls[ballEntities[i]].GetAabb(ballEntities[i]);
			}
			var kdRoot = new KdRoot(ballBounds);
			ballEntities.Dispose();
			PerfMarker1.End();

			var marker = PerfMarker2;

			Entities
				.WithName("StaticBroadPhaseJob")
				.ForEach((Entity entity, ref DynamicBuffer<OverlappingDynamicBufferElement> colliderIds, in BallData ball) => {

					marker.Begin();

					colliderIds.Clear();
					kdRoot.GetAabbOverlaps(in entity, in ball, ref colliderIds);

					marker.End();

				}).Run();

			ballBounds.Dispose();
			kdRoot.Dispose();
		}
	}
}
