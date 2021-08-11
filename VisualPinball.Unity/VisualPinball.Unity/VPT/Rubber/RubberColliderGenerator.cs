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

using System.Collections.Generic;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class RubberColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		private readonly RubberMeshGenerator _meshGenerator;

		public RubberColliderGenerator(RubberApi rubberApi, RubberMeshGenerator meshGenerator)
		{
			_api = rubberApi;
			_meshGenerator = meshGenerator;
		}

		internal void GenerateColliders(Table table, List<ICollider> colliders)
		{
			var mesh = _meshGenerator.GetMesh(table, 6, true); //!! adapt hacky code in the function if changing the "6" here
			var addedEdges = EdgeSet.Get();

			// add collision triangles and edges
			for (var i = 0; i < mesh.Indices.Length; i += 3) {
				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var rg0 = mesh.Vertices[mesh.Indices[i]].ToUnityFloat3();
				var rg1 = mesh.Vertices[mesh.Indices[i + 2]].ToUnityFloat3();
				var rg2 = mesh.Vertices[mesh.Indices[i + 1]].ToUnityFloat3();

				colliders.Add(new TriangleCollider(rg0, rg1, rg2, _api.GetColliderInfo()));

				GenerateHitEdge(mesh, addedEdges, mesh.Indices[i], mesh.Indices[i + 2], table, colliders);
				GenerateHitEdge(mesh, addedEdges, mesh.Indices[i + 2], mesh.Indices[i + 1], table, colliders);
				GenerateHitEdge(mesh, addedEdges, mesh.Indices[i + 1], mesh.Indices[i], table, colliders);
			}

			// add collision vertices
			foreach (var mv in mesh.Vertices) {
				colliders.Add(new PointCollider(mv.ToUnityFloat3(), _api.GetColliderInfo()));
			}
		}

		private void GenerateHitEdge(Mesh mesh, EdgeSet addedEdges, int i, int j,
			Table table, ICollection<ICollider> colliders)
		{
			if (addedEdges.ShouldAddHitEdge(i, j)) {
				var v1 = mesh.Vertices[i].ToUnityFloat3();
				var v2 = mesh.Vertices[j].ToUnityFloat3();
				colliders.Add(new Line3DCollider(v1, v2, _api.GetColliderInfo()));
			}
		}
	}
}
