// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Engine.Math;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	public static class MeshExtensions
	{
		public const string AnimationShape = "animation";

		public static Mesh ToVpMesh(this UnityEngine.Mesh unityMesh)
		{
			using var meshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(unityMesh);
			var meshData = meshDataArray[0];
			var vpMesh = new Mesh(unityMesh.name) {
				Vertices = new Vertex3DNoTex2[meshData.vertexCount]
			};
			var unityVertices = new NativeArray<Vector3>(meshData.vertexCount, Allocator.TempJob);
			var unityNormals = new NativeArray<Vector3>(meshData.vertexCount, Allocator.TempJob);
			meshData.GetVertices(unityVertices);
			meshData.GetNormals(unityNormals);

			for (var i = 0; i < vpMesh.Vertices.Length; i++) {
				var unityUv = unityMesh.uv[i];
				vpMesh.Vertices[i] = new Vertex3DNoTex2(
					unityVertices[i].x, unityVertices[i].y, unityVertices[i].z,
					unityNormals[i].x, unityNormals[i].y, unityNormals[i].z,
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

					var frameData = new Mesh.VertData[unityMesh.vertexCount];
					for (var j = 0; j < unityMesh.vertexCount; j++) {
						frameData[j] = new Mesh.VertData(
							deltaVertices[j].x + unityVertices[j].x, deltaVertices[j].y + unityVertices[j].y, deltaVertices[j].z + unityVertices[j].z,
							deltaNormals[j].x + unityNormals[j].x, deltaNormals[j].y + unityNormals[j].y, deltaNormals[j].z + unityNormals[j].z);
					}

					vpMesh.AnimationFrames.Add(frameData);
				}
			}

			unityVertices.Dispose();
			unityNormals.Dispose();

			return vpMesh;
		}

		public static UnityEngine.Mesh ToUnityMesh(this Mesh vpMesh, string name = null)
		{
			var mesh = new UnityEngine.Mesh { name = name ?? vpMesh.Name };
			vpMesh.ApplyToUnityMesh(mesh);
			return mesh;
		}

		public static void ApplyToUnityMesh(this Mesh vpMesh, UnityEngine.Mesh mesh)
		{
			// sometime we get empty meshes, e.g. when generating wire meshes for a non-wire ramp, so handle accordingly.
			if (vpMesh.Indices == null || vpMesh.Vertices == null) {
				mesh.triangles = null;
				mesh.vertices = Array.Empty<Vector3>();
				mesh.normals = Array.Empty<Vector3>();
				mesh.uv = Array.Empty<Vector2>();
				return;
			}

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
			if (vpMesh.AnimationFrames.Count > 0) {

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

		public static Vector3 ToUnityVector3(this Mesh.VertData vpVert)
		{
			return new Vector3(vpVert.X, vpVert.Y, vpVert.Z);
		}

		public static Vector3 ToUnityNormalVector3(this Mesh.VertData vpVert)
		{
			return new Vector3(vpVert.Nx, vpVert.Ny, vpVert.Nz);
		}
	}
}
