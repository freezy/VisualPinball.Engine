// ReSharper disable CompareOfFloatsByEqualityOperator

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using VisualPinball.Engine.Common;
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
	[Serializable]
	public class Mesh
	{
		public string Name;
		public Vertex3DNoTex2[] Vertices;
		public int[] Indices;
		public bool IsSet => Vertices != null && Indices != null;

		public List<VertData[]> AnimationFrames;

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

		public static void ClosestPointOnPolygon(RenderVertex3D[] rgv, Vertex2D pvin, bool fClosed, out Vertex2D pvOut, out int piSeg)
		{
			var count = rgv.Length;
			var minDist = Constants.FloatMax;
			piSeg = -1; // in case we are not next to the line
			pvOut = new Vertex2D();
			var loopCount = count;
			if (!fClosed) {
				--loopCount; // Don"t check segment running from the end point to the beginning point
			}

			// Go through line segment, calculate distance from point to the line
			// then pick the shortest distance
			for (var i = 0; i < loopCount; ++i) {
				var p2 = i < count - 1 ? i + 1 : 0;

				var rgvi = new RenderVertex3D();
				rgvi.Set(rgv[i].X, rgv[i].Y, rgv[i].Z);
				var rgvp2 = new RenderVertex3D();
				rgvp2.Set(rgv[p2].X, rgv[p2].Y, rgv[p2].Z);
				var a = rgvi.Y - rgvp2.Y;
				var b = rgvp2.X - rgvi.X;
				var c = -(a * rgvi.X + b * rgvi.Y);

				var dist = MathF.Abs(a * pvin.X + b * pvin.Y + c) / MathF.Sqrt(a * a + b * b);

				if (dist < minDist) {
					// Assuming we got a segment that we are closet to, calculate the intersection
					// of the line with the perpendicular line projected from the point,
					// to find the closest point on the line
					var d = -b;
					var f = -(d * pvin.X + a * pvin.Y);

					var det = a * a - b * d;
					var invDet = det != 0.0f ? 1.0f / det : 0.0f;
					var intersectX = (b * f - a * c) * invDet;
					var intersectY = (c * d - a * f) * invDet;

					// If the intersect point lies on the polygon segment
					// (not out in space), then make this the closest known point
					if (intersectX >= MathF.Min(rgvi.X, rgvp2.X) - 0.1 &&
					    intersectX <= MathF.Max(rgvi.X, rgvp2.X) + 0.1 &&
					    intersectY >= MathF.Min(rgvi.Y, rgvp2.Y) - 0.1 &&
					    intersectY <= MathF.Max(rgvi.Y, rgvp2.Y) + 0.1) {
						minDist = dist;
						var seg = i;

						pvOut.X = intersectX;
						pvOut.Y = intersectY;
						piSeg = seg;
					}
				}
			}
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
			var minX = MathF.Min(pv1.GetX(), pv3.GetX());
			var maxX = MathF.Max(pv1.GetX(), pv3.GetX());
			var minY = MathF.Min(pv1.GetY(), pv3.GetY());
			var maxY = MathF.Max(pv1.GetY(), pv3.GetY());

			for (var i = 0; i < poly.Count; ++i) {
				var pvCross1 = rgv[poly[i]];
				var pvCross2 = rgv[poly[i < poly.Count - 1 ? i + 1 : 0]];

				if (pvCross1 != pv1 && pvCross2 != pv1 && pvCross1 != pv3 && pvCross2 != pv3 &&
				    (pvCross1.GetY() >= minY || pvCross2.GetY() >= minY) &&
				    (pvCross1.GetY() <= maxY || pvCross2.GetY() <= maxY) &&
				    (pvCross1.GetX() >= minX || pvCross2.GetX() >= minX) &&
				    (pvCross1.GetX() <= maxX || pvCross2.GetX() <= maxX) &&
				    LinesIntersect(pv1, pv3, pvCross1, pvCross2)) {

					return false;
				}
			}

			return true;
		}

		private static float GetDot(IRenderVertex pvEnd1, IRenderVertex pvJoint, IRenderVertex pvEnd2)
		{
			return (pvJoint.GetX() - pvEnd1.GetX()) * (pvJoint.GetY() - pvEnd2.GetY()) - (pvJoint.GetY() - pvEnd1.GetY()) * (pvJoint.GetX() - pvEnd2.GetX());
		}

		private static bool LinesIntersect(IRenderVertex start1, IRenderVertex start2, IRenderVertex end1, IRenderVertex end2) {

			var x1 = start1.GetX();
			var y1 = start1.GetY();
			var x2 = start2.GetX();
			var y2 = start2.GetY();
			var x3 = end1.GetX();
			var y3 = end1.GetY();
			var x4 = end2.GetX();
			var y4 = end2.GetY();

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


		#region VertData
		/// <summary>
		/// VertData is a utility struct containing position and normal data.<p/>
		///
		/// It is used primarily for storing animation frames.
		/// </summary>
		[Serializable]
		public struct VertData
		{
			public const int Size = 28;

			public float X;
			public float Y;
			public float Z;

			public float Nx;
			public float Ny;
			public float Nz;

			public VertData(BinaryReader reader)
			{
				var startPos = reader.BaseStream.Position;
				X = reader.ReadSingle();
				Y = reader.ReadSingle();
				Z = reader.ReadSingle();
				Nx = reader.ReadSingle();
				Ny = reader.ReadSingle();
				Nz = reader.ReadSingle();
				var remainingSize = Size - (reader.BaseStream.Position - startPos);
				if (remainingSize > 0)
				{
					throw new InvalidOperationException();
				}
			}

			public VertData(IReadOnlyList<float> arr)
			{
				X = arr.Count > 0 ? arr[0] : float.NaN;
				Y = arr.Count > 1 ? arr[1] : float.NaN;
				Z = arr.Count > 2 ? arr[2] : float.NaN;
				Nx = arr.Count > 3 ? arr[3] : float.NaN;
				Ny = arr.Count > 4 ? arr[4] : float.NaN;
				Nz = arr.Count > 5 ? arr[5] : float.NaN;
			}

			public VertData(float x, float y, float z, float nx = float.NaN, float ny = float.NaN, float nz = float.NaN)
			{
				X = x;
				Y = y;
				Z = z;
				Nx = nx;
				Ny = ny;
				Nz = nz;
			}

			public void Write(BinaryWriter writer)
			{
				writer.Write(X);
				writer.Write(Y);
				writer.Write(Z);
				writer.Write(Nx);
				writer.Write(Ny);
				writer.Write(Nz);
			}

			public Vertex3D GetVertex()
			{
				return new Vertex3D(X, Y, Z);
			}

			public Vertex3D GetNormal()
			{
				return new Vertex3D(Nx, Ny, Nz);
			}

			public VertData Clone()
			{
				var vertex = new VertData
				{
					X = X,
					Y = Y,
					Z = Z,
					Nx = Nx,
					Ny = Ny,
					Nz = Nz
				};
				return vertex;
			}

			public override string ToString()
			{
				return $"VertData({X}/{Y}/{Z}, {Nx}/{Ny}/{Nz})";
			}
		}
		#endregion

	}

}
