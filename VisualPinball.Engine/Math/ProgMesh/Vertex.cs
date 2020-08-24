using System.Collections.Generic;

namespace VisualPinball.Engine.Math.ProgMesh
{
	/// <summary>
	/// For the polygon reduction algorithm we use data structures
	/// that contain a little bit more information than the usual
	/// indexed face set type of data structure.
	/// From a vertex we wish to be able to quickly get the
	/// neighboring faces and vertices.
	/// </summary>
	public class Vertex
	{
		/// <summary>
		/// location of point in euclidean space
		/// </summary>
		public Vertex3D position;

		/// <summary>
		/// place of vertex in original Array
		/// </summary>
		public int id;

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
		internal float objdist;

		/// <summary>
		/// candidate vertex for collapse
		/// </summary>
		internal Vertex collapse;

		public Vertex(Vertex3D v, int id)
		{
			position = v;
			this.id = id;
			ProgMesh.vertices.Add(this);
		}

		~Vertex()
		{
			while (neighbor.Count > 0) {
				Util.RemoveFillWithBack(neighbor[0].neighbor, this);
				Util.RemoveFillWithBack(neighbor, neighbor[0]);
			}
			Util.RemoveFillWithBack(ProgMesh.vertices, this);
		}

		public void RemoveIfNonNeighbor(Vertex n)
		{
			// removes n from neighbor Array if n isn't a neighbor.
			if (!neighbor.Contains(n)) {
				return;
			}

			foreach (var face in face) {
				if (face.HasVertex(n)) {
					return;
				}
			}
			Util.RemoveFillWithBack(neighbor, n);
		}
	}
}
