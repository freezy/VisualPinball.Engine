// Progressive Mesh type Polygon Reduction Algorithm
//   by Stan Melax (c) 1998
//
// Permission to use any of this code wherever you want is granted..
// Although, please do acknowledge authorship if appropriate.

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

using System;
using System.Diagnostics;
using System.Collections.Generic;
using VisualPinball.Engine.Common;

namespace VisualPinball.Engine.Math.Mesh
{
	/// <summary>
	///
	/// The function ProgressiveMesh() takes a model in an "indexed face
	/// set" sort of way.  i.e. Array of vertices and Array of triangles.
	/// The function then does the polygon reduction algorithm
	/// internally and reduces the model all the way down to 0
	/// vertices and then returns the order in which the
	/// vertices are collapsed and to which neighbor each vertex
	/// is collapsed to.  More specifically the returned "permutation"
	/// indicates how to reorder your vertices so you can render
	/// an object by using the first n vertices (for the n
	/// vertex version).  After permuting your vertices, the
	/// map Array indicates to which vertex each vertex is collapsed to.
	/// </summary>
	internal class ProgMesh
	{
		public readonly List<ProgMeshVertex> Vertices = new List<ProgMeshVertex>();
		public readonly List<ProgMeshTriangle> Triangles = new List<ProgMeshTriangle>();

		private static float ComputeEdgeCollapseCost(ProgMeshVertex u, ProgMeshVertex v)
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
			var sides = new List<ProgMeshTriangle>(u.Face.Count);
			foreach (var face in u.Face) {
				if (face.HasVertex(v)) {
					sides.Add(face);
				}
			}

			// use the triangle facing most away from the sides
			// to determine our curvature term
			var curvature = 0f;
			foreach (var face in u.Face) {
				var minCurve = 1f; // curve for face i and closer side to it
				foreach (var side in sides) {
					// use dot product of face normals.
					var dotProd = face.Normal.Dot(side.Normal);
					minCurve = MathF.Min(minCurve, (1f - dotProd) * 0.5f);
				}

				curvature = MathF.Max(curvature, minCurve);
			}

			// the more coplanar the lower the curvature term
			var edgeLength = v.Position.Sub(u.Position).Magnitude();
			return edgeLength * curvature;
		}

		private static void ComputeEdgeCostAtVertex(ProgMeshVertex v)
		{
			// compute the edge collapse cost for all edges that start
			// from vertex v.  Since we are only interested in reducing
			// the object by selecting the min cost edge at each step, we
			// only cache the cost of the least cost edge at this vertex
			// (in member variable collapse) as well as the value of the
			// cost (in member variable ObjDist).
			if (v.Neighbor.Count == 0) {
				// v doesn't have neighbors so it costs nothing to collapse
				v.Collapse = null;
				v.ObjDist = -0.01f;
				return;
			}

			v.ObjDist = Constants.FloatMax;
			v.Collapse = null;

			// search all neighboring edges for "least cost" edge
			foreach (var neighbor in v.Neighbor) {
				var dist = ComputeEdgeCollapseCost(v, neighbor);
				if (dist < v.ObjDist) {
					v.Collapse = neighbor;  // candidate for edge collapse
					v.ObjDist = dist;             // cost of the collapse
				}
			}
		}

		private void ComputeAllEdgeCollapseCosts()
		{
			// For all the edges, compute the difference it would make
			// to the model if it was collapsed.  The least of these
			// per vertex is cached in each vertex object.
			foreach (var v in Vertices) {
				ComputeEdgeCostAtVertex(v);
			}
		}

		private void Collapse(ProgMeshVertex u, ProgMeshVertex v)
		{
			int i;

			// Collapse the edge uv by moving vertex u onto v
			// Actually remove tris on uv, then update tris that
			// have u to have v, and then remove u.
			if (v == null) {
				// u is a vertex all by itself so just delete it
				u.Dispose(this);
				return;
			}
			var tmp = new ProgMeshVertex[u.Neighbor.Count];

			// make tmp a Array of all the neighbors of u
			for (i = 0; i < tmp.Length; i++) {
				tmp[i] = u.Neighbor[i];
			}

			// delete triangles on edge uv:
			i = u.Face.Count;
			while (i-- > 0) {
				if (u.Face[i].HasVertex(v)) {
					u.Face[i].Dispose(this);
				}
			}

			// update remaining triangles to have v instead of u
			i = u.Face.Count;
			while (i-- > 0) {
				u.Face[i].ReplaceVertex(u, v);
			}
			u.Dispose(this);

			// recompute the edge collapse costs for neighboring vertices
			foreach (var t in tmp) {
				ComputeEdgeCostAtVertex(t);
			}
		}

