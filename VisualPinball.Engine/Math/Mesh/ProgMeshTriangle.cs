// Progressive Mesh type Polygon Reduction Algorithm
//   by Stan Melax (c) 1998
//
// Permission to use any of this code wherever you want is granted..
// Although, please do acknowledge authorship if appropriate.
using System.Diagnostics;
using System.Linq;
using VisualPinball.Engine.Common;

namespace VisualPinball.Engine.Math.Mesh
{
	internal class ProgMeshTriangle
	{
		/// <summary>
		/// The 3 points that make this tri
		/// </summary>
		private readonly ProgMeshVertex[] _vertex;

		/// <summary>
		/// Unit vector orthogonal to this face
		/// </summary>
		public ProgMeshFloat3 Normal;

		public ProgMeshTriangle(ProgMeshVertex v0, ProgMeshVertex v1, ProgMeshVertex v2)
		{
			Debug.Assert(v0 != null && v1 != null && v2 != null, "[ProgMeshTriangle] Vertices must not be null.");
			Debug.Assert(v0 != v1 && v1 != v2 && v2 != v0, "[ProgMeshTriangle] Vertices must be different.");

			_vertex = new[] {v0, v1, v2};
			ComputeNormal();

			for (var i = 0; i < 3; i++) {
				_vertex[i].Face.Add(this);
				for (var j = 0; j < 3; j++) {
					if (i != j) {
						ProgMeshUtil.AddUnique(_vertex[i].Neighbor, _vertex[j]);
					}
				}
			}
		}

		public void Dispose(ProgMesh pm)
		{
			ProgMeshUtil.RemoveFillWithBack(pm.Triangles, this);
			for (var i = 0; i < 3; i++) {
				if (_vertex[i] != null) {
					ProgMeshUtil.RemoveFillWithBack(_vertex[i].Face, this);
				}
			}

			for (var i = 0; i < 3; i++) {
				var i2 = (i + 1) % 3;
				if (_vertex[i] != null && _vertex[i2] != null) {
					_vertex[i].RemoveIfNonNeighbor(_vertex[i2]);
					_vertex[i2].RemoveIfNonNeighbor(_vertex[i]);
				}
			}
		}

		public void ReplaceVertex(ProgMeshVertex vOld, ProgMeshVertex vNew)
		{
			Debug.Assert(vOld != null && vNew != null, "[ProgMeshTriangle.ReplaceVertex] Arguments must not be null.");
			Debug.Assert(vOld == _vertex[0] || vOld == _vertex[1] || vOld == _vertex[2], "[ProgMeshTriangle.replaceVertex] vOld must not be included in this.vertex.");
			Debug.Assert(vNew != _vertex[0] && vNew != _vertex[1] && vNew != _vertex[2], "[ProgMeshTriangle.replaceVertex] vNew must not be included in this.vertex.");

			if (vOld == _vertex[0]) {
				_vertex[0] = vNew;

			} else if (vOld == _vertex[1]) {
				_vertex[1] = vNew;

			} else {
				Debug.Assert(vOld == _vertex[2], "[ProgMeshTriangle.ReplaceVertex] vOld == vertex[2]");
				_vertex[2] = vNew;
			}

			ProgMeshUtil.RemoveFillWithBack(vOld.Face, this);
			Debug.Assert(!vNew.Face.Contains(this), "[ProgMeshTriangle.ReplaceVertex] !Contains(vNew->face, this)");

			vNew.Face.Add(this);

			for (var i = 0; i < 3; i++) {
				vOld.RemoveIfNonNeighbor(_vertex[i]);
				_vertex[i].RemoveIfNonNeighbor(vOld);
			}

			for (var i = 0; i < 3; i++) {
				Debug.Assert(_vertex[i].Face.Count(f => f == this) == 1, "[ProgMeshTriangle.replaceVertex] Contains(vertex[i]->face, this) == 1");
				for (var j = 0; j < 3; j++) {
					if (i != j) {
						ProgMeshUtil.AddUnique(_vertex[i].Neighbor, _vertex[j]);
					}
				}
			}
			ComputeNormal();
		}

		public bool HasVertex(ProgMeshVertex v)
		{
			return v == _vertex[0] || v == _vertex[1] || v == _vertex[2];
		}

		private void ComputeNormal()
		{
			var v0 = _vertex[0].Position;
			var v1 = _vertex[1].Position;
			var v2 = _vertex[2].Position;
			Normal = ProgMeshFloat3.Cross(v1.Sub(v0), v2.Sub(v1));
			var l =  Normal.Magnitude();
			if (l > Constants.FloatMin) {
				Normal = Normal.DivideScalar(l);
			}
		}
	}

	internal readonly struct ProgMeshTriData {

		public readonly int[] V;

		public ProgMeshTriData(int v1, int v2, int v3)
		{
			V = new[] {v1, v2, v3};
		}
	}
}
