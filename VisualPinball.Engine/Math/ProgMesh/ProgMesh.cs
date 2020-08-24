using System.Collections.Generic;

namespace VisualPinball.Engine.Math.ProgMesh
{
	internal class ProgMesh
	{
		internal static readonly List<Vertex> vertices = new List<Vertex>();
		internal static readonly List<Triangle> triangles = new List<Triangle>();

		internal static float ComputeEdgeCollapseCost(Vertex u, Vertex v)
		{
			// if we collapse edge uv by moving u to v then how
			// much different will the model change, i.e. how much "error".
			// Texture, vertex normal, and border vertex code was removed
			// to keep this demo as simple as possible.
			// The method of determining cost was designed in order
			// to exploit small and coplanar regions for
			// effective polygon reduction.
			// Is is possible to add some checks here to see if "folds"
			// would be generated.  i.e. normal of a remaining face gets
			// flipped.  I never seemed to run into this problem and
			// therefore never added code to detect this case.

			// find the "sides" triangles that are on the edge uv
			var sides = new List<Triangle>(u.face.Count);
			foreach (var face in u.face) {
				if (face.HasVertex(v)) {
					sides.Add(face);
				}
			}

			// use the triangle facing most away from the sides
			// to determine our curvature term
			var curvature = 0f;
			foreach (var face in u.face) {
				var minCurve = 1f; // curve for face i and closer side to it
				foreach (var side in sides) {
					// use dot product of face normals.
					var dotProd = face.normal.Dot(side.normal);
					minCurve = MathF.Min(minCurve, (1f - dotProd) * 0.5f);
				}

				curvature = MathF.Max(curvature, minCurve);
			}

			// the more coplanar the lower the curvature term
			var edgeLength = v.position.Clone().Sub(u.position).Magnitude();
			return edgeLength * curvature;
		}

		internal static void ComputeEdgeCostAtVertex(Vertex v)
		{
			// compute the edge collapse cost for all edges that start
			// from vertex v.  Since we are only interested in reducing
			// the object by selecting the min cost edge at each step, we
			// only cache the cost of the least cost edge at this vertex
			// (in member variable collapse) as well as the value of the
			// cost (in member variable objdist).
			if (v.neighbor.Count == 0) {
				// v doesn't have neighbors so it costs nothing to collapse
				v.collapse = null;
				v.objdist = -0.01f;
				return;
			}

			v.objdist = float.MaxValue;
			v.collapse = null;

			// search all neighboring edges for "least cost" edge
			foreach (var neighbor in v.neighbor) {
				var dist = ComputeEdgeCollapseCost(v, neighbor);
				if (dist < v.objdist) {
					v.collapse = neighbor;  // candidate for edge collapse
					v.objdist = dist;             // cost of the collapse
				}
			}
		}

		internal static void ComputeAllEdgeCollapseCosts()
		{
			// For all the edges, compute the difference it would make
			// to the model if it was collapsed.  The least of these
			// per vertex is cached in each vertex object.
			foreach (var v in vertices) {
				ComputeEdgeCostAtVertex(v);
			}
		}

		internal static void Collapse(ref Vertex u, Vertex v)
		{
			// Collapse the edge uv by moving vertex u onto v
			// Actually remove tris on uv, then update tris that
			// have u to have v, and then remove u.
			if (v == null) {
				// u is a vertex all by itself so just delete it
				u = null;
				return;
			}
			var tmp = new List<Vertex>(u.neighbor.Count);

			// make tmp a Array of all the neighbors of u
			for (var i = 0; i < tmp.Count; i++) {
				tmp[i] = u.neighbor[i];
			}

			// delete triangles on edge uv:
			{
				var i = u.face.Count;
				while (i-- > 0) {
					if (u.face[i].HasVertex(v))
						u.face[i] = null;
				}
			}

			// update remaining triangles to have v instead of u
			{
				var i = u.face.Count;
				while (i-- > 0) {
					u.face[i].ReplaceVertex(u, v);
				}
			}
			u = null;

			// recompute the edge collapse costs for neighboring vertices
			foreach (var t in tmp) {
				ComputeEdgeCostAtVertex(t);
			}
		}

		internal static void AddVertex(List<Vertex3D> vert)
		{
			for (var i = 0; i < vert.Count; i++) {
				new Vertex(vert[i], i); //!! braindead design, actually fills up "vertices"
			}
		}

		internal static void AddFaces(List<tridata> tri)
		{
			for (var i = 0; i < tri.Count; i++) {
				new Triangle(vertices[tri[i].v[0]], //!! braindead design, actually fills up "triangles"
					vertices[tri[i].v[1]],
					vertices[tri[i].v[2]]
				);
			}
		}

