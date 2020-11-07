using System.Collections.Generic;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal class EdgeSetBetter
	{
		private readonly Dictionary<long, bool> _edges;

		internal static EdgeSetBetter Get(int capacity)
		{
			return new EdgeSetBetter(capacity);
		}

		private EdgeSetBetter(int capacity)
		{
			_edges = new Dictionary<long, bool>(capacity);
		}

		internal void Add(int i, int j) {
			_edges.Add(GetKey(i, j), true);
		}

		internal bool Has(int i, int j) {
			return _edges.ContainsKey(GetKey(i, j));
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
