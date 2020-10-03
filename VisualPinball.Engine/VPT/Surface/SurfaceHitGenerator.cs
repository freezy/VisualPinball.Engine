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
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Surface
{
	public class SurfaceHitGenerator
	{
		public List<LineSegSlingshot> LineSling = new List<LineSegSlingshot>();

		private readonly SurfaceData _data;

		public SurfaceHitGenerator(SurfaceData data)
		{
			_data = data;
		}

		public HitObject[] GenerateHitObjects(Table.Table table, IItem item)
		{
			return UpdateCommonParameters(Generate3DPolys(table, item), table);
		}
		/// <summary>
		/// Returns all hit objects for the surface.
		/// </summary>
		private HitObject[] Generate3DPolys(Table.Table table, IItem item)
		{
			var hitObjects = new List<HitObject>();

			var vVertex =  DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_data.DragPoints);

			var count = vVertex.Length;
			var rgv3Dt = new Vertex3D[count];
			var rgv3Db = _data.IsBottomSolid ? new Vertex3D[count] : null;

			var bottom = _data.HeightBottom + table.TableHeight;
			var top = _data.HeightTop + table.TableHeight;

			for (var i = 0; i < count; ++i) {
				var pv1 = vVertex[i];
				rgv3Dt[i] = new Vertex3D(pv1.X, pv1.Y, top);

				if (rgv3Db != null) {
					rgv3Db[count - 1 - i] = new Vertex3D(pv1.X, pv1.Y, bottom);
				}

				var pv2 = vVertex[(i + 1) % count];
				var pv3 = vVertex[(i + 2) % count];
				hitObjects.AddRange(GenerateLinePolys(pv2, pv3, table, item));
			}

			hitObjects.AddRange(new Hit3DPoly(rgv3Dt, ItemType.Surface, item).ConvertToTriangles());

			if (rgv3Db != null) {
				hitObjects.AddRange(new Hit3DPoly(rgv3Db, ItemType.Surface, item).ConvertToTriangles());
			}

			return hitObjects.ToArray();
		}

		/// <summary>
		/// Returns the hit line polygons for the surface.
		/// </summary>
		private IEnumerable<HitObject> GenerateLinePolys(RenderVertex2D pv1, Vertex2D pv2, Table.Table table, IItem item)
		{
			var linePolys = new List<HitObject>();
			var bottom = _data.HeightBottom + table.TableHeight;
			var top = _data.HeightTop + table.TableHeight;

			if (!pv1.IsSlingshot) {
				linePolys.Add(new LineSeg(pv1, pv2, bottom, top, ItemType.Surface, item));

			} else {
				var slingLine = new LineSegSlingshot(_data, pv1, pv2, bottom, top, ItemType.Surface, item) {
					Force = _data.SlingshotForce,
					FireEvents = true, Threshold = _data.Threshold
				};

				// slingshots always have hit events

				linePolys.Add(slingLine);
				LineSling.Add(slingLine);
			}

			if (_data.HeightBottom != 0) {
				// add lower edge as a line
				linePolys.Add(new HitLine3D(new Vertex3D(pv1.X, pv1.Y, bottom), new Vertex3D(pv2.X, pv2.Y, bottom), ItemType.Surface, item));
			}

			// add upper edge as a line
			linePolys.Add(new HitLine3D(new Vertex3D(pv1.X, pv1.Y, top), new Vertex3D(pv2.X, pv2.Y, top), ItemType.Surface, item));

			// create vertical joint between the two line segments
			linePolys.Add(new HitLineZ(pv1, bottom, top, ItemType.Surface, item));

			// add upper and lower end points of line
			if (_data.HeightBottom != 0) {
				linePolys.Add(new HitPoint(new Vertex3D(pv1.X, pv1.Y, bottom), ItemType.Surface, item));
			}

			linePolys.Add(new HitPoint(new Vertex3D(pv1.X, pv1.Y, top), ItemType.Surface, item));

			return linePolys.ToArray();
		}

		/// <summary>
		/// Updates the hit object with parameters common to the surface.
		/// </summary>
		/// <param name="hitObjects"></param>
		/// <param name="events"></param>
		/// <param name="table"></param>
		/// <returns></returns>
		private HitObject[] UpdateCommonParameters(HitObject[] hitObjects, Table.Table table) {
			foreach (var obj in hitObjects) {

				obj.ApplyPhysics(_data, table);

				if (_data.HitEvent) {
					obj.FireEvents = true;
					obj.Threshold = _data.Threshold;
				}
			}
			return hitObjects;
		}
	}
}
