﻿using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Extensions
{
	public static class MeshExtensions
	{
		public static Engine.VPT.Mesh ToVpMesh(this UnityEngine.Mesh unityMesh)
		{
			var vpMesh = new Engine.VPT.Mesh(unityMesh.name);
			vpMesh.Vertices = new Engine.Math.Vertex3DNoTex2[unityMesh.vertexCount];
			for (int i = 0; i < vpMesh.Vertices.Length; i++) {
				var unityVertex = unityMesh.vertices[i];
				var unityNormal = unityMesh.normals[i];
				var unityUv = unityMesh.uv[i];
				vpMesh.Vertices[i] = new Vertex3DNoTex2(
					unityVertex.x, unityVertex.y, unityVertex.z,
					unityNormal.x, unityNormal.y, unityNormal.z,
					unityUv.x, -unityUv.y );
			}
			vpMesh.Indices = unityMesh.triangles;
			return vpMesh;
		}

		public static UnityEngine.Mesh ToUnityMesh(this Engine.VPT.Mesh vpMesh, string name = null)
		{
			var mesh = new UnityEngine.Mesh { name = name ?? vpMesh.Name };
			vpMesh.ApplyToUnityMesh(mesh);
			return mesh;
		}

		public static void ApplyToUnityMesh(this Engine.VPT.Mesh vpMesh, UnityEngine.Mesh mesh)
		{
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
			mesh.RecalculateBounds();

			// faces
			mesh.triangles = vpMesh.Indices;
		}
	}
}

