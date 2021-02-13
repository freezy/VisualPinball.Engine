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

// ReSharper disable CompareOfFloatsByEqualityOperator

using System.Collections.Generic;
using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class SurfaceColliderGenerator
	{
		private readonly SurfaceApi _api;
		private readonly SurfaceData _data;

		public SurfaceColliderGenerator(SurfaceApi surfaceApi)
		{
			_api = surfaceApi;
			_data = surfaceApi.Data;
		}

		internal void GenerateColliders(Table table, List<ICollider> colliders)
		{
			var vVertex =  DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_data.DragPoints);

			var count = vVertex.Length;
			var rgv3Dt = new float3[count];
			var rgv3Db = _data.IsBottomSolid ? new float3[count] : null;

			var bottom = _data.HeightBottom + table.TableHeight;
			var top = _data.HeightTop + table.TableHeight;

			for (var i = 0; i < count; ++i) {
				var pv1 = vVertex[i];
				rgv3Dt[i] = new float3(pv1.X, pv1.Y, top);

				if (rgv3Db != null) {
					rgv3Db[count - 1 - i] = new float3(pv1.X, pv1.Y, bottom);
				}

				var pv2 = vVertex[(i + 1) % count];
				var pv3 = vVertex[(i + 2) % count];
				GenerateLinePolys(pv2, pv3, table, colliders);
			}

			ColliderUtils.Generate3DPolyColliders(in rgv3Dt, table, _api.GetColliderInfo(table), colliders);

			if (rgv3Db != null) {
				ColliderUtils.Generate3DPolyColliders(in rgv3Db, table, _api.GetColliderInfo(table), colliders);
			}
		}

		/// <summary>
		/// Returns the hit line polygons for the surface.
		/// </summary>
		private void GenerateLinePolys(RenderVertex2D pv1, Vertex2D pv2, Table table, ICollection<ICollider> colliders)
		{
			var bottom = _data.HeightBottom + table.TableHeight;
			var top = _data.HeightTop + table.TableHeight;

			if (!pv1.IsSlingshot) {
				colliders.Add(new LineCollider(pv1.ToUnityFloat2(), pv2.ToUnityFloat2(), bottom, top, _api.GetColliderInfo(table)));

			} else {
				colliders.Add(new LineSlingshotCollider(_data.SlingshotForce, pv1.ToUnityFloat2(), pv2.ToUnityFloat2(), bottom, top, _api.GetColliderInfo(table)));
			}

			if (_data.HeightBottom != 0) {
				// add lower edge as a line
				colliders.Add(new Line3DCollider(new float3(pv1.X, pv1.Y, bottom), new float3(pv2.X, pv2.Y, bottom), _api.GetColliderInfo(table)));
			}

			// add upper edge as a line
			colliders.Add(new Line3DCollider(new float3(pv1.X, pv1.Y, top), new float3(pv2.X, pv2.Y, top), _api.GetColliderInfo(table)));

			// create vertical joint between the two line segments
			colliders.Add(new LineZCollider(pv1.ToUnityFloat2(), bottom, top, _api.GetColliderInfo(table)));

			// add upper and lower end points of line
			if (_data.HeightBottom != 0) {
				colliders.Add(new PointCollider(new float3(pv1.X, pv1.Y, bottom), _api.GetColliderInfo(table)));
			}

			colliders.Add(new PointCollider(new float3(pv1.X, pv1.Y, top), _api.GetColliderInfo(table)));
		}
	}
}
