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
using VisualPinball.Engine.Common;
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

		public HitObject[] GenerateHitObjects(Table.Table table, IItem item)
		{
			if (_data.Shape == TriggerShape.TriggerStar || _data.Shape == TriggerShape.TriggerButton) {
				return GenerateRoundHitObjects(table, item);
			}
			return GenerateCurvedHitObjects(table, item);
		}

		private HitObject[] GenerateRoundHitObjects(Table.Table table, IItem item)
		{
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			return new HitObject[] {
				new TriggerHitCircle(_data.Center, _data.Radius, height, height + _data.HitHeight, item) {
					IsEnabled = _data.IsEnabled,
				}
			};
		}

		private HitObject[] GenerateCurvedHitObjects(Table.Table table, IItem item)
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
				hitObjects.Add(GetLineSeg(pv2, pv3, height, item));
			}

			hitObjects.AddRange( new Hit3DPoly(rgv3D, ItemType.Trigger, item).ConvertToTriangles());

			return hitObjects.ToArray();
		}

		private TriggerHitLineSeg GetLineSeg(Vertex2D pv1, Vertex2D pv2, float height, IItem item) {
			return new TriggerHitLineSeg(
				pv1.Clone(),
				pv2.Clone(),
				height,
				height + MathF.Max(_data.HitHeight - 8.0f, 0f), // adjust for same hit height as circular
				item
			);
		}
	}
}
