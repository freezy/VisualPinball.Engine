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

			var ballEntities = _ballQuery.ToEntityArray(Allocator.TempJob);
			var balls = GetComponentDataFromEntity<BallData>(true);
			var kdRoot = new KdRoot();
			Job.WithCode(() => {
				var ballBounds = new NativeArray<Aabb>(ballEntities.Length, Allocator.Temp);
				for (var i = 0; i < ballEntities.Length; i++) {
					ballBounds[i] = balls[ballEntities[i]].GetAabb(ballEntities[i]);
				}
				kdRoot.Init(ballBounds, Allocator.TempJob);
			}).Run();

			ballEntities.Dispose();
			PerfMarker1.End();

			var overlappingEntities = GetBufferFromEntity<OverlappingDynamicBufferElement>();
			var marker = PerfMarker2;

			Entities
				.WithName("StaticBroadPhaseJob")
				.WithNativeDisableParallelForRestriction(overlappingEntities)
				.ForEach((Entity entity, in BallData ball) => {

					marker.Begin();

					// don't play with frozen balls
					if (ball.IsFrozen) {
						return;
					}

					var colliderEntities = overlappingEntities[entity];
					colliderEntities.Clear();
					kdRoot.GetAabbOverlaps(in entity, in ball, ref colliderEntities);

					marker.End();

				}).Run();

			kdRoot.Dispose();
		}
	}
}
