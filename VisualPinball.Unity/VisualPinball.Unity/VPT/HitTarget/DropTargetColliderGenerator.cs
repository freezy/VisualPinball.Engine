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
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	public class DropTargetColliderGenerator : TargetColliderGenerator
	{
		public DropTargetColliderGenerator(IApiColliderGenerator api, TargetComponent comp, float4x4 matrix)
			: base(api, comp, matrix) { }

		internal void GenerateColliders(ref ColliderReference colliders)
		{
			// QUICK FIX and TODO for Cupiii
			/* hitmesh should not be generated by the Mesh generator. Drop Targets need special Hitshapes, that shoujld be very simple and cannot be activated from behind.
			var hitMesh = MeshGenerator.GetMesh();
			for (var i = 0; i < hitMesh.Vertices.Length; i++) {
				hitMesh.Vertices[i].MultiplyMatrix(localToPlayfield);
			}
			
			var addedEdges = EdgeSet.Get();
			
			GenerateCollidables(hitMesh, addedEdges, Data.IsLegacy, colliders);
			*/
			var addedEdges = EdgeSet.Get(Allocator.TempJob);

			//if (!Data.IsLegacy)    // Always generate special hitshapes (QUICKFIX)
			{
				var rgv3D = new Vertex3D[DropTargetHitPlaneVertices.Length];
				var hitShapeOffset = 0.18f;
				if (Data.TargetType == TargetType.DropTargetBeveled) {
					hitShapeOffset = 0.25f;
				}
				if (Data.TargetType == TargetType.DropTargetFlatSimple) {
					hitShapeOffset = 0.13f;
				}

				// now create a special hit shape with hit event enabled to prevent a hit event when hit from behind
				for (var i = 0; i < DropTargetHitPlaneVertices.Length; i++) {
					var dropTargetHitPlaneVertex = DropTargetHitPlaneVertices[i];
					var vert = new Vertex3D(
						dropTargetHitPlaneVertex.x,
						dropTargetHitPlaneVertex.y + hitShapeOffset,
						dropTargetHitPlaneVertex.z
					);

					rgv3D[i] = new Vertex3D(vert.X, vert.Y, vert.Z);
				}

				for (var i = 0; i < DropTargetHitPlaneIndices.Length; i += 3) {
					var i0 = DropTargetHitPlaneIndices[i];
					var i1 = DropTargetHitPlaneIndices[i + 1];
					var i2 = DropTargetHitPlaneIndices[i + 2];

					// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
					var rgv0 = rgv3D[i0].ToUnityFloat3();
					var rgv1 = rgv3D[i1].ToUnityFloat3();
					var rgv2 = rgv3D[i2].ToUnityFloat3();

					colliders.Add(new TriangleCollider(rgv0, rgv2, rgv1, GetColliderInfo(true)), Matrix);

					if (addedEdges.ShouldAddHitEdge(i0, i1)) {
						colliders.Add(new Line3DCollider(rgv0, rgv2, GetColliderInfo(true)), Matrix);
					}
					if (addedEdges.ShouldAddHitEdge(i1, i2)) {
						colliders.Add(new Line3DCollider(rgv2, rgv1, GetColliderInfo(true)), Matrix);
					}
					if (addedEdges.ShouldAddHitEdge(i2, i0)) {
						colliders.Add(new Line3DCollider(rgv1, rgv0, GetColliderInfo(true)), Matrix);
					}
				}

				// add collision vertices
				for (var i = 0; i < DropTargetHitPlaneVertices.Length; ++i) {
					colliders.Add(new PointCollider(rgv3D[i].ToUnityFloat3(), GetColliderInfo(true)), Matrix);
				}
			}

			addedEdges.Dispose();
		}

		private static readonly float3[] DropTargetHitPlaneVertices = {
			new float3(-0.300000f, 0.001737f, -0.160074f) * 32f,
			new float3(-0.300000f, 0.001738f, 0.439926f) * 32f,
			new float3(0.300000f, 0.001738f, 0.439926f) * 32f,
			new float3(0.300000f, 0.001737f, -0.160074f) * 32f,
			new float3(-0.500000f, 0.001738f, 0.439926f) * 32f,
			new float3(-0.500000f, 0.001738f, 1.789926f) * 32f,
			new float3(0.500000f, 0.001738f, 1.789926f) * 32f,
			new float3(0.500000f, 0.001738f, 0.439926f) * 32f,
			new float3(-0.535355f, 0.001738f, 0.454570f) * 32f,
			new float3(-0.535355f, 0.001738f, 1.775281f) * 32f,
			new float3(-0.550000f, 0.001738f, 0.489926f) * 32f,
			new float3(-0.550000f, 0.001738f, 1.739926f) * 32f,
			new float3(0.535355f, 0.001738f, 0.454570f) * 32f,
			new float3(0.535355f, 0.001738f, 1.775281f) * 32f,
			new float3(0.550000f, 0.001738f, 0.489926f) * 32f,
			new float3(0.550000f, 0.001738f, 1.739926f * 32f)
		};

		private static readonly int[] DropTargetHitPlaneIndices = {
			0, 1, 2, 2, 3, 0, 1, 4, 5, 6, 7, 2, 5, 6, 1,
			2, 1, 6, 4, 8, 9, 9, 5, 4, 8, 10, 11, 11, 9, 8,
			6, 12, 7, 12, 6, 13, 12, 13, 14, 13, 15, 14,
		};
	}
}
