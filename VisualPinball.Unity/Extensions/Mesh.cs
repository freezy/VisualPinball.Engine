using UnityEngine;

namespace VisualPinball.Unity.Extensions
{
	public static class Mesh
	{
		public static UnityEngine.Mesh ToUnityMesh(this Engine.VPT.Mesh vpMesh)
		{
			var mesh = new UnityEngine.Mesh { name = vpMesh.Name };

			var matrixRot = new Matrix4x4();
			matrixRot.SetTRS(Vector3.zero, Quaternion.Euler(-90, 0, 0), new Vector3(0.01f, 0.01f, 0.01f));

			// vertices
			var vertices = new Vector3[vpMesh.Vertices.Length];
			var normals = new Vector3[vpMesh.Vertices.Length];
			var uv = new Vector2[vpMesh.Vertices.Length];
			for (var i = 0; i < vertices.Length; i++) {
				var vertex = vpMesh.Vertices[i];
				vertices[i] = vertices[i] = matrixRot.MultiplyPoint(vertex.ToUnityVector3());
				normals[i] = matrixRot.MultiplyPoint(vertex.ToUnityNormalVector3());
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
