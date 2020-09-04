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
using System.Diagnostics;

namespace VisualPinball.Engine.Math.Mesh
{
	internal static class ProgMeshUtil
	{
		public static void RemoveFillWithBack<T>(List<T> c, T t)
		{
			var idxOf = c.IndexOf(t);
			var val = c[c.Count - 1];
			c.RemoveAt(c.Count - 1);

			if (idxOf == c.Count) {
				return;
			}
			c[idxOf] = val;

			Debug.Assert(!c.Contains(t), "[removeFillWithBack] List must not include value anymore.");
		}

		public static void AddUnique<T>(List<T> c, T t)
		{
			if (!c.Contains(t)) {
				c.Add(t);
			}
		}

		public static void PermuteVertices<T>(int[] permutation, T[] vert, ProgMeshTriData[] tri)
		{
			// rearrange the vertex Array
			var tempArray = new T[vert.Length];
			for (var i = 0; i < vert.Length; i++) {
				tempArray[i] = vert[i];
			}

			for (var i = 0; i < vert.Length; i++) {
				vert[permutation[i]] = tempArray[i];
			}

			// update the changes in the entries in the triangle Array
			for (var i = 0; i < tri.Length; i++) {
				for (var j = 0; j < 3; j++) {
					tri[i].V[j] = permutation[tri[i].V[j]];
				}
			}
		}
	}
}
