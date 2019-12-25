using System;
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
	}
}
