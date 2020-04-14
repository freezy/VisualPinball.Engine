using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.HitTest
{
	public class BallBroadPhaseSystem : JobComponentSystem
	{
		public BlobAssetReference<QuadTree> QuadTree;

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var quadTree = QuadTree;
			return Entities.ForEach((ref BallData ballData) => {
				if (quadTree.Value.Center.x > 0) {

				}
			}).Schedule(inputDeps);
		}
	}
}
