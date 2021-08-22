// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using System.Diagnostics;
using System.Linq;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Math.Mesh;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Table;
using MathF = VisualPinball.Engine.Math.MathF;

namespace VisualPinball.Unity
{
	public class PrimitiveColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		private readonly PrimitiveData _data;
		private readonly PrimitiveMeshGenerator _meshGenerator;

		public PrimitiveColliderGenerator(PrimitiveApi primitiveApi, PrimitiveData data)
		{
			_api = primitiveApi;
			_data = data;
			_meshGenerator = new PrimitiveMeshGenerator(data);
		}

		internal void GenerateColliders(Table table, Mesh originalMesh, List<ICollider> colliders)
		{
			// playfield can't be a toy
			if (_data.IsToy) {
				return;
			}

			var mesh = _meshGenerator.GetTransformedMesh(table, originalMesh, Origin.Global, false);

			var reducedVertices = math.max(
				(uint) MathF.Pow(mesh.Vertices.Length,
					MathF.Clamp(1f - _data.CollisionReductionFactor, 0f, 1f) * 0.25f + 0.75f),
				420u //!! 420 = magic
			);

			if (reducedVertices < mesh.Vertices.Length) {
				mesh = ComputeReducedMesh(mesh, reducedVertices);
			}

			ColliderUtils.GenerateCollidersFromMesh(table, mesh, _api.GetColliderInfo(), colliders);
		}

		private static Mesh ComputeReducedMesh(Mesh mesh, uint reducedVertices)
		{
			var progVertices = mesh.Vertices
				.Select(v =>new ProgMeshFloat3(v.X, v.Y, v.Z))
				.ToArray();

			var progIndices = new ProgMeshTriData[mesh.Indices.Length / 3];
			var i2 = 0;
			for (var i = 0; i < mesh.Indices.Length; i += 3) {
				var t = new ProgMeshTriData(
					mesh.Indices[i],
					mesh.Indices[i + 1],
					mesh.Indices[i + 2]
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
