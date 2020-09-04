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

using System.Collections.Generic;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Engine.Physics
{
	/// <summary>
	/// Our quadtree, used for static collision.
	/// </summary>
	public class QuadTree
	{
		private Primitive _unique; // everything below/including this node shares the same original primitive object (just for early outs if not collidable)

		public readonly QuadTree[] Children = new QuadTree[4];
		public readonly Vertex3D Center = new Vertex3D();
		public List<HitObject> HitObjects;
		public bool IsLeaf = true;

		private QuadTree()
		{
			HitObjects = new List<HitObject>();
		}

		public QuadTree(List<HitObject> hitObjects, Rect3D bounds)
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
				Children[i] = new QuadTree();
			}

			var vRemain = new List<HitObject>(); // hit objects which did not go to a quadrant

			_unique = HitObjects[0].E ? HitObjects[0].Item as Primitive : null;

			// sort items into appropriate child nodes
			foreach (var hitObject in HitObjects) {
				int oct;

				if ((hitObject.E ? hitObject.Item : null) != _unique) {
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
