using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitKdNode
	{
		public Rect3D RectBounds = new Rect3D();
		public int Start;
		public int Items; // contains the 2 bits for axis (bits 30/31)

		private HitKd HitOct; //!! meh, stupid
		private HitKdNode[] Children; // if NULL, is a leaf; otherwise keeps the 2 children

		public HitKdNode(HitKd hitOct)
		{
			HitOct = hitOct;
		}

		public void Reset(HitKd hitOct)
		{
			Children = null;
			HitOct = hitOct;
			Start = 0;
			Items = 0;
		}

		public void HitTestBall(Ball ball, CollisionEvent coll, PlayerPhysics physics) {

			var orgItems = Items & 0x3FFFFFFF;
			var axis = Items >> 30;

			for (var i = Start; i < Start + orgItems; i++) {
				var pho = HitOct.GetItemAt(i);
				if (ball.Hit != pho && pho.HitBBox.IntersectSphere(ball.State.Pos, ball.Hit.RcHitRadiusSqr)) {
					pho.DoHitTest(ball, coll, physics);
				}
			}

			// TODO never executed (https://www.Vpforums.Org/index.Php?showtopic=42690)
			if (Children != null) { // not a leaf

				// not a leaf
				if (axis == 0) {
					var vCenter = (RectBounds.Left + RectBounds.Right) * 0.5f;
					if (ball.Hit.HitBBox.Left <= vCenter) {
						Children[0].HitTestBall(ball, coll, physics);
					}

					if (ball.Hit.HitBBox.Right >= vCenter) {
						Children[1].HitTestBall(ball, coll, physics);
					}

				} else if (axis == 1) {
					var vCenter = (RectBounds.Top + RectBounds.Bottom) * 0.5f;
					if (ball.Hit.HitBBox.Top <= vCenter) {
						Children[0].HitTestBall(ball, coll, physics);
					}

					if (ball.Hit.HitBBox.Bottom >= vCenter) {
						Children[1].HitTestBall(ball, coll, physics);
					}

				} else {
					var vCenter = (RectBounds.ZLow + RectBounds.ZHigh) * 0.5f;
					if (ball.Hit.HitBBox.ZLow <= vCenter) {
						Children[0].HitTestBall(ball, coll, physics);
					}

					if (ball.Hit.HitBBox.ZHigh >= vCenter) {
						Children[1].HitTestBall(ball, coll, physics);
					}
				}
			}
		}

		/* istanbul ignore never next executed below the "magic" check (https://www.Vpforums.Org/index.Php?showtopic=42690) */
		public void CreateNextLevel(int level, int levelEmpty) {
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
			Children = HitOct.AllocTwoNodes();
			if (Children.Length == 0) {
				// ran out of nodes - abort
				return;
			}

			Children[0].RectBounds = RectBounds;
			Children[1].RectBounds = RectBounds;

			var vCenter = new Vertex3D(
				(RectBounds.Left + RectBounds.Right) * 0.5f,
				(RectBounds.Top + RectBounds.Bottom) * 0.5f,
				(RectBounds.ZLow + RectBounds.ZHigh) * 0.5f
			);
			if (axis == 0) {
				Children[0].RectBounds.Right = vCenter.X;
				Children[1].RectBounds.Left = vCenter.X;

			} else if (axis == 1) {
				Children[0].RectBounds.Bottom = vCenter.Y;
				Children[1].RectBounds.Top = vCenter.Y;

			} else {
				Children[0].RectBounds.ZHigh = vCenter.Z;
				Children[1].RectBounds.ZLow = vCenter.Z;
			}

			Children[0].HitOct = HitOct; //!! meh
			Children[0].Items = 0;
			Children[0].Children = null;
			Children[1].HitOct = HitOct; //!! meh
			Children[1].Items = 0;
			Children[1].Children = null;

			// determine amount of items that cross splitplane, or are passed on to the children
			if (axis == 0) {
				for (var i = Start; i < Start + orgItems; ++i) {
					var pho = HitOct.GetItemAt(i);

					if (pho.HitBBox.Right < vCenter.X) {
						Children[0].Items++;

					} else if (pho.HitBBox.Left > vCenter.X) {
						Children[1].Items++;
					}
				}

			} else if (axis == 1) {
				for (var i = Start; i < Start + orgItems; ++i) {
					var pho = HitOct.GetItemAt(i);

					if (pho.HitBBox.Bottom < vCenter.Y) {
						Children[0].Items++;

					} else if (pho.HitBBox.Top > vCenter.Y) {
						Children[1].Items++;
					}
				}

			} else {
				// axis == 2
				for (var i = Start; i < Start + orgItems; ++i) {
					var pho = HitOct.GetItemAt(i);

					if (pho.HitBBox.ZHigh < vCenter.Z) {
						Children[0].Items++;

					} else if (pho.HitBBox.ZLow > vCenter.Z) {
						Children[1].Items++;
					}
				}
			}

			// check if at least two nodes feature objects, otherwise don"t bother subdividing further
			var countEmpty = 0;
			if (Children[0].Items == 0) {
				countEmpty = 1;
			}

			if (Children[1].Items == 0) {
				++countEmpty;
			}

			if (orgItems - Children[0].Items - Children[1].Items == 0) {
				++countEmpty;
			}

			if (countEmpty >= 2) {
				++levelEmpty;

			} else {
				levelEmpty = 0;
			}

			if (levelEmpty > 8) {
				// If 8 levels were all just subdividing the same objects without luck, exit & Free the nodes again (but at least empty space was cut off)
				HitOct.NumNodes -= 2;
				Children = null;
				return;
			}

			Children[0].Start = Start + orgItems - Children[0].Items - Children[1].Items;
			Children[1].Start = Children[0].Start + Children[0].Items;

			var items = 0;
			Children[0].Items = 0;
			Children[1].Items = 0;

			switch (axis) {

				// sort items that cross splitplane in-place, the others are sorted into a temporary
				case 0: {
					for (var i = Start; i < Start + orgItems; ++i) {
						var pho = HitOct.GetItemAt(i);

						if (pho.HitBBox.Right < vCenter.X) {
							HitOct.tmp[Children[0].Start + Children[0].Items++] = HitOct.OrgIdx[i];

						} else if (pho.HitBBox.Left > vCenter.X) {
							HitOct.tmp[Children[1].Start + Children[1].Items++] = HitOct.OrgIdx[i];

						} else {
							HitOct.OrgIdx[Start + items++] = HitOct.OrgIdx[i];
						}
					}

					break;
				}

				case 1: {
					for (var i = Start; i < Start + orgItems; ++i) {
						var pho = HitOct.GetItemAt(i);

						if (pho.HitBBox.Bottom < vCenter.Y) {
							HitOct.tmp[Children[0].Start + Children[0].Items++] = HitOct.OrgIdx[i];

						} else if (pho.HitBBox.Top > vCenter.Y) {
							HitOct.tmp[Children[1].Start + Children[1].Items++] = HitOct.OrgIdx[i];

						} else {
							HitOct.OrgIdx[Start + items++] = HitOct.OrgIdx[i];
						}
					}

					break;
				}

				default: { // axis == 2

					for (var i = Start; i < Start + orgItems; ++i) {
						var pho = HitOct.GetItemAt(i);

						if (pho.HitBBox.ZHigh < vCenter.Z) {
							HitOct.tmp[Children[0].Start + Children[0].Items++] = HitOct.OrgIdx[i];

						} else if (pho.HitBBox.ZLow > vCenter.Z) {
							HitOct.tmp[Children[1].Start + Children[1].Items++] = HitOct.OrgIdx[i];

						} else {
							HitOct.OrgIdx[Start + items++] = HitOct.OrgIdx[i];
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
			for (var i = 0; i < Children[0].Items; i++) {
				HitOct.OrgIdx[Children[0].Start + i] = HitOct.tmp[Children[0].Start + i];
			}

			for (var i = 0; i < Children[1].Items; i++) {
				HitOct.OrgIdx[Children[1].Start + i] = HitOct.tmp[Children[1].Start + i];
			}
			//memcpy(&this.HitOct->m_org_idx[this.Children[0].Start], &this.HitOct->tmp[this.Children[0].Start], this.Children[0].Items*sizeof(unsigned int));
			//memcpy(&this.HitOct->m_org_idx[this.Children[1].Start], &this.HitOct->tmp[this.Children[1].Start], this.Children[1].This.Items*sizeof(unsigned int));

			Children[0].CreateNextLevel(level + 1, levelEmpty);
			Children[1].CreateNextLevel(level + 1, levelEmpty);
		}
	}
}
