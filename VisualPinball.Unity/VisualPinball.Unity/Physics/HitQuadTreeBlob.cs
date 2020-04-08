using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Unity.Physics.HitTest;

namespace VisualPinball.Unity.Physics
{
	public struct HitQuadTreeBlob
	{
		public readonly BlobPtr<HitQuadTreeBlob> Child0;
		public readonly BlobPtr<HitQuadTreeBlob> Child1;
		public readonly BlobPtr<HitQuadTreeBlob> Child2;
		public readonly BlobPtr<HitQuadTreeBlob> Child3;

		public readonly float3 Center;
		public BlobArray<uint> HitObjectIds;
		public bool IsLeaf;

		public static HitQuadTreeBlob Create(HitQuadTree hitQuad)
		{
			return new HitQuadTreeBlob();
		}

		public void HitTestBall(Ball ball, CollisionEvent coll, PlayerPhysics physics, HitObjectBlobAsset hob)
		{
			for (var i = 0; i < HitObjectIds.Length; i++) {
				var vho = hob.Get(HitObjectIds[i]);
				if (ball.Hit != vho // ball can not hit itself
				    && vho.HitBBox.IntersectRect(ball.Hit.HitBBox)
				    && vho.HitBBox.IntersectSphere(ball.State.Pos, ball.Hit.HitRadiusSqr))
				{
					vho.DoHitTest(ball, coll, physics);
				}
			}
		}
	}
}
