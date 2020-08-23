using System.Collections.Generic;

namespace VisualPinball.Engine.Math.ProgMesh
{
	public class ProgMesh
	{
		public static float ComputeEdgeCollapseCost(Vertex u, Vertex v)
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
			for (var i = 0; i < u.face.Count; i++) {
				if (u.face[i].HasVertex(v)) {
					sides.Add(u.face[i]);
				}
			}

			// use the triangle facing most away from the sides
			// to determine our curvature term
			var curvature = 0f;
			for (var i = 0; i < u.face.Count; i++) {
				var minCurv = 1f; // curve for face i and closer side to it
				for (var j = 0; j < sides.Count; j++) {
					// use dot product of face normals.
					var dotProd = u.face[i].normal.Dot(sides[j].normal);
					minCurv = MathF.Min(minCurv, (1f - dotProd) * 0.5f);
				}

				curvature = MathF.Max(curvature, minCurv);
			}

			// the more coplanar the lower the curvature term
			var edgeLength = v.position.Clone().Sub(u.position).Magnitude();
			return edgeLength * curvature;
		}
	}
}
