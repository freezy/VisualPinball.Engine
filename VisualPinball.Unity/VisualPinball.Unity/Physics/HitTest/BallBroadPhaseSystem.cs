using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.HitTest
{
	public class BallBroadPhaseSystem : JobComponentSystem
	{
		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var collisionData = GetSingleton<CollisionData>();
			return Entities.ForEach((ref BallData ballData) => {
				ref var quadTree = ref collisionData.QuadTree.Value;
				if (quadTree.Center.x > 0) {

				}
			}).Schedule(inputDeps);
		}
	}
}