		internal static Vertex MinimumCostEdge()
		{
			// Find the edge that when collapsed will affect model the least.
			// This funtion actually returns a Vertex, the second vertex
			// of the edge (collapse candidate) is stored in the vertex data.
			// Serious optimization opportunity here: this function currently
			// does a sequential search through an unsorted Array :-(
			// Our algorithm could be O(n*lg(n)) instead of O(n*n)
			var mn = vertices[0];
			foreach (var vertex in vertices) {
				if (vertex.objdist < mn.objdist) {
					mn = vertex;
				}
			}
			return mn;
		}

		internal void ProgressiveMesh(List<Vertex3D> vert, List<tridata> tri, List<int> map, List<int> permutation)
		{
			if (vert.Count == 0 || tri.Count == 0)
				return;

			if (vertices.Count < vert.Count) {
				vertices.AddRange(new Vertex[vert.Count - vertices.Count]);
			}

			if (triangles.Count < tri.Count) {
				triangles.AddRange(new Triangle[tri.Count - triangles.Count]);
			}

			AddVertex(vert);  // put input data into our data structures
			AddFaces(tri);
			ComputeAllEdgeCollapseCosts(); // cache all edge collapse costs

			if (permutation.Count < vertices.Count) {
				permutation.AddRange(new int[vertices.Count - permutation.Count]);
			}
			if (map.Count < vertices.Count) {
				map.AddRange(new int[vertices.Count - map.Count]);
			}

			// reduce the object down to nothing:
			while (vertices.Count > 0) {
				// get the next vertex to collapse
				var mn = MinimumCostEdge();
				// keep track of this vertex, i.e. the collapse ordering
				permutation[mn.id] = vertices.Count - 1;
				// keep track of vertex to which we collapse to
				map[vertices.Count - 1] = mn.collapse?.id ?? 0;
				// Collapse this edge
				Collapse(ref mn, mn.collapse);
			}

			// reorder the map Array based on the collapse ordering
			for (var i = 0; i < map.Count; i++)
				map[i] = map[i] == ~0u ? 0 : permutation[map[i]];

			// The caller of this function should reorder their vertices
			// according to the returned "permutation".
		}

		// Note that the use of the MapVertex() function and the map
		// Array isn't part of the polygon reduction algorithm.
		// We just set up this system here in this module
		// so that we could retrieve the model at any desired vertex count.
		// Therefore if this part of the program confuses you, then
		// dont worry about it.
		// When the model is rendered using a maximum of mx vertices
		// then it is vertices 0 through mx-1 that are used.
		// We are able to do this because the vertex Array
		// gets sorted according to the collapse order.
		// The MapVertex() routine takes a vertex number 'a' and the
		// maximum number of vertices 'mx' and returns the
		// appropriate vertex in the range 0 to mx-1.
		// When 'a' is greater than 'mx' the MapVertex() routine
		// follows the chain of edge collapses until a vertex
		// within the limit is reached.
		//   An example to make this clear: assume there is
		//   a triangle with vertices 1, 3 and 12.  But when
		//   rendering the model we limit ourselves to 10 vertices.
		//   In that case we find out how vertex 12 was removed
		//   by the polygon reduction algorithm.  i.e. which
		//   edge was collapsed.  Lets say that vertex 12 was collapsed
		//   to vertex number 7.  This number would have been stored
		//   in the collapse_map array (i.e. map[12]==7).
		//   Since vertex 7 is in range (less than max of 10) we
		//   will want to render the triangle 1,3,7.
		//   Pretend now that we want to limit ourselves to 5 vertices.
		//   and vertex 7 was collapsed to vertex 3
		//   (i.e. map[7]==3).  Then triangle 1,3,12 would now be
		//   triangle 1,3,3.  i.e. this polygon was removed by the
		//   progressive mesh polygon reduction algorithm by the time
		//   it had gotten down to 5 vertices.
		//   No need to draw a one dimensional polygon. :-)
		internal static int MapVertex(int a, uint mx, List<int> map)
		{
			while (a >= mx) {
				a = map[a];
			}

			return a;
		}

		void ReMapIndices(uint num_vertices, List<tridata> tri, List<tridata> new_tri, List<int> map)
		{
			for (var i = 0; i < tri.Count; i++) {
				var t = new tridata {
					v = new[] {
						MapVertex(tri[i].v[0], num_vertices, map),
						MapVertex(tri[i].v[1], num_vertices, map),
						MapVertex(tri[i].v[2], num_vertices, map)
					}
				};

				//!! note:  serious optimization opportunity here,
				//  by sorting the triangles the following "continue"
				//  could have been made into a "break" statement.
				if (t.v[0] == t.v[1] || t.v[1] == t.v[2] || t.v[2] == t.v[0]) {
					continue;
				}
				new_tri.Add(t);
			}
		}
	}
}
