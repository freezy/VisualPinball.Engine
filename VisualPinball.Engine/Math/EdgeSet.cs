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
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Math
{
	public class EdgeSet
	{
		private readonly Dictionary<long, bool> _edges;

		public EdgeSet(int capacity)
		{
			_edges = new Dictionary<long, bool>(capacity);
		}

		public void Add(int i, int j) {
			_edges.Add(GetKey(i, j), true);
		}

		public bool Has(int i, int j) {
			return _edges.ContainsKey(GetKey(i, j));
		}

		public IEnumerable<HitObject> AddHitEdge(int i, int j, Vertex3D vi, Vertex3D vj, ItemType itemType, IItem item) {
			if (!Has(i, j)) {   // edge not yet added?
				Add(i, j);
				return new[] { new HitLine3D(vi, vj, itemType, item) };
			}
			return new HitObject[0];
		}

		private static long GetKey(int i, int j)
		{
			return ((long) System.Math.Min(i, j) << 32) + System.Math.Max(i, j);
		}
	}
}
