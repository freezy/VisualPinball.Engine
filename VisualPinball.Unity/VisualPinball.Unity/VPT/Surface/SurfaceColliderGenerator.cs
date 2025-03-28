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

// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	public class SurfaceColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		private readonly SurfaceComponent _component;
		private readonly SurfaceColliderComponent _colliderComponent;
		private readonly float4x4 _matrix;
		private readonly SurfaceMeshGenerator _meshGen;

		public SurfaceColliderGenerator(SurfaceApi surfaceApi, SurfaceComponent component, SurfaceColliderComponent colliderComponent, float4x4 matrix)
		{
			_api = surfaceApi;
			_matrix = matrix;
			_component = component;
			_colliderComponent = colliderComponent;

			var data = new SurfaceData();
			component.CopyDataTo(data, null, null, false);
			var pos = new Vertex3D(matrix.c3.x, matrix.c3.y, matrix.c3.z);
			_meshGen = new SurfaceMeshGenerator(data, pos);
		}

		internal void GenerateColliders(ref ColliderReference colliders)
		{
			var topMesh = _meshGen.GetMesh(SurfaceMeshGenerator.Top, 0, 0, 0, false);
			//var sideMesh = _meshGen.GetMesh(SurfaceMeshGenerator.Side, 0, 0, 0, false);

			ColliderUtils.GenerateCollidersFromMesh(topMesh, _api.GetColliderInfo(), ref colliders, _matrix);
			GenerateSideColliders(ref colliders);
			// ColliderUtils.GenerateCollidersFromMesh(sideMesh, _api.GetColliderInfo(), ref colliders, _matrix);
		}

		private void GenerateSideColliders(ref ColliderReference colliders, float margin = 0f)
		{
			var vVertex =  DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_component.DragPoints);

			var count = vVertex.Length;
			var rgv3Dt = new float3[count];
			var rgv3Db = _colliderComponent.IsBottomSolid ? new float3[count] : null;

			var bottom = _component.HeightBottom - margin;
			var top = _component.HeightTop + margin;

			for (var i = 0; i < count; ++i) {
				var pv1 = vVertex[i];
				rgv3Dt[i] = new float3(pv1.X, pv1.Y, top);

				if (rgv3Db != null) {
					rgv3Db[count - 1 - i] = new float3(pv1.X, pv1.Y, bottom);
				}

				var pv2 = vVertex[(i + 1) % count];
				var pv3 = vVertex[(i + 2) % count];
				GenerateLinePolys(pv2, pv3, ref colliders);
			}
		}

		/// <summary>
		/// Returns the hit line polygons for the surface.
		/// </summary>
		private void GenerateLinePolys(RenderVertex2D pv1, Vertex2D pv2, ref ColliderReference colliders)
		{
			var bottom = _component.HeightBottom;
			var top = _component.HeightTop;

			if (!pv1.IsSlingshot) {
				colliders.Add(new LineCollider(pv1.ToUnityFloat2(), pv2.ToUnityFloat2(), bottom, top, _api.GetColliderInfo()), _matrix);

			} else {
				colliders.Add(new LineSlingshotCollider(_colliderComponent.SlingshotForce, pv1.ToUnityFloat2(), pv2.ToUnityFloat2(), bottom, top, _api.GetColliderInfo()), _matrix);
			}

			if (_component.HeightBottom != 0) {
				// add lower edge as a line
				colliders.Add(new Line3DCollider(new float3(pv1.X, pv1.Y, bottom), new float3(pv2.X, pv2.Y, bottom), _api.GetColliderInfo()), _matrix);
			}

			// add upper edge as a line
			colliders.Add(new Line3DCollider(new float3(pv1.X, pv1.Y, top), new float3(pv2.X, pv2.Y, top), _api.GetColliderInfo()), _matrix);

			// create vertical joint between the two line segments
			colliders.Add(new LineZCollider(pv1.ToUnityFloat2(), bottom, top, _api.GetColliderInfo()), _matrix);

			// add upper and lower end points of line
			if (_component.HeightBottom != 0) {
				colliders.Add(new PointCollider(new float3(pv1.X, pv1.Y, bottom), _api.GetColliderInfo()), _matrix);
			}

			colliders.Add(new PointCollider(new float3(pv1.X, pv1.Y, top), _api.GetColliderInfo()), _matrix);
		}
	}
}