		private void AddVertex(IReadOnlyList<ProgMeshFloat3> vert)
		{
			for (var i = 0; i < vert.Count; i++) {
				Vertices.Add(new ProgMeshVertex(vert[i], i));
			}
		}

		private void AddFaces(IEnumerable<ProgMeshTriData> tri)
		{
			foreach (var t in tri) {
				Triangles.Add(new ProgMeshTriangle(
					Vertices[t.V[0]],
					Vertices[t.V[1]],
					Vertices[t.V[2]]
				));
			}
		}

		private ProgMeshVertex MinimumCostEdge()
		{
			// Find the edge that when collapsed will affect model the least.
			// This function actually returns a Vertex, the second vertex
			// of the edge (collapse candidate) is stored in the vertex data.
			// Serious optimization opportunity here: this function currently
			// does a sequential search through an unsorted Array :-(
			// Our algorithm could be O(n*lg(n)) instead of O(n*n)
			var mn = Vertices[0];
			foreach (var vertex in Vertices) {
				if (vertex.ObjDist < mn.ObjDist) {
					mn = vertex;
				}
			}
			return mn;
		}

		public Tuple<int[], int[]> ProgressiveMesh(ProgMeshFloat3[] vert, ProgMeshTriData[] tri)
		{
			if (vert.Length == 0 || tri.Length == 0)
				return new Tuple<int[], int[]>(new int[0], new int[0]);

			AddVertex(vert);  // put input data into our data structures
			AddFaces(tri);
			ComputeAllEdgeCollapseCosts(); // cache all edge collapse costs

			var permutation = new int[vert.Length];
			var map = new int[vert.Length];

			// reduce the object down to nothing:
			while (Vertices.Count > 0) {
				// get the next vertex to collapse
				var mn = MinimumCostEdge();
				// keep track of this vertex, i.e. the collapse ordering
				permutation[mn.ID] = Vertices.Count - 1;
				// keep track of vertex to which we collapse to
				map[Vertices.Count - 1] = mn.Collapse?.ID ?? 2147483647;
				// Collapse this edge
				Collapse(mn, mn.Collapse);
			}

			// reorder the map Array based on the collapse ordering
			for (var i = 0; i < map.Length; i++) {
				map[i] = map[i] == 2147483647 ? 0 : permutation[map[i]];
			}

			// The caller of this function should reorder their vertices
			// according to the returned "permutation".
			Debug.Assert(Vertices.Count == 0, "[progressiveMesh] vertices.size() == 0");
			Debug.Assert(Triangles.Count == 0,  "[progressiveMesh] triangles.size() == 0");

			return new Tuple<int[], int[]>(map, permutation);
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
		private static int MapVertex(int a, uint mx, IReadOnlyList<int> map)
		{
			while (a >= mx) {
				a = map[a];
			}

			return a;
		}

		public static void ReMapIndices(uint numVertices, ProgMeshTriData[] tri, List<ProgMeshTriData> newTri, int[] map)
		{
			Debug.Assert(newTri.Count == 0, "[ReMapIndices] new_tri.size() == 0");
			Debug.Assert(map.Length != 0, "[ReMapIndices] map.size() != 0");
			Debug.Assert(numVertices != 0, "[ReMapIndices] num_vertices != 0");

			for (var i = 0; i < tri.Length; i++) {
				var t = new ProgMeshTriData(
					MapVertex(tri[i].V[0], numVertices, map),
					MapVertex(tri[i].V[1], numVertices, map),
					MapVertex(tri[i].V[2], numVertices, map)
				);

				//!! note:  serious optimization opportunity here,
				//  by sorting the triangles the following "continue"
				//  could have been made into a "break" statement.
				if (t.V[0] == t.V[1] || t.V[1] == t.V[2] || t.V[2] == t.V[0]) {
					continue;
				}
				newTri.Add(t);
			}
		}
	}
}
