// ReSharper disable CompareOfFloatsByEqualityOperator

using System.Collections.Generic;
using VisualPinball.Engine.Game;
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

		public HitObject[] GenerateHitObjects(EventProxy events, Table.Table table)
		{
			return UpdateCommonParameters(Generate3DPolys(events, table), events, table);
		}
		/// <summary>
		/// Returns all hit objects for the surface.
		/// </summary>
		private HitObject[] Generate3DPolys(EventProxy events, Table.Table table)
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
				hitObjects.AddRange(GenerateLinePolys(pv2, pv3, events, table));
			}

			hitObjects.Add(new Hit3DPoly(rgv3Dt));

			if (rgv3Db != null) {
				hitObjects.Add(new Hit3DPoly(rgv3Db));
			}

			return hitObjects.ToArray();
		}

		/// <summary>
		/// Returns the hit line polygons for the surface.
		/// </summary>
		private IEnumerable<HitObject> GenerateLinePolys(RenderVertex2D pv1, Vertex2D pv2, EventProxy events, Table.Table table)
		{
			var linePolys = new List<HitObject>();
			var bottom = _data.HeightBottom + table.TableHeight;
			var top = _data.HeightTop + table.TableHeight;

			if (!pv1.IsSlingshot) {
				linePolys.Add(new LineSeg(pv1, pv2, bottom, top));

			} else {
				var slingLine = new LineSegSlingshot(_data, pv1, pv2, bottom, top) {
					Force = _data.SlingshotForce,
					Obj = events,
					FireEvents = true, Threshold = _data.Threshold
				};

				// slingshots always have hit events

				linePolys.Add(slingLine);
				LineSling.Add(slingLine);
			}

			if (_data.HeightBottom != 0) {
				// add lower edge as a line
				linePolys.Add(new HitLine3D(new Vertex3D(pv1.X, pv1.Y, bottom), new Vertex3D(pv2.X, pv2.Y, bottom)));
			}

			// add upper edge as a line
			linePolys.Add(new HitLine3D(new Vertex3D(pv1.X, pv1.Y, top), new Vertex3D(pv2.X, pv2.Y, top)));

			// create vertical joint between the two line segments
			linePolys.Add(new HitLineZ(pv1, bottom, top));

			// add upper and lower end points of line
			if (_data.HeightBottom != 0) {
				linePolys.Add(new HitPoint(new Vertex3D(pv1.X, pv1.Y, bottom)));
			}

			linePolys.Add(new HitPoint(new Vertex3D(pv1.X, pv1.Y, top)));

			return linePolys.ToArray();
		}

		/// <summary>
		/// Updates the hit object with parameters common to the surface.
		/// </summary>
		/// <param name="hitObjects"></param>
		/// <param name="events"></param>
		/// <param name="table"></param>
		/// <returns></returns>
		private HitObject[] UpdateCommonParameters(HitObject[] hitObjects, EventProxy events, Table.Table table) {
			foreach (var obj in hitObjects) {

				obj.ApplyPhysics(_data, table);

				if (_data.HitEvent) {
					obj.Obj = events;
					obj.FireEvents = true;
					obj.Threshold = _data.Threshold;
				}
			}
			return hitObjects;
		}
	}
}
