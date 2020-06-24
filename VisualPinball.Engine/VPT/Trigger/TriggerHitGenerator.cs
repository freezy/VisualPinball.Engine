using System.Collections.Generic;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Trigger
{
	public class TriggerHitGenerator
	{
		private readonly TriggerData _data;

		public TriggerHitGenerator(TriggerData data)
		{
			_data = data;
		}

		public HitObject[] GenerateHitObjects(Table.Table table, EventProxy events)
		{
			var hitObjects = new List<HitObject>();
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			var vVertex = DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_data.DragPoints);

			var count = vVertex.Length;
			var rgv = new RenderVertex2D[count];
			var rgv3D = new Vertex3D[count];

			for (var i = 0; i < count; i++) {
				rgv[i] = vVertex[i];
				rgv3D[i] = new Vertex3D(rgv[i].X, rgv[i].Y, height + PhysicsConstants.PhysSkin * 2.0f);
			}

			for (var i = 0; i < count; i++) {
				var pv2 = rgv[i < count - 1 ? i + 1 : 0];
				var pv3 = rgv[i < count - 2 ? i + 2 : i + 2 - count];
				hitObjects.Add(GetLineSeg(pv2, pv3, events, height));
			}

			hitObjects.Add( new Hit3DPoly(rgv3D, CollisionType.Trigger) {
				Obj = events
			});

			return hitObjects.ToArray();
		}

		private TriggerHitLineSeg GetLineSeg(Vertex2D pv1, Vertex2D pv2, EventProxy events, float height) {
			return new TriggerHitLineSeg(
				new Vertex2D(pv1.X, pv1.Y),
				new Vertex2D(pv2.X, pv2.Y),
				height,
				height + MathF.Max(_data.HitHeight - 8.0f, 0) //adjust for same hit height as circular
			) {
				Obj = events
			};
		}
	}
}
