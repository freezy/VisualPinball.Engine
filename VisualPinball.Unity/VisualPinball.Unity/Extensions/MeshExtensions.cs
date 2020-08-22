using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	public static class MeshExtensions
	{
		public const string AnimationShape = "animation";

		public static Engine.VPT.Mesh ToVpMesh(this Mesh unityMesh)
		{
			var vpMesh = new Engine.VPT.Mesh(unityMesh.name);
			vpMesh.Vertices = new Vertex3DNoTex2[unityMesh.vertexCount];
			var unityVertices = unityMesh.vertices;
			var unityNormals = unityMesh.normals;

			for (int i = 0; i < vpMesh.Vertices.Length; i++) {
				var unityVertex = unityVertices[i];
				var unityNormal = unityNormals[i];
				var unityUv = unityMesh.uv[i];
				vpMesh.Vertices[i] = new Vertex3DNoTex2(
					unityVertex.x, unityVertex.y, unityVertex.z,
					unityNormal.x, unityNormal.y, unityNormal.z,
					unityUv.x, -unityUv.y );
			}
			vpMesh.Indices = unityMesh.triangles;

			if (unityMesh.blendShapeCount > 0) {
				int animationIndex = unityMesh.GetBlendShapeIndex(AnimationShape);

				// use the first blendshape if none with default name
				if (animationIndex < 0) {
					animationIndex = 0;
				}

				Vector3[] deltaVertices = new Vector3[unityMesh.vertexCount];
				Vector3[] deltaNormals = new Vector3[unityMesh.vertexCount];

				int frameCount = unityMesh.GetBlendShapeFrameCount(animationIndex);
				for (int i = 0; i < frameCount; i++) {
					unityMesh.GetBlendShapeFrameVertices(animationIndex, i, deltaVertices, deltaNormals, null);

					Engine.VPT.Mesh.VertData[] frameData = new Engine.VPT.Mesh.VertData[unityMesh.vertexCount];
					for (int j = 0; j < unityMesh.vertexCount; j++) {
						Vector3 vertex = deltaVertices[j] + unityVertices[j];
						Vector3 normal = deltaNormals[j] + unityNormals[j];
						frameData[j] = new Engine.VPT.Mesh.VertData(vertex, normal);
					}

					vpMesh.AnimationFrames.Add(frameData);
				}
			}

			return vpMesh;
		}

		public static Mesh ToUnityMesh(this Engine.VPT.Mesh vpMesh, string name = null)
		{
			var mesh = new Mesh { name = name ?? vpMesh.Name };
			vpMesh.ApplyToUnityMesh(mesh);
			return mesh;
		}

		public static void ApplyToUnityMesh(this Engine.VPT.Mesh vpMesh, Mesh mesh)
		{
			if (vpMesh.Indices.Length > 65535) {
				mesh.indexFormat = IndexFormat.UInt32;
			}

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
			mesh.triangles = null;
			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uv;
			//mesh.RecalculateBounds(); // redundant if setting tringles

			// faces
			mesh.triangles = vpMesh.Indices;

			// animation
			if (vpMesh.AnimationFrames.Count > 0) {
				float deltaWeight = 1f / vpMesh.AnimationFrames.Count;
				Vector3[] deltaVertices = new Vector3[vpMesh.Vertices.Length];
				Vector3[] deltaNormals = new Vector3[vpMesh.Vertices.Length];

				float weight = deltaWeight;
				for (int i = 0; i < vpMesh.AnimationFrames.Count; i++, weight+= deltaWeight) {

					for (int j = 0; j < vpMesh.Vertices.Length; j++) {
						deltaVertices[j] = vpMesh.AnimationFrames[i][j].ToUnityVector3() - vertices[j];

						deltaNormals[j] = vpMesh.AnimationFrames[i][j].ToUnityNormalVector3() - normals[j];
					}
					mesh.AddBlendShapeFrame(AnimationShape, weight, deltaVertices, deltaNormals, null);
				}

				// HACK this is insane and almost certainly a Unity bug
				mesh.RecalculateNormals();
				mesh.RecalculateTangents();
			}
		}
	}
}

