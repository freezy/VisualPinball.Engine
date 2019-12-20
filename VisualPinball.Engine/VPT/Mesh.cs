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
	}
}
