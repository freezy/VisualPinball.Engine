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

using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	public class RubberColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		private readonly RubberMeshGenerator _meshGenerator;
		private readonly float4x4 _matrix;

		public RubberColliderGenerator(RubberApi rubberApi, RubberMeshGenerator meshGenerator, float4x4 matrix)
		{
			_api = rubberApi;
			_meshGenerator = meshGenerator;
			_matrix = matrix;
		}

		internal void GenerateColliders(float playfieldHeight, float hitHeight, int detailLevel, ref ColliderReference colliders, float margin)
		{
			var mesh = _meshGenerator.GetTransformedMesh(playfieldHeight, hitHeight, detailLevel, 6, true, margin); //!! adapt hacky code in the function if changing the "6" here
			var addedEdges = EdgeSet.Get(Allocator.TempJob);

			// add collision triangles and edges
			for (var i = 0; i < mesh.Indices.Length; i += 3) {
				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var rg0 = mesh.Vertices[mesh.Indices[i]].ToUnityFloat3();
				var rg1 = mesh.Vertices[mesh.Indices[i + 2]].ToUnityFloat3();
				var rg2 = mesh.Vertices[mesh.Indices[i + 1]].ToUnityFloat3();

				colliders.Add(new TriangleCollider(rg0, rg1, rg2, _api.GetColliderInfo()), _matrix);

				GenerateHitEdge(mesh, ref addedEdges, mesh.Indices[i], mesh.Indices[i + 2], ref colliders);
				GenerateHitEdge(mesh, ref addedEdges, mesh.Indices[i + 2], mesh.Indices[i + 1], ref colliders);
				GenerateHitEdge(mesh, ref addedEdges, mesh.Indices[i + 1], mesh.Indices[i], ref colliders);
			}

			// add collision vertices
			foreach (var mv in mesh.Vertices) {
				colliders.Add(new PointCollider(mv.ToUnityFloat3(), _api.GetColliderInfo()), _matrix);
			}

			addedEdges.Dispose();
		}

		private void GenerateHitEdge(Mesh mesh, ref EdgeSet addedEdges, int i, int j, ref ColliderReference colliders)
		{
			if (addedEdges.ShouldAddHitEdge(i, j)) {
				var v1 = mesh.Vertices[i].ToUnityFloat3();
				var v2 = mesh.Vertices[j].ToUnityFloat3();
				colliders.Add(new Line3DCollider(v1, v2, _api.GetColliderInfo()), _matrix);
			}
		}
	}
}
