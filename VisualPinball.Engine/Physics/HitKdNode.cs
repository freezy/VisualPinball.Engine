// ReSharper disable CommentTypo

using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitKdNode
	{
		public Rect3D RectBounds = new Rect3D(true);                               // m_rectbounds
		public int Start;                                                      // m_start

		/// <summary>
		/// Contains the 2 bits for axis (bits 30/31)
		/// </summary>
		public int Items;                                                      // m_items

		/// <summary>
		/// If NULL, is a leaf; otherwise keeps the 2 children
		/// </summary>
		private HitKdNode[] _children;                                         // m_children

		public void Reset()
		{
			_children = null;
			Start = 0;
			Items = 0;
		}

		public void HitTestBall(Ball ball, CollisionEvent coll, PlayerPhysics physics, HitKd hitOct) {

			var orgItems = Items & 0x3FFFFFFF;
			var axis = Items >> 30;

			for (var i = Start; i < Start + orgItems; i++) {
				var pho = hitOct.GetItemAt(i);
				if (ball.Hit != pho && pho.HitBBox.IntersectSphere(ball.State.Pos, ball.Hit.HitRadiusSqr)) {
					pho.DoHitTest(ball, coll, physics);
				}
			}

			if (_children != null) {
				switch (axis) {
					// not a leaf
					case 0: {
						var vCenter = (RectBounds.Left + RectBounds.Right) * 0.5f;
						if (ball.Hit.HitBBox.Left <= vCenter) {
							_children[0].HitTestBall(ball, coll, physics, hitOct);
						}

						if (ball.Hit.HitBBox.Right >= vCenter) {
							_children[1].HitTestBall(ball, coll, physics, hitOct);
						}
						break;
					}

					case 1: {
						var vCenter = (RectBounds.Top + RectBounds.Bottom) * 0.5f;
						if (ball.Hit.HitBBox.Top <= vCenter) {
							_children[0].HitTestBall(ball, coll, physics, hitOct);
						}

						if (ball.Hit.HitBBox.Bottom >= vCenter) {
							_children[1].HitTestBall(ball, coll, physics, hitOct);
						}
						break;
					}

					default: {
						var vCenter = (RectBounds.ZLow + RectBounds.ZHigh) * 0.5f;
						if (ball.Hit.HitBBox.ZLow <= vCenter) {
							_children[0].HitTestBall(ball, coll, physics, hitOct);
						}

						if (ball.Hit.HitBBox.ZHigh >= vCenter) {
							_children[1].HitTestBall(ball, coll, physics, hitOct);
						}
						break;
					}
				}
			}
		}

		/* istanbul ignore never next executed below the "magic" check (https://www.vpforums.org/index.php?showtopic=42690) */
		public void CreateNextLevel(int level, int levelEmpty, HitKd hitOct) {
			var orgItems = Items & 0x3FFFFFFF;

			// !! magic
			if (orgItems <= 4 || level >= 128 / 2) {
				return;
			}

			var vDiag = new Vertex3D(
				RectBounds.Right - RectBounds.Left,
				RectBounds.Bottom - RectBounds.Top,
				RectBounds.ZHigh - RectBounds.ZLow
			);

			int axis;
			if (vDiag.X > vDiag.Y && vDiag.X > vDiag.Z) {
				if (vDiag.X < 0.0001) {
					return;
				}
				axis = 0;

			} else if (vDiag.Y > vDiag.Z) {
				if (vDiag.Y < 0.0001) {
					return;
				}
				axis = 1;

			} else {
				if (vDiag.Z < 0.0001) {
					return;
				}

				axis = 2;
			}

			//!! weight this with ratio of elements going to middle vs left&right! (avoids volume split that goes directly through object)

			// create children, calc bboxes
			_children = hitOct.AllocTwoNodes();
			if (_children.Length == 0) {
				// ran out of nodes - abort
				return;
			}

			_children[0].RectBounds = RectBounds;
			_children[1].RectBounds = RectBounds;

			var vCenter = new Vertex3D(
				(RectBounds.Left + RectBounds.Right) * 0.5f,
				(RectBounds.Top + RectBounds.Bottom) * 0.5f,
				(RectBounds.ZLow + RectBounds.ZHigh) * 0.5f
			);
			if (axis == 0) {
				_children[0].RectBounds.Right = vCenter.X;
				_children[1].RectBounds.Left = vCenter.X;

			} else if (axis == 1) {
				_children[0].RectBounds.Bottom = vCenter.Y;
				_children[1].RectBounds.Top = vCenter.Y;

			} else {
				_children[0].RectBounds.ZHigh = vCenter.Z;
				_children[1].RectBounds.ZLow = vCenter.Z;
			}

			_children[0].Items = 0;
			_children[0]._children = null;
			_children[1].Items = 0;
			_children[1]._children = null;

			// determine amount of items that cross splitplane, or are passed on to the children
			if (axis == 0) {
				for (var i = Start; i < Start + orgItems; ++i) {
					var pho = hitOct.GetItemAt(i);

					if (pho.HitBBox.Right < vCenter.X) {
						_children[0].Items++;

					} else if (pho.HitBBox.Left > vCenter.X) {
						_children[1].Items++;
					}
				}

			} else if (axis == 1) {
				for (var i = Start; i < Start + orgItems; ++i) {
					var pho = hitOct.GetItemAt(i);

					if (pho.HitBBox.Bottom < vCenter.Y) {
						_children[0].Items++;

					} else if (pho.HitBBox.Top > vCenter.Y) {
						_children[1].Items++;
					}
				}

			} else {
				// axis == 2
				for (var i = Start; i < Start + orgItems; ++i) {
					var pho = hitOct.GetItemAt(i);

					if (pho.HitBBox.ZHigh < vCenter.Z) {
						_children[0].Items++;

					} else if (pho.HitBBox.ZLow > vCenter.Z) {
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
						var pho = hitOct.GetItemAt(i);

						if (pho.HitBBox.Right < vCenter.X) {
							hitOct.Indices[_children[0].Start + _children[0].Items++] = hitOct.OrgIdx[i];

						} else if (pho.HitBBox.Left > vCenter.X) {
							hitOct.Indices[_children[1].Start + _children[1].Items++] = hitOct.OrgIdx[i];

						} else {
							hitOct.OrgIdx[Start + items++] = hitOct.OrgIdx[i];
						}
					}

					break;
				}

				case 1: {
					for (var i = Start; i < Start + orgItems; ++i) {
						var pho = hitOct.GetItemAt(i);

						if (pho.HitBBox.Bottom < vCenter.Y) {
							hitOct.Indices[_children[0].Start + _children[0].Items++] = hitOct.OrgIdx[i];

						} else if (pho.HitBBox.Top > vCenter.Y) {
							hitOct.Indices[_children[1].Start + _children[1].Items++] = hitOct.OrgIdx[i];

						} else {
							hitOct.OrgIdx[Start + items++] = hitOct.OrgIdx[i];
						}
					}

					break;
				}

				default: { // axis == 2

					for (var i = Start; i < Start + orgItems; ++i) {
						var pho = hitOct.GetItemAt(i);

						if (pho.HitBBox.ZHigh < vCenter.Z) {
							hitOct.Indices[_children[0].Start + _children[0].Items++] = hitOct.OrgIdx[i];

						} else if (pho.HitBBox.ZLow > vCenter.Z) {
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
	}
}
