using System.Collections.Generic;

namespace VisualPinball.Engine.Math.ProgMesh
{
	public class Vertex
	{
		/// <summary>
		/// location of point in euclidean space
		/// </summary>
		public Vertex3D position;

		/// <summary>
		/// place of vertex in original Array
		/// </summary>
		public uint id;

		/// <summary>
		/// adjacent vertices
		/// </summary>
		internal List<Vertex> neighbor;

		/// <summary>
		/// adjacent triangles
		/// </summary>
		internal List<Triangle> face;

		/// <summary>
		/// cached cost of collapsing edge
		/// </summary>
		float objdist;

		/// <summary>
		/// candidate vertex for collapse
		/// </summary>
		Vertex collapse;

		public List<Vertex> vertices = new List<Vertex>();

		public Vertex(Vertex3D v, ulong id)
		{
			position = v;
			this.id = (uint)id;
			vertices.Add(this);
		}

		public void RemoveIfNonNeighbor(Vertex n)
		{
			// removes n from neighbor Array if n isn't a neighbor.
			if (!neighbor.Contains(n)) {
				return;
			}

			for (var i = 0; i < face.Count; i++) {
				if (face[i].HasVertex(n)) {
					return;
				}
			}
			Triangle.RemoveFillWithBack(neighbor, n);
		}
	}
}
