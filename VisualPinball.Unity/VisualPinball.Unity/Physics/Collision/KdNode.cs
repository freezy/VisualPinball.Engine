using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public struct KdNode
	{
		public Aabb Bounds;                                                    // m_rectbounds
		public int Start;                                                      // m_start

		/// <summary>
		/// Contains the 2 bits for axis (bits 30/31)
		/// </summary>
		public int Items;                                                      // m_items

		/// <summary>
		/// If NULL, is a leaf; otherwise keeps the 2 children
		/// </summary>
		private int _childA;                                                   // m_children
		private int _childB;

		private bool HasChildren => _childA > -1 && _childB > -1;

		public KdNode(Aabb bounds)
		{
			Bounds = bounds;
			Start = 0;
			Items = 2;
			_childA = -1;
			_childB = -1;
		}

		public void Reset()
		{
			_childA = -1;
			_childB = -1;
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
				Bounds.Right - Bounds.Left,
				Bounds.Bottom - Bounds.Top,
				Bounds.ZHigh - Bounds.ZLow
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
			if (!hitOct.HasNodesAvailable()) {
				// ran out of nodes - abort
				return;
			}

			var childA = new KdNode(Bounds);
			var childB = new KdNode(Bounds);

			var vCenter = new float3(
				(Bounds.Left + Bounds.Right) * 0.5f,
				(Bounds.Top + Bounds.Bottom) * 0.5f,
				(Bounds.ZLow + Bounds.ZHigh) * 0.5f
			);
			switch (axis) {
				case 0:
					childA.Bounds.Right = vCenter.x;
					childB.Bounds.Left = vCenter.x;
					break;
				case 1:
					childA.Bounds.Bottom = vCenter.y;
					childB.Bounds.Top = vCenter.y;
					break;
				default:
					childA.Bounds.ZHigh = vCenter.z;
					childB.Bounds.ZLow = vCenter.z;
					break;
			}

			childA.Reset();
			childB.Reset();

			// determine amount of items that cross splitplane, or are passed on to the children
			if (axis == 0) {
				for (var i = Start; i < Start + orgItems; ++i) {
					var bounds = hitOct.GetItemAt(i);

					if (bounds.Right < vCenter.x) {
						childA.Items++;

					} else if (bounds.Left > vCenter.x) {
						childB.Items++;
					}
				}

			} else if (axis == 1) {
				for (var i = Start; i < Start + orgItems; ++i) {
					var bounds = hitOct.GetItemAt(i);

					if (bounds.Bottom < vCenter.y) {
						childA.Items++;

					} else if (bounds.Top > vCenter.y) {
						childB.Items++;
					}
				}

			} else {
				// axis == 2
				for (var i = Start; i < Start + orgItems; ++i) {
					var bounds = hitOct.GetItemAt(i);

					if (bounds.ZHigh < vCenter.z) {
						childA.Items++;

					} else if (bounds.ZLow > vCenter.z) {
						childB.Items++;
					}
				}
			}

			// check if at least two nodes feature objects, otherwise don"t bother subdividing further
			var countEmpty = 0;
			if (childA.Items == 0) {
				countEmpty = 1;
			}

			if (childB.Items == 0) {
				++countEmpty;
			}

			if (orgItems - childA.Items - childB.Items == 0) {
				++countEmpty;
			}

			if (countEmpty >= 2) {
				++levelEmpty;

			} else {
				levelEmpty = 0;
			}

			if (levelEmpty > 8) {
				// If 8 levels were all just subdividing the same objects without luck, exit & Free the nodes again (but at least empty space was cut off)
				// no need to update NumNodes, since we didn't increment them yet.
				_childA = -1;
				_childB = -1;
				return;
			}

			childA.Start = Start + orgItems - childA.Items - childB.Items;
			childB.Start = childA.Start + childA.Items;

			var items = 0;
			childA.Items = 0;
			childB.Items = 0;

			switch (axis) {

				// sort items that cross splitplane in-place, the others are sorted into a temporary
				case 0: {
					for (var i = Start; i < Start + orgItems; ++i) {
						var bounds = hitOct.GetItemAt(i);

						if (bounds.Right < vCenter.x) {
							hitOct.Indices[childA.Start + childA.Items++] = hitOct.OrgIdx[i];

						} else if (bounds.Left > vCenter.x) {
							hitOct.Indices[childB.Start + childB.Items++] = hitOct.OrgIdx[i];

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
							hitOct.Indices[childA.Start + childA.Items++] = hitOct.OrgIdx[i];

						} else if (bounds.Top > vCenter.y) {
							hitOct.Indices[childB.Start + childB.Items++] = hitOct.OrgIdx[i];

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
							hitOct.Indices[childA.Start + childA.Items++] = hitOct.OrgIdx[i];

						} else if (bounds.ZLow > vCenter.z) {
							hitOct.Indices[childB.Start + childB.Items++] = hitOct.OrgIdx[i];

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
			for (var i = 0; i < childA.Items; i++) {
				hitOct.OrgIdx[childA.Start + i] = hitOct.Indices[childA.Start + i];
			}
			for (var i = 0; i < childB.Items; i++) {
				hitOct.OrgIdx[childB.Start + i] = hitOct.Indices[childB.Start + i];
			}

			hitOct.AddNodes(childA, childB, out _childA, out _childB);

			childA.CreateNextLevel(level + 1, levelEmpty, hitOct);
			childB.CreateNextLevel(level + 1, levelEmpty, hitOct);
		}

		public void GetAabbOverlaps(ref KdRoot hitOct, in Entity entity, in BallData ball, ref DynamicBuffer<OverlappingDynamicBufferElement> overlappingEntities) {

			var orgItems = Items & 0x3FFFFFFF;
			var axis = Items >> 30;
			var bounds = ball.Aabb;
			var collisionRadiusSqr = ball.CollisionRadiusSqr;

			for (var i = Start; i < Start + orgItems; i++) {
				var aabb = hitOct.GetItemAt(i);
				if (entity != aabb.ColliderEntity && aabb.IntersectSphere(ball.Position, collisionRadiusSqr)) {
					overlappingEntities.Add(new OverlappingDynamicBufferElement { Value = aabb.ColliderEntity });
				}
			}

			if (HasChildren) {
				switch (axis) {
					// not a leaf
					case 0: {
						var vCenter = (Bounds.Left + Bounds.Right) * 0.5f;
						if (bounds.Left <= vCenter) {
							hitOct.GetNodeAt(_childA).GetAabbOverlaps(ref hitOct, in entity, in ball, ref overlappingEntities);
						}

						if (bounds.Right >= vCenter) {
							hitOct.GetNodeAt(_childB).GetAabbOverlaps(ref hitOct, in entity, in ball, ref overlappingEntities);
						}
						break;
					}

					case 1: {
						var vCenter = (Bounds.Top + Bounds.Bottom) * 0.5f;
						if (bounds.Top <= vCenter) {
							hitOct.GetNodeAt(_childA).GetAabbOverlaps(ref hitOct, in entity, in ball, ref overlappingEntities);
						}

						if (bounds.Bottom >= vCenter) {
							hitOct.GetNodeAt(_childB).GetAabbOverlaps(ref hitOct, in entity, in ball, ref overlappingEntities);
						}
						break;
					}

					default: {
						var vCenter = (Bounds.ZLow + Bounds.ZHigh) * 0.5f;
						if (bounds.ZLow <= vCenter) {
							hitOct.GetNodeAt(_childA).GetAabbOverlaps(ref hitOct, in entity, in ball, ref overlappingEntities);
						}

						if (bounds.ZHigh >= vCenter) {
							hitOct.GetNodeAt(_childB).GetAabbOverlaps(ref hitOct, in entity, in ball, ref overlappingEntities);
						}
						break;
					}
				}
			}
		}
	}
}
