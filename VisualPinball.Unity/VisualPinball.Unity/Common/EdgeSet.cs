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

namespace VisualPinball.Unity
{
	internal class EdgeSet
	{
		private readonly HashSet<long> _edges;

		internal static EdgeSet Get()
		{
			return new EdgeSet();
		}

		private EdgeSet()
		{
			_edges = new HashSet<long>();
		}

		private void Add(int i, int j) {
			_edges.Add(GetKey(i, j));
		}

		private bool Has(int i, int j) {
			return _edges.Contains(GetKey(i, j));
		}

		internal bool ShouldAddHitEdge(int i, int j) {
			if (!Has(i, j)) {
				Add(i, j);
				return true;
			}
			return false;
		}

		private static long GetKey(int i, int j)
		{
			return ((long) System.Math.Min(i, j) << 32) + System.Math.Max(i, j);
		}
	}
}
