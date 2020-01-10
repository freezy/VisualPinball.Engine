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

		public Mesh Transform(Matrix3D matrix, Matrix3D normalMatrix = null, Func<float, float> getZ = null) {
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

		public Mesh MakeScale(float x, float y, float z)
		{
			foreach (var vertex in Vertices) {
				vertex.X *= x;
				vertex.Y *= y;
				vertex.Z *= z;
			}
			return this;
		}

		public static int[] PolygonToTriangles(RenderVertex[] rgv, int[] pvpoly)
		{
			// There should be this many convex triangles.
			// If not, the polygon is self-intersecting
			var tricount = pvpoly.Length - 2;
			var pvtri = new List<int>();

			for (var l = 0; l < tricount; ++l) {
				for (var i = 0; i < pvpoly.Length; ++i) {
					var s = pvpoly.Length;
					var pre = pvpoly[(i == 0) ? (s - 1) : (i - 1)];
					var a = pvpoly[i];
					var b = pvpoly[(i < s - 1) ? (i + 1) : 0];
					var c = pvpoly[(i < s - 2) ? (i + 2) : ((i + 2) - s)];
					var post = pvpoly[(i < s - 3) ? (i + 3) : ((i + 3) - s)];
					if (Mesh.advancePoint(rgv, pvpoly, a, b, c, pre, post))
					{
						pvtri.Add(a);
						pvtri.Add(c);
						pvtri.Add(b);
						pvpoly.splice((i < s - 1) ? (i + 1) : 0, 1); // b
						break;
					}
				}
			}
			return pvtri.ToArray();
		}
	}
}
