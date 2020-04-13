using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.HitTest
{
	public class BallBroadPhaseSystem : JobComponentSystem
	{
		public HitQuadTree KdTree;

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			return Entities.ForEach((ref BallData ballData) => {

			}).Schedule(inputDeps);
		}
	}
}
