// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

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

			for (var i = 0; i < vpMesh.Vertices.Length; i++) {
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
				var animationIndex = unityMesh.GetBlendShapeIndex(AnimationShape);

				// use the first blendshape if none with default name
				if (animationIndex < 0) {
					animationIndex = 0;
				}

				var deltaVertices = new Vector3[unityMesh.vertexCount];
				var deltaNormals = new Vector3[unityMesh.vertexCount];

				var frameCount = unityMesh.GetBlendShapeFrameCount(animationIndex);
				for (var i = 0; i < frameCount; i++) {
					unityMesh.GetBlendShapeFrameVertices(animationIndex, i, deltaVertices, deltaNormals, null);

					var frameData = new Engine.VPT.Mesh.VertData[unityMesh.vertexCount];
					for (var j = 0; j < unityMesh.vertexCount; j++) {
						var vertex = deltaVertices[j] + unityVertices[j];
						var normal = deltaNormals[j] + unityNormals[j];
						frameData[j] = new Engine.VPT.Mesh.VertData(
							vertex.x, vertex.y, vertex.z,
							normal.x, normal.y, normal.z);
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

		public static void ApplyToVpMesh(this Mesh mesh, Engine.VPT.Mesh vpMesh)
		{
			vpMesh.Vertices = new Vertex3DNoTex2[mesh.vertices.Length];
			for (var i = 0; i < mesh.vertices.Length; i++) {
				vpMesh.Vertices[i] = new Vertex3DNoTex2(
					mesh.vertices[i].x, mesh.vertices[i].y, mesh.vertices[i].z,
					mesh.normals[i].x, mesh.normals[i].y, mesh.normals[i].z,
					mesh.uv[i].x, mesh.uv[i].y
				);
			}
			vpMesh.Indices = mesh.triangles;
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
			//mesh.RecalculateBounds(); // redundant if setting triangles

			// faces
			mesh.triangles = vpMesh.Indices;

			// animation
			if (vpMesh.AnimationFrames.Count == 1) {

				// if there's only one frame, we assume a linear interpolation and just
				// add it in form of UV sets. we then have a shader that interpolates
				// the mesh.

				var deltaVertices = new Vector3[vpMesh.Vertices.Length];
				var deltaNormals = new Vector3[vpMesh.Vertices.Length];

				var blendVertices = vpMesh.AnimationFrames[0];
				for (var i = 0; i < vpMesh.Vertices.Length; i++) {
					deltaVertices[i] = blendVertices[i].ToUnityVector3() - vertices[i];
					deltaNormals[i] = blendVertices[i].ToUnityNormalVector3() - normals[i];
				}
				mesh.SetUVs(Engine.VPT.Mesh.AnimationUVChannelVertices, deltaVertices);
				mesh.SetUVs(Engine.VPT.Mesh.AnimationUVChannelNormals, deltaNormals);

			} else if (vpMesh.AnimationFrames.Count > 0) {

				var deltaWeight = 1f / vpMesh.AnimationFrames.Count;
				var deltaVertices = new Vector3[vpMesh.Vertices.Length];
				var deltaNormals = new Vector3[vpMesh.Vertices.Length];

				var weight = deltaWeight;
				mesh.ClearBlendShapes();
				foreach (var vertData in vpMesh.AnimationFrames) {
					for (var j = 0; j < vpMesh.Vertices.Length; j++) {
						deltaVertices[j] = vertData[j].ToUnityVector3() - vertices[j];
						deltaNormals[j] = vertData[j].ToUnityNormalVector3() - normals[j];
					}
					mesh.AddBlendShapeFrame(AnimationShape, weight, deltaVertices, deltaNormals, null);
					weight += deltaWeight;
				}

				// HACK this is insane and almost certainly a Unity bug
				mesh.RecalculateNormals();
				mesh.RecalculateTangents();
			}
		}

		public static Vector3 ToUnityVector3(this Engine.VPT.Mesh.VertData vpVert)
		{
			return new Vector3(vpVert.X, vpVert.Y, vpVert.Z);
		}

		public static Vector3 ToUnityNormalVector3(this Engine.VPT.Mesh.VertData vpVert)
		{
			return new Vector3(vpVert.Nx, vpVert.Ny, vpVert.Nz);
		}
	}
}

