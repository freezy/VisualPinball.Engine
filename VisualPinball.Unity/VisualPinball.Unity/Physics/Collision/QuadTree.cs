// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	internal struct QuadTree
	{
		public BlobArray<BlobPtr<QuadTree>> Children;
		public BlobArray<BlobPtr<Aabb>> Bounds;
		public float3 Center;
		public bool IsLeaf;

		public static void Create(Engine.Physics.QuadTree src, ref QuadTree dest, BlobBuilder builder)
		{
			var children = builder.Allocate(ref dest.Children, 4);
			for (var i = 0; i < 4; i++) {
				if (src.Children[i] != null) {
					ref var child = ref builder.Allocate(ref children[i]);
					Create(src.Children[i], ref child, builder);
				}
			}

			var colliders = builder.Allocate(ref dest.Bounds, src.HitObjects.Count);
			for (var i = 0; i < src.HitObjects.Count; i++) {
				ref var bounds = ref builder.Allocate(ref colliders[i]);
				src.HitObjects[i].HitBBox.ToAabb(ref bounds, src.HitObjects[i].Id);
			}

			dest.Center = src.Center.ToUnityFloat3();
			dest.IsLeaf = src.IsLeaf;
		}

		public void GetAabbOverlaps(in BallData ball, ref DynamicBuffer<OverlappingStaticColliderBufferElement> matchedColliderIds)
		{
			var ballAabb = ball.Aabb;
			var collisionRadiusSqr = ball.CollisionRadiusSqr;

			for (var i = 0; i < Bounds.Length; i++) {
				ref var bounds = ref Bounds[i].Value;
				if (bounds.IntersectRect(ballAabb) && bounds.IntersectSphere(ball.Position, collisionRadiusSqr)) {
					matchedColliderIds.Add(new OverlappingStaticColliderBufferElement { Value = bounds.ColliderId });
				}
			}

			if (!IsLeaf) {
				var isLeft = ballAabb.Left <= Center.x;
				var isRight = ballAabb.Right >= Center.x;

				if (ballAabb.Top <= Center.y) {
					// Top
					if (isLeft) {
						Children[0].Value.GetAabbOverlaps(in ball, ref matchedColliderIds);
					}

					if (isRight) {
						Children[1].Value.GetAabbOverlaps(in ball, ref matchedColliderIds);
					}
				}

				if (ballAabb.Bottom >= Center.y) {
					// Bottom
					if (isLeft) {
						Children[2].Value.GetAabbOverlaps(in ball, ref matchedColliderIds);
					}

					if (isRight) {
						Children[3].Value.GetAabbOverlaps(in ball, ref matchedColliderIds);
					}
				}
			}
		}
	}
}
