using Unity.Entities;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	public class DynamicCollisionSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("DynamicCollisionSystem");
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			var hitTime = _simulateCycleSystemGroup.HitTime;
			var swapBallCollisionHandling = _simulateCycleSystemGroup.SwapBallCollisionHandling;
			var balls = GetComponentDataFromEntity<BallData>();
			var collEvents = GetComponentDataFromEntity<CollisionEventData>(true);

			Entities
				.WithName("DynamicCollisionJob")
				.WithNativeDisableParallelForRestriction(balls)
				.WithReadOnly(collEvents)
				.ForEach((ref BallData ball, ref CollisionEventData collEvent) => {

					// pick "other" ball
					ref var otherEntity = ref collEvent.ColliderEntity;

					// find balls with hit objects and minimum time
					if (otherEntity != Entity.Null && collEvent.HitTime <= hitTime) {

						marker.Begin();

						var otherBall = balls[otherEntity];
						var otherCollEvent = collEvents[otherEntity];

						// now collision, contact and script reactions on active ball (object)+++++++++

						//this.activeBall = ball;                         // For script that wants the ball doing the collision

						if (BallCollider.Collide(
							ref otherBall, ref ball,
							in collEvent, in otherCollEvent,
							swapBallCollisionHandling
						)) {
							balls[otherEntity] = otherBall;
						}

						// remove trial hit object pointer
						collEvent.ClearCollider();

						marker.End();
					}
				}).Run();
		}
	}
}
