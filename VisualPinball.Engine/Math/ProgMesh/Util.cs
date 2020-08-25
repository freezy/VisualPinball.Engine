using System;
using System.Collections.Generic;

namespace VisualPinball.Engine.Math.ProgMesh
{
	internal static class Util
	{
		internal static void RemoveFillWithBack<T>(List<T> c, T t)
		{
			var idxOf = c.IndexOf(t);
			var val = c[c.Count - 1];
			c.RemoveAt(c.Count - 1);

			if (idxOf == c.Count) {
				return;
			}
			c[idxOf] = val;

			Assert(!c.Contains(t), "[removeFillWithBack] List must not include value anymore.");
		}

		internal static void AddUnique<T>(List<T> c, T t)
		{
			if (!c.Contains(t)) {
				c.Add(t);
			}
		}


		internal static void PermuteVertices<T>(List<int> permutation, List<T> vert, List<tridata> tri)
		{
			// rearrange the vertex Array
			var temp_Array = new List<T>(vert.Count);
			for (var i = 0; i < vert.Count; i++) {
				temp_Array[i] = vert[i];
			}

			for (var i = 0; i < vert.Count; i++) {
				vert[permutation[i]] = temp_Array[i];
			}

			// update the changes in the entries in the triangle Array
			for (var i = 0; i < tri.Count; i++) {
				for (var j = 0; j < 3; j++) {
					tri[i].v[j] = permutation[tri[i].v[j]];
				}
			}
		}

		internal static void Assert(bool success, string message) {
			if (!success) {
				throw new InvalidOperationException(message);
			}
		}
	}
}
