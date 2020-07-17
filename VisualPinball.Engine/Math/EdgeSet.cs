using System.Collections.Generic;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Math
{
	public class EdgeSet
	{
		private readonly HashSet<string> _edges = new HashSet<string>();

		public void Add(int i, int j) {
			_edges.Add(GetKey(i, j));
		}

		public bool Has(int i, int j) {
			return _edges.Contains(GetKey(i, j));
		}

		public IEnumerable<HitObject> AddHitEdge(int i, int j, Vertex3D vi, Vertex3D vj, ItemType itemType) {
			if (!Has(i, j)) {   // edge not yet added?
				Add(i, j);
				return new[] { new HitLine3D(vi, vj, itemType) };
			}
			return new HitObject[0];
		}

		private static string GetKey(int i, int j) {
			return $"{System.Math.Min(i, j)},{System.Math.Max(i, j)}";
		}
	}
}
