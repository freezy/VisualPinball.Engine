using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Physics
{
	public struct HitQuadTreeBlob
	{
		public BlobArray<HitQuadTreeBlob> Children;
		public float3 Center;
		public BlobArray<uint> HitObjectIds;
		public bool IsLeaf;

		public static void Create(BlobBuilder blobBuilder, ref HitQuadTreeBlob hitQuadTreeBlob, HitQuadTree hitQuad)
		{
			var children = blobBuilder.Allocate(ref hitQuadTreeBlob.Children, 4);
			for (var i = 0; i < 4; i++) {
				if (hitQuad.Children[i] != null) {
					Create(blobBuilder, ref children[i], hitQuad.Children[i]);
				}
			}
			hitQuadTreeBlob.Center = hitQuad.Center.ToUnityFloat3();
			hitQuadTreeBlob.IsLeaf = hitQuad.IsLeaf;
			var hitObjectIds = blobBuilder.Allocate(ref hitQuadTreeBlob.HitObjectIds, hitQuad.HitObjects.Count);
			for (var i = 0; i < hitQuad.HitObjects.Count; i++) {
				hitObjectIds[i] = hitQuad.HitObjects[i].Id;
			}
		}

		public void HitTestBall(Ball ball, CollisionEvent coll, PlayerPhysics physics, HitObjectsBlob hob)
		{
			for (var i = 0; i < HitObjectIds.Length; i++) {
				var vho = hob.Get(HitObjectIds[i]);
				if (ball.Hit != vho // ball can not hit itself
				    && vho.HitBBox.IntersectRect(ball.Hit.HitBBox)
				    && vho.HitBBox.IntersectSphere(ball.State.Pos, ball.Hit.HitRadiusSqr))
				{
					//vho.DoHitTest(ball, coll, physics);
				}
			}
		}
	}
}
