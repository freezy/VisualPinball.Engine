using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct QuadTree
	{
		public BlobArray<BlobPtr<QuadTree>> Children;
		public BlobArray<BlobPtr<Collider.Collider>> Colliders;
		public float3 Center;
		public bool IsLeaf;

		public static void Create(HitQuadTree src, ref QuadTree dest, BlobBuilder builder)
		{
			var children = builder.Allocate(ref dest.Children, 4);
			for (var i = 0; i < 4; i++) {
				if (src.Children[i] != null) {
					ref var child = ref builder.Allocate(ref children[i]);
					Create(src.Children[i], ref child, builder);
				}
			}

			var colliders = builder.Allocate(ref dest.Colliders, src.HitObjects.Count);
			for (var i = 0; i < src.HitObjects.Count; i++) {
				Collider.Collider.Create(src.HitObjects[i], ref colliders[i], builder);
			}

			dest.Center = src.Center.ToUnityFloat3();
			dest.IsLeaf = src.IsLeaf;
		}

		public void GetAabbOverlaps(BallData ball, DynamicBuffer<ColliderBufferElement> colliders)
		{
			var ballAabb = ball.Aabb;
			var collisionRadiusSqr = ball.CollisionRadiusSqr;

			for (var i = 0; i < Colliders.Length; i++) {
				ref var ptr = ref Colliders[i];
				ref var collider = ref ptr.Value;
				if (collider.Aabb.IntersectRect(ballAabb) && collider.Aabb.IntersectSphere(ball.Position, collisionRadiusSqr)) {
					colliders.Add(new ColliderBufferElement { Value = collider });
				}
			}

			if (!IsLeaf) {
				var isLeft = ballAabb.Left <= Center.x;
				var isRight = ballAabb.Right >= Center.x;

				if (ballAabb.Top <= Center.y) {
					// Top
					if (isLeft) {
						Children[0].Value.GetAabbOverlaps(ball, colliders);
					}

					if (isRight) {
						Children[1].Value.GetAabbOverlaps(ball, colliders);
					}
				}

				if (ballAabb.Bottom >= Center.y) {
					// Bottom
					if (isLeft) {
						Children[2].Value.GetAabbOverlaps(ball, colliders);
					}

					if (isRight) {
						Children[3].Value.GetAabbOverlaps(ball, colliders);
					}
				}
			}
		}
	}
}
