// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using System;
using Unity.Collections;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal struct EdgeSet : IDisposable
	{
		private NativeParallelHashSet<long> _edges;

		internal static EdgeSet Get(Allocator allocator)
		{
			return new EdgeSet(allocator);
		}

		private EdgeSet(Allocator allocator)
		{
			_edges = new NativeParallelHashSet<long>(64, allocator);
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

		private static long GetKey(int i, int j) => ((long) math.min(i, j) << 32) + math.max(i, j);

		public void Dispose()
		{
			_edges.Dispose();
		}
	}
}
