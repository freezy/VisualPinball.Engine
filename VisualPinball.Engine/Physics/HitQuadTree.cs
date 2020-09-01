using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitQuadTree
	{
		private EventProxy _unique; // everything below/including this node shares the same original primitive object (just for early outs if not collidable)

		public readonly HitQuadTree[] Children = new HitQuadTree[4];
		public readonly Vertex3D Center = new Vertex3D();
		public List<HitObject> HitObjects;
		public bool IsLeaf = true;

		private HitQuadTree()
		{
			HitObjects = new List<HitObject>();
		}

		public HitQuadTree(List<HitObject> hitObjects, Rect3D bounds)
		{
			HitObjects = hitObjects;
			CreateNextLevel(bounds, 0, 0);
		}

		private void CreateNextLevel(Rect3D bounds, int level, int levelEmpty)
		{
			if (HitObjects.Count <= 4) {
				//!! magic
				return;
			}

			IsLeaf = false;

			Center.X = (bounds.Left + bounds.Right) * 0.5f;
			Center.Y = (bounds.Top + bounds.Bottom) * 0.5f;
			Center.Z = (bounds.ZLow + bounds.ZHigh) * 0.5f;

			for (var i = 0; i < 4; i++) {
				Children[i] = new HitQuadTree();
			}

			List<HitObject> vRemain = new List<HitObject>(); // hit objects which did not go to a quadrant

			// TODO check if casting in C++ results in null if not the cast type
			_unique = HitObjects[0].E ? HitObjects[0].Obj : null;

			// sort items into appropriate child nodes
			foreach (var hitObject in HitObjects) {
				int oct;

				if ((hitObject.E ? hitObject.Obj : null) != _unique) {
					// are all objects in current node unique/belong to the same primitive?
					_unique = null;
				}

				if (hitObject.HitBBox.Right < Center.X) {
					oct = 0;

				} else if (hitObject.HitBBox.Left > Center.X) {
					oct = 1;

				} else {
					oct = 128;
				}

				if (hitObject.HitBBox.Bottom < Center.Y) {
					oct |= 0;

				} else if (hitObject.HitBBox.Top > Center.Y) {
					oct |= 2;

				} else {
					oct |= 128;
				}

				if ((oct & 128) == 0) {
					Children[oct].HitObjects.Add(hitObject);

				} else {
					vRemain.Add(hitObject);
				}
			}

			// m_vho originally.Swap(vRemain); - but vRemain isn't used below.
			HitObjects = vRemain;

			// check if at least two nodes feature objects, otherwise don't bother subdividing further
			var countEmpty = HitObjects.Count == 0 ? 1 : 0;
			for (var i = 0; i < 4; ++i) {
				if (Children[i].HitObjects.Count == 0) {
					++countEmpty;
				}
			}

			if (countEmpty >= 4) {
				++levelEmpty;

			} else {
				levelEmpty = 0;
			}

			if (Center.X - bounds.Left > 0.0001 //!! magic
			    &&
			    levelEmpty <=
			    8 // If 8 levels were all just subdividing the same objects without luck, exit & Free the nodes again (but at least empty space was cut off)
			    && level + 1 < 128 / 3)
			{
				for (var i = 0; i < 4; ++i) {
					var childBounds = new Rect3D {
						Left = (i & 1) != 0 ? Center.X : bounds.Left,
						Top = (i & 2) != 0 ? Center.Y : bounds.Top,
						ZLow = bounds.ZLow,
						Right = (i & 1) != 0 ? bounds.Right : Center.X,
						Bottom = (i & 2) != 0 ? bounds.Bottom : Center.Y,
						ZHigh = bounds.ZHigh
					};

					Children[i].CreateNextLevel(childBounds, level + 1, levelEmpty);
				}
			}
		}
	}
}
