// ReSharper disable CompareOfFloatsByEqualityOperator

using System;
using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// A mesh consists of vertices and indices that link the vertices to faces
	/// (or triangles). <p/>
	///
	/// The vertices also contain UVs and normals apart from the actual
	/// coordinates.
	/// </summary>
	public class Mesh
	{
		public string Name;
		public Vertex3DNoTex2[] Vertices;
		public int[] Indices;
		public bool IsSet => Vertices != null && Indices != null;

		public Mesh() { }

		public Mesh(string name)
		{
			Name = name;
		}

		public Mesh(string name, float[][] vertices, int[] indices)
		{
			Name = name;
			Vertices = vertices.Select(v => new Vertex3DNoTex2(v)).ToArray();
			Indices = indices;
		}

		public Mesh(float[][] vertices, int[] indices)
		{
			Vertices = vertices.Select(v => new Vertex3DNoTex2(v)).ToArray();
			Indices = indices;
		}

		public Mesh(Vertex3DNoTex2[] vertices, int[] indices)
		{
			Vertices = vertices;
			Indices = indices;
		}

		public Mesh Transform(Matrix3D matrix, Matrix3D normalMatrix = null, Func<float, float> getZ = null) {

			// abort on identity matrices
			if (matrix.IsIdentity() && (normalMatrix == null || normalMatrix.IsIdentity())) {
				return this;
			}

			// transform vertices
			foreach (var vertex in Vertices) {
				var vert = new Vertex3D(vertex.X, vertex.Y, vertex.Z).MultiplyMatrix(matrix);
				vertex.X = vert.X;
				vertex.Y = vert.Y;
				vertex.Z = getZ?.Invoke(vert.Z) ?? vert.Z;

				var norm = new Vertex3D(vertex.Nx, vertex.Ny, vertex.Nz).MultiplyMatrixNoTranslate(normalMatrix ?? matrix);
				vertex.Nx = norm.X;
				vertex.Ny = norm.Y;
				vertex.Nz = norm.Z;
			}
			return this;
		}

		public Mesh MakeTranslation(float x, float y, float z)
		{
			foreach (var vertex in Vertices) {
				vertex.X += x;
				vertex.Y += y;
				vertex.Z += z;
			}

			return this;
		}

		public Mesh Clone(string name = null) {

			var mesh = new Mesh {
				Name = name ?? Name,
				Vertices = new Vertex3DNoTex2[Vertices.Length],
				Indices = new int[Indices.Length]
			};
			//mesh.animationFrames = this.animationFrames.map(a => a.clone());
			Vertices.Select(v => v.Clone()).ToArray().CopyTo(mesh.Vertices, 0);
			Indices.CopyTo(mesh.Indices, 0);
			//mesh.faceIndexOffset = this.faceIndexOffset;
			return mesh;
		}

		public static void ComputeNormals(Vertex3DNoTex2[] vertices, int numVertices, int[] indices, int numIndices) {

			for (var i = 0; i < numVertices; i++) {
				var v = vertices[i];
				v.Nx = v.Ny = v.Nz = 0.0f;
			}

			for (var i = 0; i < numIndices; i += 3) {
				var a = vertices[indices[i]];
				var b = vertices[indices[i + 1]];
				var c = vertices[indices[i + 2]];

				var e0 = new Vertex3D(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
				var e1 = new Vertex3D(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
				var normal = e0.Clone().Cross(e1).Normalize();

				a.Nx += normal.X; a.Ny += normal.Y; a.Nz += normal.Z;
				b.Nx += normal.X; b.Ny += normal.Y; b.Nz += normal.Z;
				c.Nx += normal.X; c.Ny += normal.Y; c.Nz += normal.Z;
			}

			for (var i = 0; i < numVertices; i++) {
				var v = vertices[i];
				var l = v.Nx * v.Nx + v.Ny * v.Ny + v.Nz * v.Nz;
				var invL = l >= Constants.FloatMin ? 1.0f / MathF.Sqrt(l) : 0.0f;
				v.Nx *= invL;
				v.Ny *= invL;
				v.Nz *= invL;
			}
		}

		public Mesh MakeScale(float x, float y, float z)
		{
			foreach (var vertex in Vertices) {
				vertex.X *= x;
				vertex.Y *= y;
				vertex.Z *= z;
			}
			return this;
		}

		public static int[] PolygonToTriangles(IRenderVertex[] rgv, List<int> poly)
		{
			// There should be this many convex triangles.
			// If not, the polygon is self-intersecting
			var triCount = poly.Count - 2;
			var tri = new List<int>();

			for (var l = 0; l < triCount; ++l) {
				for (var i = 0; i < poly.Count; ++i) {
					var s = poly.Count;
					var pre = poly[i == 0 ? s - 1 : i - 1];
					var a = poly[i];
					var b = poly[i < s - 1 ? i + 1 : 0];
					var c = poly[i < s - 2 ? i + 2 : i + 2 - s];
					var post = poly[i < s - 3 ? i + 3 : i + 3 - s];
					if (AdvancePoint(rgv, poly, a, b, c, pre, post)) {
						tri.Add(a);
						tri.Add(c);
						tri.Add(b);
						poly.RemoveAt(i < s - 1 ? i + 1 : 0); // b
						break;
					}
				}
			}
			return tri.ToArray();
		}

		private static bool AdvancePoint(IReadOnlyList<IRenderVertex> rgv, IReadOnlyList<int> poly, int a, int b, int c, int pre, int post)
		{
			var pv1 = rgv[a];
			var pv2 = rgv[b];
			var pv3 = rgv[c];

			var pvPre = rgv[pre];
			var pvPost = rgv[post];

			if (GetDot(pv1, pv2, pv3) < 0 ||
			    // Make sure angle created by new triangle line falls inside existing angles
			    // If the existing angle is a concave angle, then new angle must be smaller,
			    // because our triangle can"t have angles greater than 180
			    GetDot(pvPre, pv1, pv2) > 0 &&
			    GetDot(pvPre, pv1, pv3) < 0 || // convex angle, make sure new angle is smaller than it
			    GetDot(pv2, pv3, pvPost) > 0 && GetDot(pv1, pv3, pvPost) < 0) {
				return false;
			}

			// Now make sure the interior segment of this triangle (line ac) does not
			// intersect the polygon anywhere

			// sort our static line segment
			var minX = MathF.Min(pv1.X, pv3.X);
			var maxX = MathF.Max(pv1.X, pv3.X);
			var minY = MathF.Min(pv1.Y, pv3.Y);
			var maxY = MathF.Max(pv1.Y, pv3.Y);

			for (var i = 0; i < poly.Count; ++i) {
				var pvCross1 = rgv[poly[i]];
				var pvCross2 = rgv[poly[i < poly.Count - 1 ? i + 1 : 0]];

				if (pvCross1 != pv1 && pvCross2 != pv1 && pvCross1 != pv3 && pvCross2 != pv3 &&
				    (pvCross1.Y >= minY || pvCross2.Y >= minY) &&
				    (pvCross1.Y <= maxY || pvCross2.Y <= maxY) &&
				    (pvCross1.X >= minX || pvCross2.X >= minX) &&
				    (pvCross1.X <= maxX || pvCross2.Y <= maxX) &&
				    LinesIntersect(pv1, pv3, pvCross1, pvCross2)) {

					return false;
				}
			}

			return true;
		}

		private static float GetDot(IRenderVertex pvEnd1, IRenderVertex pvJoint, IRenderVertex pvEnd2)
		{
			return (pvJoint.X - pvEnd1.X) * (pvJoint.Y - pvEnd2.Y) - (pvJoint.Y - pvEnd1.Y) * (pvJoint.X - pvEnd2.X);
		}

		private static bool LinesIntersect(IRenderVertex start1, IRenderVertex start2, IRenderVertex end1, IRenderVertex end2) {

			var x1 = start1.X;
			var y1 = start1.Y;
			var x2 = start2.X;
			var y2 = start2.Y;
			var x3 = end1.X;
			var y3 = end1.Y;
			var x4 = end2.X;
			var y4 = end2.Y;

			var d123 = (x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1);

			if (d123 == 0.0) { // p3 lies on the same line as p1 and p2
				return x3 >= MathF.Min(x1, x2) && x3 <= MathF.Max(x2, x1);
			}

			var d124 = (x2 - x1) * (y4 - y1) - (x4 - x1) * (y2 - y1);

			if (d124 == 0.0) { // p4 lies on the same line as p1 and p2
				return x4 >= MathF.Min(x1, x2) && x4 <= MathF.Max(x2, x1);
			}

			if (d123 * d124 >= 0.0) {
				return false;
			}

			var d341 = (x3 - x1) * (y4 - y1) - (x4 - x1) * (y3 - y1);

			if (d341 == 0.0) { // p1 lies on the same line as p3 and p4
				return x1 >= MathF.Min(x3, x4) && x1 <= MathF.Max(x3, x4);
			}

			var d342 = d123 - d124 + d341;

			if (d342 == 0.0) { // p1 lies on the same line as p3 and p4
				return x2 >= MathF.Min(x3, x4) && x2 <= MathF.Max(x3, x4);
			}

			return d341 * d342 < 0.0;
		}
	}
}
