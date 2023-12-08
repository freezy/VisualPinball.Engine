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

// ReSharper disable LoopCanBeConvertedToQuery

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Math.Mesh;
using VisualPinball.Engine.VPT;
using Debug = System.Diagnostics.Debug;
using Logger = NLog.Logger;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	public class PrimitiveColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		private readonly IMeshGenerator _meshGenerator;
		private readonly PrimitiveComponent _primitiveComponent;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly ProfilerMarker PerfMarker1 = new("PrimitiveColliderGenerator");
		private static readonly ProfilerMarker PerfMarker2 = new("PrimitiveColliderGenerator.reduce");
		private static readonly ProfilerMarker PerfMarker3 = new("PrimitiveColliderGenerator.generate");

		public PrimitiveColliderGenerator(IApiColliderGenerator primitiveApi, IMeshGenerator meshGenerator, PrimitiveComponent primitiveComponent)
		{
			_api = primitiveApi;
			_meshGenerator = meshGenerator;
			_primitiveComponent = primitiveComponent;
		}

		internal void GenerateColliders(float collisionReductionFactor, ref ColliderReference colliders)
		{
			PerfMarker1.Begin();
			//var mesh = _meshGenerator.GetMesh();
			var unityMesh = _primitiveComponent.GetUnityMesh();
			if (unityMesh == null) {
				Logger.Warn($"Primitive {_meshGenerator.name} did not return a mesh for collider generation.");
				return;
			}

			using var meshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(unityMesh);
			var meshData = meshDataArray[0];
			var subMesh = meshData.GetSubMesh(0); // todo loop through all sub meshes?
			// var vpMesh = new Mesh(unityMesh.name) {
			// 	Vertices = new Vertex3DNoTex2[meshData.vertexCount]
			// };
			var unityVertices = new NativeArray<Vector3>(meshData.vertexCount, Allocator.TempJob);
			var unityIndices = new NativeArray<int>(subMesh.indexCount, Allocator.TempJob);
			meshData.GetVertices(unityVertices);
			meshData.GetIndices(unityIndices, 0);

			var reducedVertices = math.max(
				(uint) math.pow(meshData.vertexCount,
					math.clamp(1f - collisionReductionFactor, 0f, 1f) * 0.25f + 0.75f),
				420u //!! 420 = magic
			);

			PerfMarker2.Begin();
			if (reducedVertices < meshData.vertexCount) {
				var mesh = ComputeReducedMesh(in unityVertices, in unityIndices, reducedVertices);
				unityIndices.Dispose();
				unityVertices.Dispose();

				var meshVertices = mesh.Vertices.Select(v => v.ToUnityVector3()).ToArray();
				unityVertices = new NativeArray<Vector3>(meshVertices, Allocator.TempJob);
				unityIndices = new NativeArray<int>(mesh.Indices, Allocator.TempJob);
			}
			PerfMarker2.End();

			PerfMarker3.Begin();
			var worldToVpx = (Matrix4x4)_primitiveComponent.TransformationWithinPlayfield.TransformToVpx();
			ColliderUtils.GenerateCollidersFromMesh(in unityVertices, in unityIndices, ref worldToVpx, _api.GetColliderInfo(), ref colliders);
			PerfMarker3.End();
			PerfMarker1.End();

			unityVertices.Dispose();
			unityIndices.Dispose();
		}

		private static Mesh ComputeReducedMesh(in NativeArray<Vector3> vertices, in NativeArray<int> indices, uint reducedVertices)
		{
			var progVertices = vertices
				.Select(v => new ProgMeshFloat3(v.x, v.y, v.z))
				.ToArray();

			var progIndices = new ProgMeshTriData[indices.Length / 3];
			var i2 = 0;
			for (var i = 0; i < indices.Length; i += 3) {
				var t = new ProgMeshTriData(
					indices[i],
					indices[i + 1],
					indices[i + 2]
				);
				if (t.V[0] != t.V[1] && t.V[1] != t.V[2] && t.V[2] != t.V[0]) {
					progIndices[i2++] = t;
				}
			}

			Debug.Assert(progIndices.Length == i2);
			var (progMap, progPerm) = new ProgMesh().ProgressiveMesh(progVertices, progIndices);
			ProgMeshUtil.PermuteVertices(progPerm, progVertices, progIndices);

			var progNewIndices = new List<ProgMeshTriData>();
			ProgMesh.ReMapIndices(reducedVertices, progIndices, progNewIndices, progMap);

			var reducedIndices = new List<int>();
			foreach (var index in progNewIndices) {
				reducedIndices.Add(index.V[0]);
				reducedIndices.Add(index.V[1]);
				reducedIndices.Add(index.V[2]);
			}

			return new Mesh(
				progVertices.Select(pv => new Vertex3DNoTex2(pv.X, pv.Y, pv.Z)).ToArray(),
				reducedIndices.ToArray()
			);
		}
	}
}
