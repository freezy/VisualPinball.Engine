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

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal struct QuadTree
	{
		private BlobArray<BlobPtr<QuadTree>> _children;
		private BlobArray<BlobPtr<ColliderBounds>> _bounds;
		private float3 _center;
		private bool _isLeaf;

		public static void Create(Player player, BlobBuilder builder, ref BlobArray<BlobPtr<Collider>> colliders, ref QuadTree dest, Aabb rootBounds)
		{
			var children = builder.Allocate(ref dest._children, 4);
			var aabbs = new List<ColliderBounds>();
			var cs = new List<Collider>();
			for (var i = 0; i < colliders.Length; i++) {
				if (colliders[i].Value.Type != ColliderType.Plane) {
					var c = colliders[i].Value;
					var bounds = colliders[i].Value.Bounds(player);
					//Debug.Log("Adding aabb " + aabb + " (" + colliders[i].Value.Type + ")");
					if (bounds.ColliderEntity == Entity.Null) {
						throw new InvalidOperationException($"Entity of {bounds} must be set ({colliders[i].Value.ItemType}).");
					}
					if (bounds.ColliderId < 0) {
						throw new InvalidOperationException($"ColliderId of {bounds} must be set ({colliders[i].Value.ItemType}).");
					}

					aabbs.Add(bounds);
					cs.Add(c);
				}
			}

			dest.CreateNextLevel(builder, rootBounds, 0, 0, aabbs, ref children);
		}

		private void CreateNextLevel(BlobBuilder builder, Aabb bounds, int level, int levelEmpty,
			IReadOnlyCollection<ColliderBounds> remainingBounds, ref BlobBuilderArray<BlobPtr<QuadTree>> children)
		{
			_center.x = (bounds.Left + bounds.Right) * 0.5f;
			_center.y = (bounds.Top + bounds.Bottom) * 0.5f;
			_center.z = (bounds.ZLow + bounds.ZHigh) * 0.5f;

			if (remainingBounds.Count <= 4) {
				CopyBounds(builder, remainingBounds.ToArray(), ref _bounds);
				_isLeaf = true;
				return;
			}

			_isLeaf = false;

			ref var child0 = ref builder.Allocate(ref children[0]);
			ref var child1 = ref builder.Allocate(ref children[1]);
			ref var child2 = ref builder.Allocate(ref children[2]);
			ref var child3 = ref builder.Allocate(ref children[3]);

			var childBounds0 = new List<ColliderBounds>();
			var childBounds1 = new List<ColliderBounds>();
			var childBounds2 = new List<ColliderBounds>();
			var childBounds3 = new List<ColliderBounds>();

			var vRemain = new List<ColliderBounds>(); // hit objects which did not go to a quadrant

			//_unique = HitObjects[0].E ? HitObjects[0].Item as Primitive : null;

			// sort items into appropriate child nodes
			foreach (var b in remainingBounds) {
				int oct;

				// if ((hitObject.E ? hitObject.Item : null) != _unique) {
				// 	// are all objects in current node unique/belong to the same primitive?
				// 	_unique = null;
				// }

				if (b.Aabb.Right < _center.x) {
					oct = 0;

				} else if (b.Aabb.Left > _center.x) {
					oct = 1;

				} else {
					oct = 128;
				}

				if (b.Aabb.Bottom < _center.y) {
					oct |= 0;

				} else if (b.Aabb.Top > _center.y) {
					oct |= 2;

				} else {
					oct |= 128;
				}

				if ((oct & 128) == 0) {
					switch (oct) {
						case 0: childBounds0.Add(b); break;
						case 1: childBounds1.Add(b); break;
						case 2: childBounds2.Add(b); break;
						case 3: childBounds3.Add(b); break;
					}

				} else {
					vRemain.Add(b);
				}
			}

			// copy remaining AABBs to blob
			CopyBounds(builder, vRemain, ref _bounds);

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

			if (_center.x - bounds.Left > 0.0001 //!! magic
			    && levelEmpty <= 8 // If 8 levels were all just subdividing the same objects without luck, exit & Free the nodes again (but at least empty space was cut off)
			    && level + 1 < 128 / 3)
			{
				CreateNextLevel(builder, ref child0, ref childBounds0, GetBounds(0, in _center, in bounds), level, levelEmpty);
				CreateNextLevel(builder, ref child1, ref childBounds1, GetBounds(1, in _center, in bounds), level, levelEmpty);
				CreateNextLevel(builder, ref child2, ref childBounds2, GetBounds(2, in _center, in bounds), level, levelEmpty);
				CreateNextLevel(builder, ref child3, ref childBounds3, GetBounds(3, in _center, in bounds), level, levelEmpty);

			} else {
				child0._isLeaf = true;
				child1._isLeaf = true;
				child2._isLeaf = true;
				child3._isLeaf = true;
			}
		}

		private static void CreateNextLevel(BlobBuilder builder, ref QuadTree child, ref List<ColliderBounds> childBounds,
			in Aabb bounds, int level, int levelEmpty)
		{
			var children = builder.Allocate(ref child._children, 4);
			child.CreateNextLevel(builder, bounds, level + 1, levelEmpty, childBounds, ref children);

		}

		private static void CopyBounds(BlobBuilder builder, IReadOnlyList<ColliderBounds> src, ref BlobArray<BlobPtr<ColliderBounds>> dest)
		{
			var boundsBlob = builder.Allocate(ref dest, src.Count);
			for (var i = 0; i < src.Count; i++) {
				ref var bounds = ref builder.Allocate(ref boundsBlob[i]);
				bounds.Aabb = src[i].Aabb;
				bounds.ColliderEntity = src[i].ColliderEntity;
				bounds.ColliderId = src[i].ColliderId;
			}
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

			for (var i = 0; i < _bounds.Length; i++) {
				ref var bounds = ref _bounds[i].Value;
				if (bounds.Aabb.IntersectRect(ballAabb) && bounds.Aabb.IntersectSphere(ball.Position, collisionRadiusSqr)) {
					matchedColliderIds.Add(new OverlappingStaticColliderBufferElement { Value = bounds.ColliderId });
				}
			}

			if (!_isLeaf) {
				var isLeft = ballAabb.Left <= _center.x;
				var isRight = ballAabb.Right >= _center.x;

				if (ballAabb.Top <= _center.y) {
					// Top
					if (isLeft) {
						_children[0].Value.GetAabbOverlaps(in ball, ref matchedColliderIds);
					}

					if (isRight) {
						_children[1].Value.GetAabbOverlaps(in ball, ref matchedColliderIds);
					}
				}

				if (ballAabb.Bottom >= _center.y) {
					// Bottom
					if (isLeft) {
						_children[2].Value.GetAabbOverlaps(in ball, ref matchedColliderIds);
					}

					if (isRight) {
						_children[3].Value.GetAabbOverlaps(in ball, ref matchedColliderIds);
					}
				}
			}
		}
	}
}
