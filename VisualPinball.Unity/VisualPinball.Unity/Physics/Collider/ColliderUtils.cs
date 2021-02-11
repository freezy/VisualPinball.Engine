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
using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public static class ColliderUtils
	{
		public static void Generate3DPolyColliders(in float3[] rgv, Table table, ColliderInfo info, ICollection<ICollider> colliders)
		{
			var inputVerts = new float2[rgv.Length];

			// Newell's method for normal computation
			for (var i = 0; i < rgv.Length; ++i) {
				inputVerts[i] = rgv[i].xy;
			}

			// todo make triangulator use float3
			Triangulator.Triangulate(inputVerts, WindingOrder.CounterClockwise, out var outputVerts, out var outputIndices);

			var triangulatedVerts = new Vertex3DNoTex2[outputVerts.Length];
			for (var i = 0; i < outputVerts.Length; i++) {
				triangulatedVerts[i] = new Vertex3DNoTex2(outputVerts[i].x, outputVerts[i].y, rgv[0].z);
			}
			var mesh = new Mesh(triangulatedVerts, outputIndices);

			GenerateCollidersFromMesh(table, mesh, info, colliders, true);
		}

		public static void GenerateCollidersFromMesh(Table table, Mesh mesh, ColliderInfo info, ICollection<ICollider> colliders, bool onlyTriangles = false)
		{
			var addedEdges = EdgeSetBetter.Get();
			var collCount = colliders.Count;

			// add collision triangles and edges
			for (var i = 0; i < mesh.Indices.Length; i += 3) {
				var i0 = mesh.Indices[i];
				var i1 = mesh.Indices[i + 1];
				var i2 = mesh.Indices[i + 2];


				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var rgv0 = mesh.Vertices[i0].GetVertex().ToUnityFloat3();
				var rgv1 = mesh.Vertices[i1].GetVertex().ToUnityFloat3();
				var rgv2 = mesh.Vertices[i2].GetVertex().ToUnityFloat3();

				colliders.Add(new TriangleCollider(rgv0, rgv2, rgv1, info));

				if (!onlyTriangles) {

					if (addedEdges.ShouldAddHitEdge(i0, i1)) {
						colliders.Add(new Line3DCollider(rgv0, rgv2, info));
					}
					if (addedEdges.ShouldAddHitEdge(i1, i2)) {
						colliders.Add(new Line3DCollider(rgv2, rgv1, info));
					}
					if (addedEdges.ShouldAddHitEdge(i2, i0)) {
						colliders.Add(new Line3DCollider(rgv1, rgv0, info));
					}
				}
			}

			// add collision vertices
			if (!onlyTriangles) {
				foreach (var vertex in mesh.Vertices) {
					colliders.Add(new PointCollider(vertex.ToUnityFloat3(), info));
				}
			}
		}
	}
}
