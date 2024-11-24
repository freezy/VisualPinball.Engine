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

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity
{
	public class RampColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		private readonly IRampData _data;
		private readonly RampMeshGenerator _meshGenerator;
		private readonly RampColliderComponent _colliderComponent;
		private readonly float4x4 _matrix;

		public RampColliderGenerator(RampApi rampApi, IRampData data, RampColliderComponent colliderComponent, float4x4 matrix)
		{
			_api = rampApi;
			_data = data;
			_colliderComponent = colliderComponent;
			_meshGenerator = new RampMeshGenerator(data);
			_matrix = matrix;
		}

		internal void GenerateColliders(float tableHeight, ref ColliderReference colliders, float margin = 0f)
		{
			var rv = _meshGenerator.GetRampVertex(tableHeight, PhysicsConstants.HitShapeDetailLevel, true);
			var rgvLocal = rv.RgvLocal;
			var rgHeight1 = rv.PointHeights;
			var vertexCount = rv.VertexCount;

			var h = GetWallHeights();
			var wallHeightRight = h.x;
			var wallHeightLeft = h.y;

			float2 pv1, pv2, pv3 = new float2(), pv4 = new float2();

			// Add line segments for right ramp wall.
			if (wallHeightRight > 0.0f) {
				for (var i = 0; i < vertexCount - 1; i++) {
					pv2 = rgvLocal[i].ToUnityFloat2();
					pv3 = rgvLocal[i + 1].ToUnityFloat2();

					GenerateWallLineSeg(pv2, pv3, i > 0,rgHeight1[i], rgHeight1[i + 1], wallHeightRight, ref colliders);
					GenerateWallLineSeg(pv3, pv2, i < vertexCount - 2, rgHeight1[i], rgHeight1[i + 1], wallHeightRight, ref colliders);

					// add joints at start and end of right wall
					if (i == 0) {
						colliders.AddLineZ(pv2, rgHeight1[0], rgHeight1[0] + wallHeightRight, _api.GetColliderInfo(), _matrix);
					}

					if (i == vertexCount - 2) {
						colliders.AddLineZ(pv3, rgHeight1[vertexCount - 1], rgHeight1[vertexCount - 1] + wallHeightRight, _api.GetColliderInfo(), _matrix);
					}
				}
			}

			// Add line segments for left ramp wall.
			if (wallHeightLeft > 0.0f) {
				for (var i = 0; i < vertexCount - 1; i++) {
					pv2 = rgvLocal[vertexCount + i].ToUnityFloat2();
					pv3 = rgvLocal[vertexCount + i + 1].ToUnityFloat2();

					GenerateWallLineSeg(pv2, pv3, i > 0, rgHeight1[vertexCount - i - 2], rgHeight1[vertexCount - i - 1], wallHeightLeft, ref colliders);
					GenerateWallLineSeg(pv3, pv2, i < vertexCount - 2, rgHeight1[vertexCount - i - 2], rgHeight1[vertexCount - i - 1], wallHeightLeft, ref colliders);

					// add joints at start and end of left wall
					if (i == 0) {
						colliders.AddLineZ(pv2, rgHeight1[vertexCount - 1], rgHeight1[vertexCount - 1] + wallHeightLeft, _api.GetColliderInfo(), _matrix);
					}

					if (i == vertexCount - 2) {
						colliders.AddLineZ(pv3, rgHeight1[0], rgHeight1[0] + wallHeightLeft, _api.GetColliderInfo(), _matrix);
					}
				}
			}

			// Add hit triangles for the ramp floor.
			TriangleCollider ph3dPolyOld = default;
			var isOldSet = false;

			for (var i = 0; i < vertexCount - 1; i++) {
				/*
				* Layout of one ramp quad seen from above, ramp direction is bottom to top:
				*
				*    3 - - 4
				*    | \   |
				*    |   \ |
				*    2 - - 1
				*/
				pv1 = rgvLocal[i].ToUnityFloat2(); // i-th right
				pv2 = rgvLocal[vertexCount * 2 - i - 1].ToUnityFloat2(); // i-th left
				pv3 = rgvLocal[vertexCount * 2 - i - 2].ToUnityFloat2(); // (i+1)-th left
				pv4 = rgvLocal[i + 1].ToUnityFloat2(); // (i+1)-th right

				// left ramp floor triangle, CCW order
				var rg0 = new float3(pv2.x, pv2.y, rgHeight1[i] + margin);
				var rg1 = new float3(pv1.x, pv1.y, rgHeight1[i] + margin);
				var rg2 = new float3(pv3.x, pv3.y, rgHeight1[i + 1] + margin);

				// add joint for starting edge of ramp
				if (i == 0) {
					colliders.Add(new Line3DCollider(rg0, rg1, _api.GetColliderInfo()), _matrix);
				}

				// add joint for left edge
				colliders.Add(new Line3DCollider(rg0, rg2, _api.GetColliderInfo()), _matrix);

				// degenerate triangles happen if width is 0 at some point
				if (!TriangleCollider.IsDegenerate(rg0, rg1, rg2)) {
					var ph3dPoly = new TriangleCollider(rg0, rg1, rg2, _api.GetColliderInfo());
					colliders.Add(ph3dPoly, _matrix);

					CheckJoint(isOldSet, in ph3dPolyOld, in ph3dPoly, ref colliders);
					ph3dPolyOld = ph3dPoly;
					isOldSet = true;
				}

				// right ramp floor triangle, CCW order
				rg0 = new float3(pv3.x, pv3.y, rgHeight1[i + 1] + margin);
				rg1 = new float3(pv1.x, pv1.y, rgHeight1[i] + margin);
				rg2 = new float3(pv4.x, pv4.y, rgHeight1[i + 1] + margin);

				// add joint for right edge
				colliders.Add(new Line3DCollider(rg1, rg2, _api.GetColliderInfo()), _matrix);

				if (!TriangleCollider.IsDegenerate(rg0, rg1, rg2)) {
					var ph3dPoly = new TriangleCollider(rg0, rg1, rg2, _api.GetColliderInfo());
					colliders.Add(ph3dPoly, _matrix);

					CheckJoint(isOldSet, in ph3dPolyOld, in ph3dPoly, ref colliders);
					ph3dPolyOld = ph3dPoly;
					isOldSet = true;
				}
			}

			if (vertexCount >= 2) {
				// add joint for final edge of ramp
				var v1 = new float3(pv4.x, pv4.y, rgHeight1[vertexCount - 1]);
				var v2 = new float3(pv3.x, pv3.y, rgHeight1[vertexCount - 1]);
				colliders.Add(new Line3DCollider(v1, v2, _api.GetColliderInfo()), _matrix);
			}

			// add outside bottom,
			// joints at the intersections are not needed since the inner surface has them
			// this surface is identical... except for the direction of the normal face.
			// hence the joints protect both surface edges from having a fall through
			for (var i = 0; i < vertexCount - 1; i++) {
				// see sketch above
				pv1 = rgvLocal[i].ToUnityFloat2();
				pv2 = rgvLocal[vertexCount * 2 - i - 1].ToUnityFloat2();
				pv3 = rgvLocal[vertexCount * 2 - i - 2].ToUnityFloat2();
				pv4 = rgvLocal[i + 1].ToUnityFloat2();

				// left ramp triangle, order CW
				var rg0 = new float3(pv1.x, pv1.y, rgHeight1[i]);
				var rg1 = new float3(pv2.x, pv2.y, rgHeight1[i]);
				var rg2 = new float3(pv3.x, pv3.y, rgHeight1[i + 1]);

				if (!TriangleCollider.IsDegenerate(rg0, rg1, rg2)) {
					colliders.Add(new TriangleCollider(rg0, rg1, rg2, _api.GetColliderInfo()), _matrix);
				}

				// right ramp triangle, order CW
				rg0 = new float3(pv3.x, pv3.y, rgHeight1[i + 1]);
				rg1 = new float3(pv4.x, pv4.y, rgHeight1[i + 1]);
				rg2 = new float3(pv1.x, pv1.y, rgHeight1[i]);

				if (!TriangleCollider.IsDegenerate(rg0, rg1, rg2)) {
					colliders.Add(new TriangleCollider(rg0, rg1, rg2, _api.GetColliderInfo()), _matrix);
				}
			}
		}

		private float2 GetWallHeights()
		{
			switch (_data.Type) {
				case RampType.RampTypeFlat: return new float2(_colliderComponent.RightWallHeight, _colliderComponent.LeftWallHeight);
				case RampType.RampType1Wire: return new float2(31.0f, 31.0f);
				case RampType.RampType2Wire: return new float2(31.0f, 31.0f);
				case RampType.RampType4Wire: return new float2(62.0f, 62.0f);
				case RampType.RampType3WireRight: return new float2(62.0f, (float)(6 + 12.5));
				case RampType.RampType3WireLeft: return new float2((float)(6 + 12.5), 62.0f);
				default:
					throw new InvalidOperationException($"Unknown ramp type {_data.Type}");
			}
		}

		private void GenerateWallLineSeg(float2 pv1, float2 pv2, bool pv3Exists, float height1, float height2, float wallHeight, ref ColliderReference colliders)
		{
			//!! Hit-walls are still done via 2D line segments with only a single lower and upper border, so the wall will always reach below and above the actual ramp -between- two points of the ramp
			// Thus, subdivide until at some point the approximation error is 'subtle' enough so that one will usually not notice (i.e. dependent on ball size)
			if (height2 - height1 > 2.0 * PhysicsConstants.PhysSkin) { //!! use ballsize
				GenerateWallLineSeg(pv1, (pv1 + pv2) * 0.5f, pv3Exists, height1, (height1 + height2) * 0.5f, wallHeight, ref colliders);
				GenerateWallLineSeg((pv1 + pv2) * 0.5f, pv2, true, (height1 + height2) * 0.5f, height2, wallHeight, ref colliders);

			} else {
				colliders.AddLine(pv1, pv2, height1, height2 + wallHeight, _api.GetColliderInfo(), _matrix);

				if (pv3Exists) {
					colliders.AddLineZ(pv1, height1, height2 + wallHeight, _api.GetColliderInfo(), _matrix);
				}
			}
		}

		private void CheckJoint(bool isOldSet, in TriangleCollider ph3d1, in TriangleCollider ph3d2, ref ColliderReference colliders)
		{
			if (isOldSet) {   // may be null in case of degenerate triangles
				var jointNormal = math.cross(ph3d1.Normal(), ph3d2.Normal());
				if (math.lengthsq(jointNormal) < 1e-8) { // coplanar triangles need no joints
					return;
				}
			}
			// By convention of the calling function, points 1 [0] and 2 [1] of the second polygon will
			// be the common-edge points
			colliders.Add(new Line3DCollider(ph3d2.Rgv0, ph3d2.Rgv1, _api.GetColliderInfo()), _matrix);
		}
	}
}
