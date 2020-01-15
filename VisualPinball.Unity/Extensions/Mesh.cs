using UnityEngine;
using UnityEngine.UIElements;
using VisualPinball.Engine.Game;
namespace VisualPinball.Unity.Extensions
{
	public static class Mesh
	{
		public static UnityEngine.Mesh ToUnityMesh(this Engine.VPT.Mesh vpMesh, string name = null)
		{
			var mesh = new UnityEngine.Mesh { name = name ?? vpMesh.Name };

			// vertices
			var vertices = new Vector3[vpMesh.Vertices.Length];
			var normals = new Vector3[vpMesh.Vertices.Length];
			var uv = new Vector2[vpMesh.Vertices.Length];
			for (var i = 0; i < vertices.Length; i++) {
				var vertex = vpMesh.Vertices[i];
				vertices[i] = vertex.ToUnityVector3();
				normals[i] = vertex.ToUnityNormalVector3();
				uv[i] = vertex.ToUnityUvVector2();
			}
			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uv;
			mesh.RecalculateBounds();

			// faces
			mesh.triangles = vpMesh.Indices;

			return mesh;
		}
	}
}

