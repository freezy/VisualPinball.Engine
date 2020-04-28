using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct KdNode
	{
		public Aabb RectBounds;                               // m_rectbounds
		public int Start;                                                      // m_start

		/// <summary>
		/// Contains the 2 bits for axis (bits 30/31)
		/// </summary>
		public int Items;                                                      // m_items

		/// <summary>
		/// If NULL, is a leaf; otherwise keeps the 2 children
		/// </summary>
		private NativeSlice<KdNode> _children;                                 // m_children

		public void Reset()
		{
			_children = new NativeSlice<KdNode>();
			Start = 0;
			Items = 0;
		}

		public void CreateNextLevel(int level, int levelEmpty, KdRoot hitOct) {
			var orgItems = Items & 0x3FFFFFFF;

			// !! magic
			if (orgItems <= 4 || level >= 128 / 2) {
				return;
			}

			var vDiag = new float3(
				RectBounds.Right - RectBounds.Left,
				RectBounds.Bottom - RectBounds.Top,
				RectBounds.ZHigh - RectBounds.ZLow
			);

			int axis;
			if (vDiag.x > vDiag.y && vDiag.x > vDiag.z) {
				if (vDiag.x < 0.0001) {
					return;
				}
				axis = 0;

			} else if (vDiag.y > vDiag.z) {
				if (vDiag.y < 0.0001) {
					return;
				}
				axis = 1;

			} else {
				if (vDiag.z < 0.0001) {
					return;
				}
				axis = 2;
			}

			//!! weight this with ratio of elements going to middle vs left&right! (avoids volume split that goes directly through object)

			// create children
			_children = hitOct.AllocTwoNodes();
			if (_children.Length == 0) {
				// ran out of nodes - abort
				return;
			}

			var child0 = new KdNode();
			var child1 = new KdNode();

			child0.RectBounds = RectBounds;
			child1.RectBounds = RectBounds;

			var vCenter = new float3(
				(RectBounds.Left + RectBounds.Right) * 0.5f,
				(RectBounds.Top + RectBounds.Bottom) * 0.5f,
				(RectBounds.ZLow + RectBounds.ZHigh) * 0.5f
			);
			switch (axis) {
				case 0:
					child0.RectBounds.Right = vCenter.x;
					child1.RectBounds.Left = vCenter.x;
					break;
				case 1:
					child0.RectBounds.Bottom = vCenter.y;
					child1.RectBounds.Top = vCenter.y;
					break;
				default:
					child0.RectBounds.ZHigh = vCenter.z;
					child1.RectBounds.ZLow = vCenter.z;
					break;
			}

			child0.Items = 0;
			child0._children = new NativeSlice<KdNode>();
			child1.Items = 0;
			child1._children = new NativeSlice<KdNode>();

			// determine amount of items that cross splitplane, or are passed on to the children
			if (axis == 0) {
				for (var i = Start; i < Start + orgItems; ++i) {
					var bounds = hitOct.GetItemAt(i);

					if (bounds.Right < vCenter.x) {
						child0.Items++;

					} else if (bounds.Left > vCenter.x) {
						child1.Items++;
					}
				}

			} else if (axis == 1) {
				for (var i = Start; i < Start + orgItems; ++i) {
					var bounds = hitOct.GetItemAt(i);

					if (bounds.Bottom < vCenter.y) {
						child0.Items++;

					} else if (bounds.Top > vCenter.y) {
						child1.Items++;
					}
				}

			} else {
				// axis == 2
				for (var i = Start; i < Start + orgItems; ++i) {
					var bounds = hitOct.GetItemAt(i);

					if (bounds.ZHigh < vCenter.z) {
						child0.Items++;

					} else if (bounds.ZLow > vCenter.z) {
						child1.Items++;
					}
				}
			}

			// check if at least two nodes feature objects, otherwise don"t bother subdividing further
			var countEmpty = 0;
			if (child0.Items == 0) {
				countEmpty = 1;
			}

			if (child1.Items == 0) {
				++countEmpty;
			}

			if (orgItems - child0.Items - child1.Items == 0) {
				++countEmpty;
			}

			if (countEmpty >= 2) {
				++levelEmpty;

			} else {
				levelEmpty = 0;
			}

			if (levelEmpty > 8) {
				// If 8 levels were all just subdividing the same objects without luck, exit & Free the nodes again (but at least empty space was cut off)
				hitOct.NumNodes -= 2;
				_children = new NativeSlice<KdNode>();
				return;
			}

			child0.Start = Start + orgItems - child0.Items - child1.Items;
			child1.Start = child0.Start + child0.Items;

			var items = 0;
			child0.Items = 0;
			child1.Items = 0;

			switch (axis) {

				// sort items that cross splitplane in-place, the others are sorted into a temporary
				case 0: {
					for (var i = Start; i < Start + orgItems; ++i) {
						var bounds = hitOct.GetItemAt(i);

						if (bounds.Right < vCenter.x) {
							hitOct.Indices[child0.Start + child0.Items++] = hitOct.OrgIdx[i];

						} else if (bounds.Left > vCenter.x) {
							hitOct.Indices[child1.Start + child1.Items++] = hitOct.OrgIdx[i];

						} else {
							hitOct.OrgIdx[Start + items++] = hitOct.OrgIdx[i];
						}
					}
					break;
				}

				case 1: {
					for (var i = Start; i < Start + orgItems; ++i) {
						var bounds = hitOct.GetItemAt(i);

						if (bounds.Bottom < vCenter.y) {
							hitOct.Indices[child0.Start + child0.Items++] = hitOct.OrgIdx[i];

						} else if (bounds.Top > vCenter.y) {
							hitOct.Indices[child1.Start + child1.Items++] = hitOct.OrgIdx[i];

						} else {
							hitOct.OrgIdx[Start + items++] = hitOct.OrgIdx[i];
						}
					}
					break;
				}

				default: { // axis == 2

					for (var i = Start; i < Start + orgItems; ++i) {
						var bounds = hitOct.GetItemAt(i);

						if (bounds.ZHigh < vCenter.z) {
							hitOct.Indices[child0.Start + child0.Items++] = hitOct.OrgIdx[i];

						} else if (bounds.ZLow > vCenter.z) {
							hitOct.Indices[child1.Start + child1.Items++] = hitOct.OrgIdx[i];

						} else {
							hitOct.OrgIdx[Start + items++] = hitOct.OrgIdx[i];
						}
					}
					break;
				}
			}

			// The following assertions hold after this step:
			//assert( this.Start + items == this.Children[0].This.Start );
			//assert( this.Children[0].This.Start + this.Children[0].This.Items == this.Children[1].This.Start );
			//assert( this.Children[1].This.Start + this.Children[1].This.Items == this.Start + org_items );
			//assert( this.Start + org_items <= this.HitOct->tmp.Size() );

			Items = items | (axis << 30);

			// copy temporary back //!! could omit this by doing everything inplace
			for (var i = 0; i < child0.Items; i++) {
				hitOct.OrgIdx[child0.Start + i] = hitOct.Indices[child0.Start + i];
			}

			for (var i = 0; i < child1.Items; i++) {
				hitOct.OrgIdx[child1.Start + i] = hitOct.Indices[child1.Start + i];
			}
			//memcpy(&this.HitOct->m_org_idx[this.Children[0].Start], &this.HitOct->tmp[this.Children[0].Start], this.Children[0].Items*sizeof(unsigned int));
			//memcpy(&this.HitOct->m_org_idx[this.Children[1].Start], &this.HitOct->tmp[this.Children[1].Start], this.Children[1].This.Items*sizeof(unsigned int));

			_children[0] = child0;
			_children[1] = child1;

			child0.CreateNextLevel(level + 1, levelEmpty, hitOct);
			child1.CreateNextLevel(level + 1, levelEmpty, hitOct);
		}

		public void GetAabbOverlaps(ref KdRoot hitOct, in Entity entity, in BallData ball, ref DynamicBuffer<MatchedBallColliderBufferElement> matchedColliderIds) {

			var orgItems = Items & 0x3FFFFFFF;
			var axis = Items >> 30;
			var bounds = ball.Aabb;
			var collisionRadiusSqr = ball.CollisionRadiusSqr;

			for (var i = Start; i < Start + orgItems; i++) {
				var aabb = hitOct.GetItemAt(i);
				if (entity != aabb.ColliderEntity && aabb.IntersectSphere(ball.Position, collisionRadiusSqr)) {
					matchedColliderIds.Add(new MatchedBallColliderBufferElement { Value = aabb.ColliderEntity });
				}
			}

			if (_children.Length > 0) {
				switch (axis) {
					// not a leaf
					case 0: {
						var vCenter = (RectBounds.Left + RectBounds.Right) * 0.5f;
						if (bounds.Left <= vCenter) {
							_children[0].GetAabbOverlaps(ref hitOct, in entity, in ball, ref matchedColliderIds);
						}

						if (bounds.Right >= vCenter) {
							_children[1].GetAabbOverlaps(ref hitOct, in entity, in ball, ref matchedColliderIds);
						}
						break;
					}

					case 1: {
						var vCenter = (RectBounds.Top + RectBounds.Bottom) * 0.5f;
						if (bounds.Top <= vCenter) {
							_children[0].GetAabbOverlaps(ref hitOct, in entity, in ball, ref matchedColliderIds);
						}

						if (bounds.Bottom >= vCenter) {
							_children[1].GetAabbOverlaps(ref hitOct, in entity, in ball, ref matchedColliderIds);
						}
						break;
					}

					default: {
						var vCenter = (RectBounds.ZLow + RectBounds.ZHigh) * 0.5f;
						if (bounds.ZLow <= vCenter) {
							_children[0].GetAabbOverlaps(ref hitOct, in entity, in ball, ref matchedColliderIds);
						}

						if (bounds.ZHigh >= vCenter) {
							_children[1].GetAabbOverlaps(ref hitOct, in entity, in ball, ref matchedColliderIds);
						}
						break;
					}
				}
			}
		}
	}
}
