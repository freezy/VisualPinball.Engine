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

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	public struct KickerMeshVertexBlobAsset
	{
		public BlobArray<float3> Vertices;
		public BlobArray<float3> Normals;
	}

	internal struct ColliderMeshData
	{
		public BlobAssetReference<KickerMeshVertexBlobAsset> Value;

		public ColliderMeshData(IList<Vertex3DNoTex2> vertices, float radius, float3 position)
		{
			var rad = radius * 0.8f;
			using var blobBuilder = new BlobBuilder(Allocator.Temp);
			ref var blobAsset = ref blobBuilder.ConstructRoot<KickerMeshVertexBlobAsset>();
			var blobVertices = blobBuilder.Allocate(ref blobAsset.Vertices, vertices.Count);
			var blobNormals = blobBuilder.Allocate(ref blobAsset.Normals, vertices.Count);
			for (var i = 0; i < vertices.Count; i++) {
				blobVertices[i] = new float3(
					vertices[i].X * rad + position.x,
					vertices[i].Y * rad + position.y,
					vertices[i].Z * rad + position.z
				);
				blobNormals[i] = new float3(vertices[i].Nx, vertices[i].Ny, vertices[i].Nz);
			}
			Value = blobBuilder.CreateBlobAssetReference<KickerMeshVertexBlobAsset>(Allocator.Persistent);
		}
	}
}
