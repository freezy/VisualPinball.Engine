// Progressive Mesh type Polygon Reduction Algorithm
//   by Stan Melax (c) 1998
//
// Permission to use any of this code wherever you want is granted..
// Although, please do acknowledge authorship if appropriate.

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
