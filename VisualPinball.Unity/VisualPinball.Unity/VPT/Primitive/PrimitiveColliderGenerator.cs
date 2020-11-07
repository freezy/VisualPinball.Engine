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

namespace VisualPinball.Unity
{
	public class PrimitiveColliderGenerator
	{
		private readonly PrimitiveApi _api;
		private readonly PrimitiveData _data;
		private readonly PrimitiveMeshGenerator _meshGenerator;
		private bool _useAsPlayfield;

		public PrimitiveColliderGenerator(PrimitiveApi primitiveApi)
		{
			_api = primitiveApi;
			_data = primitiveApi.Data;
			_meshGenerator = primitiveApi.Item.MeshGenerator;
		}

		internal void GenerateColliders(Table table, List<ICollider> colliders, ref int nextColliderId)
		{
			if (_data.Name == "playfield_mesh") {
				_data.IsVisible = false;
				_useAsPlayfield = true;
			}

			// playfield can't be a toy
			if (_data.IsToy && !_useAsPlayfield) {
				return;
			}

			var mesh = _meshGenerator.GetTransformedMesh(table, Origin.Global, false);

			var reducedVertices = math.max(
				(uint) MathF.Pow(mesh.Vertices.Length,
					MathF.Clamp(1f - _data.CollisionReductionFactor, 0f, 1f) * 0.25f + 0.75f),
				420u //!! 420 = magic
			);

			if (reducedVertices < mesh.Vertices.Length) {
				mesh = ComputeReducedMesh(mesh, reducedVertices);
			}

			GenerateCollidersFromMesh(table, mesh, colliders, ref nextColliderId);
		}

		private void GenerateCollidersFromMesh(Table table, Mesh mesh, ICollection<ICollider> colliders, ref int nextColliderId, bool onlyTriangles = false)
		{
			var addedEdges = EdgeSetBetter.Get(mesh.Vertices.Length);

			// add collision triangles and edges
			for (var i = 0; i < mesh.Indices.Length; i += 3) {
				var i0 = mesh.Indices[i];
				var i1 = mesh.Indices[i + 1];
				var i2 = mesh.Indices[i + 2];


				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var rgv0 = mesh.Vertices[i0].GetVertex().ToUnityFloat3();
				var rgv1 = mesh.Vertices[i1].GetVertex().ToUnityFloat3();
				var rgv2 = mesh.Vertices[i2].GetVertex().ToUnityFloat3();

				// todo handle playfield mehs

				colliders.Add(new TriangleCollider(rgv0, rgv2, rgv1, _api.GetNextColliderInfo(table, ref nextColliderId)));

				if (!onlyTriangles) {

					if (addedEdges.ShouldAddHitEdge(i0, i1)) {
						colliders.Add(new Line3DCollider(rgv0, rgv2, _api.GetNextColliderInfo(table, ref nextColliderId)));
					}
					if (addedEdges.ShouldAddHitEdge(i1, i2)) {
						colliders.Add(new Line3DCollider(rgv2, rgv1, _api.GetNextColliderInfo(table, ref nextColliderId)));
					}
					if (addedEdges.ShouldAddHitEdge(i2, i0)) {
						colliders.Add(new Line3DCollider(rgv1, rgv0, _api.GetNextColliderInfo(table, ref nextColliderId)));
					}
				}
			}

			// add collision vertices
			if (!onlyTriangles) {
				foreach (var vertex in mesh.Vertices) {
					colliders.Add(new PointCollider(vertex.ToUnityFloat3(), _api.GetNextColliderInfo(table, ref nextColliderId)));
				}
			}
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

		// private HitObject SetupHitObject(HitObject obj, Table table)
		// {
		// 	if (!_primitive.UseAsPlayfield) {
		// 		obj.ApplyPhysics(_data, table);
		//
		// 	} else {
		// 		obj.SetElasticity(table.Data.Elasticity, table.Data.ElasticityFalloff);
		// 		obj.SetFriction(table.Data.Friction);
		// 		obj.SetScatter(MathF.DegToRad(table.Data.Scatter));
		// 		obj.SetEnabled(true);
		// 	}
		//
		// 	obj.Threshold = _data.Threshold;
		// 	obj.E = true;
		// 	obj.FireEvents = _data.HitEvent;
		// 	return obj;
		// }
	}
}
