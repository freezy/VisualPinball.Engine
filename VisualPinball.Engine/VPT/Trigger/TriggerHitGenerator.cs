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
			if (_data.Shape == TriggerShape.TriggerStar || _data.Shape == TriggerShape.TriggerButton) {
				return GenerateRoundHitObjects(table, events);
			}
			return GenerateCurvedHitObjects(table, events);
		}

		private HitObject[] GenerateRoundHitObjects(Table.Table table, EventProxy events)
		{
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			return new HitObject[] {
				new TriggerHitCircle(_data.Center, _data.Radius, height, height + _data.HitHeight) {
					Obj = events,
					IsEnabled = _data.IsEnabled,
				}
			};
		}

		private HitObject[] GenerateCurvedHitObjects(Table.Table table, EventProxy events)
		{
			var hitObjects = new List<HitObject>();
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			var vVertex = DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_data.DragPoints);

			var count = vVertex.Length;
			var rgv = new RenderVertex2D[count];
			var rgv3D = new Vertex3D[count];

			for (var i = 0; i < count; i++) {
				rgv[i] = vVertex[i];
				rgv3D[i] = new Vertex3D(rgv[i].X, rgv[i].Y, height + (float)(PhysicsConstants.PhysSkin * 2.0));
			}

			for (var i = 0; i < count; i++) {
				var pv2 = rgv[i < count - 1 ? i + 1 : 0];
				var pv3 = rgv[i < count - 2 ? i + 2 : i + 2 - count];
				hitObjects.Add(GetLineSeg(pv2, pv3, events, height));
			}

			hitObjects.Add( new Hit3DPoly(rgv3D, ItemType.Trigger) {
				Obj = events
			});

			return hitObjects.ToArray();
		}

		private TriggerHitLineSeg GetLineSeg(Vertex2D pv1, Vertex2D pv2, EventProxy events, float height) {
			return new TriggerHitLineSeg(
				pv1.Clone(),
				pv2.Clone(),
				height,
				height + MathF.Max(_data.HitHeight - 8.0f, 0f) // adjust for same hit height as circular
			) {
				Obj = events
			};
		}
	}
}
