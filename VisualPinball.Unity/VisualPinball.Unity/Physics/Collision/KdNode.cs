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
		private KdNode[] _children;                                 // m_children

		public void Reset()
		{
			_children = null;
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
			if (_children == null) {
				// ran out of nodes - abort
				return;
			}

			_children[0].RectBounds = RectBounds;
			_children[1].RectBounds = RectBounds;

			var vCenter = new float3(
				(RectBounds.Left + RectBounds.Right) * 0.5f,
				(RectBounds.Top + RectBounds.Bottom) * 0.5f,
				(RectBounds.ZLow + RectBounds.ZHigh) * 0.5f
			);
			switch (axis) {
				case 0:
					_children[0].RectBounds.Right = vCenter.x;
					_children[1].RectBounds.Left = vCenter.x;
					break;
				case 1:
					_children[0].RectBounds.Bottom = vCenter.y;
					_children[1].RectBounds.Top = vCenter.y;
					break;
				default:
					_children[0].RectBounds.ZHigh = vCenter.z;
					_children[1].RectBounds.ZLow = vCenter.z;
					break;
			}

			_children[0].Items = 0;
			_children[0]._children = null;
			_children[1].Items = 0;
			_children[1]._children = null;

			// determine amount of items that cross splitplane, or are passed on to the children
			if (axis == 0) {
				for (var i = Start; i < Start + orgItems; ++i) {
					ref var bounds = ref hitOct.GetItemAt(i);

					if (bounds.Right < vCenter.x) {
						_children[0].Items++;

					} else if (bounds.Left > vCenter.x) {
						_children[1].Items++;
					}
				}

			} else if (axis == 1) {
				for (var i = Start; i < Start + orgItems; ++i) {
					ref var bounds = ref hitOct.GetItemAt(i);

					if (bounds.Bottom < vCenter.y) {
						_children[0].Items++;

					} else if (bounds.Top > vCenter.y) {
						_children[1].Items++;
					}
				}

			} else {
				// axis == 2
				for (var i = Start; i < Start + orgItems; ++i) {
					ref var bounds = ref hitOct.GetItemAt(i);

					if (bounds.ZHigh < vCenter.z) {
						_children[0].Items++;

					} else if (bounds.ZLow > vCenter.z) {
						_children[1].Items++;
					}
				}
			}

			// check if at least two nodes feature objects, otherwise don"t bother subdividing further
			var countEmpty = 0;
			if (_children[0].Items == 0) {
				countEmpty = 1;
			}

			if (_children[1].Items == 0) {
				++countEmpty;
			}

			if (orgItems - _children[0].Items - _children[1].Items == 0) {
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
				_children = null;
				return;
			}

			_children[0].Start = Start + orgItems - _children[0].Items - _children[1].Items;
			_children[1].Start = _children[0].Start + _children[0].Items;

			var items = 0;
			_children[0].Items = 0;
			_children[1].Items = 0;

			switch (axis) {

				// sort items that cross splitplane in-place, the others are sorted into a temporary
				case 0: {
					for (var i = Start; i < Start + orgItems; ++i) {
						ref var bounds = ref hitOct.GetItemAt(i);

						if (bounds.Right < vCenter.x) {
							hitOct.Indices[_children[0].Start + _children[0].Items++] = hitOct.OrgIdx[i];

						} else if (bounds.Left > vCenter.x) {
							hitOct.Indices[_children[1].Start + _children[1].Items++] = hitOct.OrgIdx[i];

						} else {
							hitOct.OrgIdx[Start + items++] = hitOct.OrgIdx[i];
						}
					}
					break;
				}

				case 1: {
					for (var i = Start; i < Start + orgItems; ++i) {
						ref var bounds = ref hitOct.GetItemAt(i);

						if (bounds.Bottom < vCenter.y) {
							hitOct.Indices[_children[0].Start + _children[0].Items++] = hitOct.OrgIdx[i];

						} else if (bounds.Top > vCenter.y) {
							hitOct.Indices[_children[1].Start + _children[1].Items++] = hitOct.OrgIdx[i];

						} else {
							hitOct.OrgIdx[Start + items++] = hitOct.OrgIdx[i];
						}
					}
					break;
				}

				default: { // axis == 2

					for (var i = Start; i < Start + orgItems; ++i) {
						ref var bounds = ref hitOct.GetItemAt(i);

						if (bounds.ZHigh < vCenter.z) {
							hitOct.Indices[_children[0].Start + _children[0].Items++] = hitOct.OrgIdx[i];

						} else if (bounds.ZLow > vCenter.z) {
							hitOct.Indices[_children[1].Start + _children[1].Items++] = hitOct.OrgIdx[i];

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
			for (var i = 0; i < _children[0].Items; i++) {
				hitOct.OrgIdx[_children[0].Start + i] = hitOct.Indices[_children[0].Start + i];
			}

			for (var i = 0; i < _children[1].Items; i++) {
				hitOct.OrgIdx[_children[1].Start + i] = hitOct.Indices[_children[1].Start + i];
			}
			//memcpy(&this.HitOct->m_org_idx[this.Children[0].Start], &this.HitOct->tmp[this.Children[0].Start], this.Children[0].Items*sizeof(unsigned int));
			//memcpy(&this.HitOct->m_org_idx[this.Children[1].Start], &this.HitOct->tmp[this.Children[1].Start], this.Children[1].This.Items*sizeof(unsigned int));

			_children[0].CreateNextLevel(level + 1, levelEmpty, hitOct);
			_children[1].CreateNextLevel(level + 1, levelEmpty, hitOct);
		}

		public void GetAabbOverlaps(ref KdRoot hitOct, in Entity entity, in BallData ball, ref DynamicBuffer<MatchedBallColliderBufferElement> matchedColliderIds) {

			var orgItems = Items & 0x3FFFFFFF;
			var axis = Items >> 30;
			var bounds = ball.Aabb;
			var collisionRadiusSqr = ball.CollisionRadiusSqr;

			for (var i = Start; i < Start + orgItems; i++) {
				ref var aabb = ref hitOct.GetItemAt(i);
				if (entity != aabb.ColliderEntity && aabb.IntersectSphere(ball.Position, collisionRadiusSqr)) {
					matchedColliderIds.Add(new MatchedBallColliderBufferElement { Value = aabb.ColliderEntity });
				}
			}

			if (_children != null) {
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
