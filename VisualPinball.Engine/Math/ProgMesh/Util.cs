using System.Collections.Generic;

namespace VisualPinball.Engine.Math.ProgMesh
{
	public static class Util
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
		}


		public static void AddUnique<T>(List<T> c, T t)
		{
			if (!c.Contains(t)) {
				c.Add(t);
			}
		}
	}
}
