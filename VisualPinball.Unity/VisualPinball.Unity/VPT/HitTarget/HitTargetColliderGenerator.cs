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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class HitTargetColliderGenerator
	{
		private readonly HitTargetApi _api;
		private readonly HitTargetData _data;
		private readonly HitTargetMeshGenerator _meshGenerator;

		public HitTargetColliderGenerator(HitTargetApi hitTargetApi)
		{
			_api = hitTargetApi;
			_data = hitTargetApi.Data;
			_meshGenerator = hitTargetApi.Item.MeshGenerator;
		}

		internal void GenerateColliders(Table table, List<ICollider> colliders)
		{
			if (_data.IsDropTarget) {
				GenerateDropTargetColliders(table, colliders);

			} else {
				GenerateHitTargetColliders(table, colliders);
			}
		}

		private void GenerateHitTargetColliders(Table table, ICollection<ICollider> colliders)
		{
			var rog = _meshGenerator.GetRenderObjects(table, Origin.Original, false);
			var ro = rog.RenderObjects[0];
			var hitMesh = ro.Mesh;
			for (var i = 0; i < hitMesh.Vertices.Length; i++) {
				hitMesh.Vertices[i].MultiplyMatrix(rog.TransformationMatrix);
			}
			var addedEdges = EdgeSet.Get();
			GenerateCollidables(hitMesh, addedEdges, true, table, colliders);
		}

		private void GenerateDropTargetColliders(Table table, ICollection<ICollider> colliders)
		{
			var rog = _meshGenerator.GetRenderObjects(table, Origin.Original, false);
			var ro = rog.RenderObjects[0];
			var hitMesh = ro.Mesh;
			for (var i = 0; i < hitMesh.Vertices.Length; i++) {
				hitMesh.Vertices[i].MultiplyMatrix(rog.TransformationMatrix);
			}
			var addedEdges = EdgeSet.Get();
			GenerateCollidables(hitMesh, addedEdges, _data.IsLegacy, table, colliders);

			var tempMatrix = new Matrix3D().RotateZMatrix(MathF.DegToRad(_data.RotZ));
			var fullMatrix = new Matrix3D().Multiply(tempMatrix);

			if (!_data.IsLegacy) {

				var rgv3D = new Vertex3D[DropTargetHitPlaneVertices.Length];
				var hitShapeOffset = 0.18f;
				if (_data.TargetType == TargetType.DropTargetBeveled) {
					hitShapeOffset = 0.25f;
				}
				if (_data.TargetType == TargetType.DropTargetFlatSimple) {
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

					vert.X *= _data.Size.X;
					vert.Y *= _data.Size.Y;
					vert.Z *= _data.Size.Z;
					vert = vert.MultiplyMatrix(fullMatrix);
					rgv3D[i] = new Vertex3D(
						vert.X + _data.Position.X,
						vert.Y + _data.Position.Y,
						vert.Z * table.GetScaleZ() + _data.Position.Z + table.TableHeight
					);
				}

				for (var i = 0; i < DropTargetHitPlaneIndices.Length; i += 3) {
					var i0 = DropTargetHitPlaneIndices[i];
					var i1 = DropTargetHitPlaneIndices[i + 1];
					var i2 = DropTargetHitPlaneIndices[i + 2];

					// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
					var rgv0 = rgv3D[i0].ToUnityFloat3();
					var rgv1 = rgv3D[i1].ToUnityFloat3();
					var rgv2 = rgv3D[i2].ToUnityFloat3();

					colliders.Add(new TriangleCollider(rgv0, rgv2, rgv1, GetColliderInfo(true)));

					if (addedEdges.ShouldAddHitEdge(i0, i1)) {
						colliders.Add(new Line3DCollider(rgv0, rgv2, GetColliderInfo(true)));
					}
					if (addedEdges.ShouldAddHitEdge(i1, i2)) {
						colliders.Add(new Line3DCollider(rgv2, rgv1, GetColliderInfo(true)));
					}
					if (addedEdges.ShouldAddHitEdge(i2, i0)) {
						colliders.Add(new Line3DCollider(rgv1, rgv0, GetColliderInfo(true)));
					}
				}

				// add collision vertices
				for (var i = 0; i < DropTargetHitPlaneVertices.Length; ++i) {
					colliders.Add(new PointCollider(rgv3D[i].ToUnityFloat3(), GetColliderInfo(true)));
				}
			}
		}

		private void GenerateCollidables(Mesh hitMesh, EdgeSet addedEdges, bool setHitObject, Table table, ICollection<ICollider> colliders)  {

			// add the normal drop target as collidable but without hit event
			for (var i = 0; i < hitMesh.Indices.Length; i += 3) {
				var i0 = hitMesh.Indices[i];
				var i1 = hitMesh.Indices[i + 1];
				var i2 = hitMesh.Indices[i + 2];

				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var rgv0 = hitMesh.Vertices[i0].ToUnityFloat3();
				var rgv1 = hitMesh.Vertices[i1].ToUnityFloat3();
				var rgv2 = hitMesh.Vertices[i2].ToUnityFloat3();

				colliders.Add(new TriangleCollider(rgv0, rgv2, rgv1, GetColliderInfo(setHitObject)));

				if (addedEdges.ShouldAddHitEdge(i0, i1)) {
					colliders.Add(new Line3DCollider(rgv0, rgv2, GetColliderInfo(setHitObject)));
				}
				if (addedEdges.ShouldAddHitEdge(i1, i2)) {
					colliders.Add(new Line3DCollider(rgv2, rgv1, GetColliderInfo(setHitObject)));
				}
				if (addedEdges.ShouldAddHitEdge(i2, i0)) {
					colliders.Add(new Line3DCollider(rgv1, rgv0, GetColliderInfo(setHitObject)));
				}
			}

			// add collision vertices
			foreach (var vertex in hitMesh.Vertices) {
				colliders.Add(new PointCollider(vertex.ToUnityFloat3(), GetColliderInfo(setHitObject)));
			}
		}

		private ColliderInfo GetColliderInfo(bool setHitObject)
		{
			var info = _api.GetColliderInfo();
			info.FireEvents = setHitObject && info.FireEvents;
			return info;
		}

		private static readonly float3[] DropTargetHitPlaneVertices = {
			new float3(-0.300000f, 0.001737f, -0.160074f),
			new float3(-0.300000f, 0.001738f, 0.439926f),
			new float3(0.300000f, 0.001738f, 0.439926f),
			new float3(0.300000f, 0.001737f, -0.160074f),
			new float3(-0.500000f, 0.001738f, 0.439926f),
			new float3(-0.500000f, 0.001738f, 1.789926f),
			new float3(0.500000f, 0.001738f, 1.789926f),
			new float3(0.500000f, 0.001738f, 0.439926f),
			new float3(-0.535355f, 0.001738f, 0.454570f),
			new float3(-0.535355f, 0.001738f, 1.775281f),
			new float3(-0.550000f, 0.001738f, 0.489926f),
			new float3(-0.550000f, 0.001738f, 1.739926f),
			new float3(0.535355f, 0.001738f, 0.454570f),
			new float3(0.535355f, 0.001738f, 1.775281f),
			new float3(0.550000f, 0.001738f, 0.489926f),
			new float3(0.550000f, 0.001738f, 1.739926f)
		};

		private static readonly int[] DropTargetHitPlaneIndices = {
			0, 1, 2, 2, 3, 0, 1, 4, 5, 6, 7, 2, 5, 6, 1,
			2, 1, 6, 4, 8, 9, 9, 5, 4, 8, 10, 11, 11, 9, 8,
			6, 12, 7, 12, 6, 13, 12, 13, 14, 13, 15, 14,
		};


	}
}
