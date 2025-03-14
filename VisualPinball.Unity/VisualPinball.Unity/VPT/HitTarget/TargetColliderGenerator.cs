﻿// Visual Pinball Engine
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

using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT;
using Mesh = UnityEngine.Mesh;

namespace VisualPinball.Unity
{
	public class TargetColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		private readonly float4x4 _matrix;

		public TargetColliderGenerator(IApiColliderGenerator api, float4x4 matrix)
		{
			_api = api;
			_matrix = matrix;
		}

		internal void GenerateColliders(ref ColliderReference colliders, Mesh colliderMesh, ItemType itemType, MonoBehaviour mainComp)
		{
			if (colliderMesh == null) {
				Debug.LogWarning($"Collider mesh of target \"{mainComp.name}\" is not set. Target will not fully work.");
				return;
			}
			using var meshDataArray = Mesh.AcquireReadOnlyMeshData(colliderMesh);
			var meshData = meshDataArray[0];
			var subMesh = meshData.GetSubMesh(0); // todo loop through all sub meshes?
			var unityVertices = new NativeArray<Vector3>(meshData.vertexCount, Allocator.TempJob);
			var unityIndices = new NativeArray<int>(subMesh.indexCount, Allocator.TempJob);
			meshData.GetVertices(unityVertices);
			meshData.GetIndices(unityIndices, 0);

			ColliderUtils.GenerateCollidersFromMesh(in unityVertices, in unityIndices, math.mul(_matrix, Physics.WorldToVpx), _api.GetColliderInfo(itemType), ref colliders);

			unityVertices.Dispose();
			unityIndices.Dispose();
		}
	}
}
