// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal struct QuadTree
	{
		public BlobArray<BlobPtr<QuadTree>> Children;
		public BlobArray<BlobPtr<Aabb>> Bounds;
		public float3 Center;
		public bool IsLeaf;

		public static void Create(BlobBuilder builder, ref BlobArray<BlobPtr<Collider>> colliders, ref QuadTree dest, Aabb rootBounds)
		{
			var children = builder.Allocate(ref dest.Children, 4);

			var aabbs = new Aabb[colliders.Length];
			for (var i = 0; i < colliders.Length; i++) {
				aabbs[i] = colliders[i].Value.Aabb;
			}
			var bounds = aabbs.ToList();

			dest.CreateNextLevel(builder, rootBounds, 0, 0, bounds, ref children);
		}

		private void CreateNextLevel(BlobBuilder builder, Aabb bounds, int level, int levelEmpty,
			IReadOnlyCollection<Aabb> remainingBounds, ref BlobBuilderArray<BlobPtr<QuadTree>> children)
		{
			if (remainingBounds.Count <= 4) {
				//!! magic
				return;
			}

			IsLeaf = false;

			Center.x = (bounds.Left + bounds.Right) * 0.5f;
			Center.y = (bounds.Top + bounds.Bottom) * 0.5f;
			Center.z = (bounds.ZLow + bounds.ZHigh) * 0.5f;

			ref var child0 = ref builder.Allocate(ref children[0]);
			ref var child1 = ref builder.Allocate(ref children[1]);
			ref var child2 = ref builder.Allocate(ref children[2]);
			ref var child3 = ref builder.Allocate(ref children[3]);

			var childBounds0 = new List<Aabb>();
			var childBounds1 = new List<Aabb>();
			var childBounds2 = new List<Aabb>();
			var childBounds3 = new List<Aabb>();

			var vRemain = new List<Aabb>(); // hit objects which did not go to a quadrant

			//_unique = HitObjects[0].E ? HitObjects[0].Item as Primitive : null;

			// sort items into appropriate child nodes
			foreach (var aabb in remainingBounds) {
				int oct;

				// if ((hitObject.E ? hitObject.Item : null) != _unique) {
				// 	// are all objects in current node unique/belong to the same primitive?
				// 	_unique = null;
				// }

				if (aabb.Right < Center.x) {
					oct = 0;

				} else if (aabb.Left > Center.x) {
					oct = 1;

				} else {
					oct = 128;
				}

				if (aabb.Bottom < Center.y) {
					oct |= 0;

				} else if (aabb.Top > Center.y) {
					oct |= 2;

				} else {
					oct |= 128;
				}

				if ((oct & 128) == 0) {
					switch (oct) {
						case 0: childBounds0.Add(aabb); break;
						case 1: childBounds1.Add(aabb); break;
						case 2: childBounds2.Add(aabb); break;
						case 3: childBounds3.Add(aabb); break;
					}

				} else {
					vRemain.Add(aabb);
				}
			}

			// copy remaining AABBs to blob
			var boundsBlob = builder.Allocate(ref Bounds, vRemain.Count);
			for (var i = 0; i < vRemain.Count; i++) {
				ref var b = ref builder.Allocate(ref boundsBlob[i]);
				b.Top = vRemain[i].Top;
				b.Bottom = vRemain[i].Bottom;
				b.Left = vRemain[i].Left;
				b.Right = vRemain[i].Right;
				b.ZLow = vRemain[i].ZLow;
				b.ZHigh = vRemain[i].ZHigh;
				b.ColliderEntity = Entity.Null; // todo
				b.ColliderId = 0; // todo
			}

			// check if at least two nodes feature objects, otherwise don't bother subdividing further
			var countEmpty = vRemain.Count == 0 ? 1 : 0;
			if (childBounds0.Count == 0) ++countEmpty;
			if (childBounds1.Count == 0) ++countEmpty;
			if (childBounds2.Count == 0) ++countEmpty;
			if (childBounds3.Count == 0) ++countEmpty;

			if (countEmpty >= 4) {
				++levelEmpty;

			} else {
				levelEmpty = 0;
			}

			if (Center.x - bounds.Left > 0.0001 //!! magic
			    && levelEmpty <= 8 // If 8 levels were all just subdividing the same objects without luck, exit & Free the nodes again (but at least empty space was cut off)
			    && level + 1 < 128 / 3)
			{
				CreateNextLevel(builder, ref child0, ref childBounds0, GetBounds(0, in Center, in bounds), level, levelEmpty);
				CreateNextLevel(builder, ref child1, ref childBounds1, GetBounds(1, in Center, in bounds), level, levelEmpty);
				CreateNextLevel(builder, ref child2, ref childBounds2, GetBounds(2, in Center, in bounds), level, levelEmpty);
				CreateNextLevel(builder, ref child3, ref childBounds3, GetBounds(3, in Center, in bounds), level, levelEmpty);
			}
		}

		private void CreateNextLevel(BlobBuilder builder, ref QuadTree child, ref List<Aabb> childBounds,
			in Aabb bounds, int level, int levelEmpty)
		{
			var children0 = builder.Allocate(ref child.Children, 4);
			child.CreateNextLevel(builder, bounds, level + 1, levelEmpty, childBounds, ref children0);

		}

		public static void Create(Engine.Physics.QuadTree src, ref QuadTree dest, BlobBuilder builder)
		{
			var children = builder.Allocate(ref dest.Children, 4);
			for (var i = 0; i < 4; i++) {
				if (src.Children[i] != null) {
					ref var child = ref builder.Allocate(ref children[i]);
					Create(src.Children[i], ref child, builder);
				}
			}

			var boundsBlob = builder.Allocate(ref dest.Bounds, src.HitObjects.Count);
			for (var i = 0; i < src.HitObjects.Count; i++) {
				ref var bounds = ref builder.Allocate(ref boundsBlob[i]);
				src.HitObjects[i].HitBBox.ToAabb(ref bounds, src.HitObjects[i].Id);
			}

			dest.Center = src.Center.ToUnityFloat3();
			dest.IsLeaf = src.IsLeaf;
		}

		private static Aabb GetBounds(int i, in float3 center, in Aabb bounds)
		{
			return new Aabb {
				Left = (i & 1) != 0 ? center.x : bounds.Left,
				Top = (i & 2) != 0 ? center.y : bounds.Top,
				ZLow = bounds.ZLow,
				Right = (i & 1) != 0 ? bounds.Right : center.x,
				Bottom = (i & 2) != 0 ? bounds.Bottom : center.y,
				ZHigh = bounds.ZHigh
			};
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
