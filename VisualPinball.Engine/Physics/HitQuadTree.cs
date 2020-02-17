using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitQuadTree
	{
		private EventProxy _unique; // everything below/including this node shares the same original primitive object (just for early outs if not collidable)

		private readonly HitQuadTree[] _children = new HitQuadTree[4];
		private readonly Vertex3D _center = new Vertex3D();
		private List<HitObject> _hitObjects = new List<HitObject>();
		private bool _isLeaf = true;

		public void AddElement(HitObject pho)
		{
			_hitObjects.Add(pho);
		}

		public void Initialize()
		{
			var bounds = new Rect3D();
			foreach (var vho in _hitObjects) {
				bounds.Extend(vho.HitBBox);
			}
			CreateNextLevel(bounds, 0, 0);
		}

		public void Initialize(Rect3D bounds)
		{
			CreateNextLevel(bounds, 0, 0);
		}

		public void HitTestBall(Ball ball, CollisionEvent coll, PlayerPhysics physics)
		{
			foreach (var vho in _hitObjects) {
				if (ball.Hit != vho // ball can not hit itself
				    && vho.HitBBox.IntersectRect(ball.Hit.HitBBox)
				    && vho.HitBBox.IntersectSphere(ball.State.Pos, ball.Hit.RcHitRadiusSqr))
				{
					vho.DoHitTest(ball, coll, physics);
				}
			}

			if (!_isLeaf) {
				var isLeft = ball.Hit.HitBBox.Left <= _center.X;
				var isRight = ball.Hit.HitBBox.Right >= _center.X;

				if (ball.Hit.HitBBox.Top <= _center.Y) {
					// Top
					if (isLeft) {
						_children[0].HitTestBall(ball, coll, physics);
					}

					if (isRight) {
						_children[1].HitTestBall(ball, coll, physics);
					}
				}

				if (ball.Hit.HitBBox.Bottom >= _center.Y) {
					// Bottom
					if (isLeft) {
						_children[2].HitTestBall(ball, coll, physics);
					}

					if (isRight) {
						_children[3].HitTestBall(ball, coll, physics);
					}
				}
			}
		}

		private void CreateNextLevel(Rect3D bounds, int level, int levelEmpty)
		{
			if (_hitObjects.Count <= 4) {
				//!! magic
				return;
			}

			_isLeaf = false;

			_center.X = (bounds.Left + bounds.Right) * 0.5f;
			_center.Y = (bounds.Top + bounds.Bottom) * 0.5f;
			_center.Z = (bounds.ZLow + bounds.ZHigh) * 0.5f;

			for (var i = 0; i < 4; i++) {
				_children[i] = new HitQuadTree();
			}

			List<HitObject> vRemain = new List<HitObject>(); // hit objects which did not go to a quadrant

			// TODO check if casting in C++ results in null if not the cast type
			_unique = _hitObjects[0].e ? _hitObjects[0].Obj : null;

			// sort items into appropriate child nodes
			foreach (var pho in _hitObjects) {
				int oct;

				if ((pho.e ? pho.Obj : null) != _unique) {
					// are all objects in current node unique/belong to the same primitive?
					_unique = null;
				}

				if (pho.HitBBox.Right < _center.X) {
					oct = 0;

				} else if (pho.HitBBox.Left > _center.X) {
					oct = 1;

				} else {
					oct = 128;
				}

				if (pho.HitBBox.Bottom < _center.Y) {
					oct |= 0;

				} else if (pho.HitBBox.Top > _center.Y) {
					oct |= 2;

				} else {
					oct |= 128;
				}

				if ((oct & 128) == 0) {
					_children[oct]._hitObjects.Add(pho);

				} else {
					vRemain.Add(pho);
				}
			}

			// m_vho originally.Swap(vRemain); - but vRemain isn"t used below.
			_hitObjects = vRemain;

			// check if at least two nodes feature objects, otherwise don"t bother subdividing further
			var countEmpty = (_hitObjects.Count == 0) ? 1 : 0;
			for (var i = 0; i < 4; ++i) {
				if (_children[i]._hitObjects.Count == 0) {
					++countEmpty;
				}
			}

			if (countEmpty >= 4) {
				++levelEmpty;

			} else {
				levelEmpty = 0;
			}

			if (_center.X - bounds.Left > 0.0001 //!! magic
			    &&
			    levelEmpty <=
			    8 // If 8 levels were all just subdividing the same objects without luck, exit & Free the nodes again (but at least empty space was cut off)
			    && level + 1 < 128 / 3)
			{
				for (var i = 0; i < 4; ++i) {
					var childBounds = new Rect3D {
						Left = (i & 1) != 0 ? _center.X : bounds.Left,
						Top = (i & 2) != 0 ? _center.Y : bounds.Top,
						ZLow = bounds.ZLow,
						Right = (i & 1) != 0 ? bounds.Right : _center.X,
						Bottom = (i & 2) != 0 ? bounds.Bottom : _center.Y,
						ZHigh = bounds.ZHigh
					};

					_children[i].CreateNextLevel(childBounds, level + 1, levelEmpty);
				}
			}
		}
	}
}
