// Progressive Mesh type Polygon Reduction Algorithm
//   by Stan Melax (c) 1998
//
// Permission to use any of this code wherever you want is granted..
// Although, please do acknowledge authorship if appropriate.

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

using System.Diagnostics;
using System.Collections.Generic;

namespace VisualPinball.Engine.Math.Mesh
{
	/// <summary>
	/// For the polygon reduction algorithm we use data structures
	/// that contain a little bit more information than the usual
	/// indexed face set type of data structure.
	/// From a vertex we wish to be able to quickly get the
	/// neighboring faces and vertices.
	/// </summary>
	internal class ProgMeshVertex
	{
		/// <summary>
		/// location of point in euclidean space
		/// </summary>
		public ProgMeshFloat3 Position;

		/// <summary>
		/// place of vertex in original Array
		/// </summary>
		public readonly int ID;

		/// <summary>
		/// adjacent vertices
		/// </summary>
		public readonly List<ProgMeshVertex> Neighbor = new List<ProgMeshVertex>();

		/// <summary>
		/// adjacent triangles
		/// </summary>
		public readonly List<ProgMeshTriangle> Face = new List<ProgMeshTriangle>();

		/// <summary>
		/// cached cost of collapsing edge
		/// </summary>
		public float ObjDist;

		/// <summary>
		/// candidate vertex for collapse
		/// </summary>
		public ProgMeshVertex Collapse;

		public ProgMeshVertex(ProgMeshFloat3 v, int id)
		{
			Position = v;
			ID = id;
		}

		public void Dispose(ProgMesh pm)
		{
			Debug.Assert(Face.Count == 0, "[ProgMeshVertex.destroy] face.size() == 0");
			while (Neighbor.Count > 0) {
				ProgMeshUtil.RemoveFillWithBack(Neighbor[0].Neighbor, this);
				ProgMeshUtil.RemoveFillWithBack(Neighbor, Neighbor[0]);
			}
			ProgMeshUtil.RemoveFillWithBack(pm.Vertices, this);
		}

		public void RemoveIfNonNeighbor(ProgMeshVertex n)
		{
			// removes n from neighbor Array if n isn't a neighbor.
			if (!Neighbor.Contains(n)) {
				return;
			}

			foreach (var face in Face) {
				if (face.HasVertex(n)) {
					return;
				}
			}
			ProgMeshUtil.RemoveFillWithBack(Neighbor, n);
		}
	}
}
