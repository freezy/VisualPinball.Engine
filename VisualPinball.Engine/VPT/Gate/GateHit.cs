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

using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Gate
{
	public class GateHit : HitObject
	{
		public LineSeg LineSeg0;
		public LineSeg LineSeg1;
		public bool TwoWay;

		public GateHit(GateData data, float height, IItem item) : base(ItemType.Gate, item)
		{
			var data1 = data;
			var height1 = height;

			var halfLength = data1.Length * 0.5f;
			var radAngle = MathF.DegToRad(data1.Rotation);
			var sn = MathF.Sin(radAngle);
			var cs = MathF.Cos(radAngle);

			LineSeg0 = new LineSeg(
				new Vertex2D(
					data1.Center.X - cs * (halfLength + PhysicsConstants.PhysSkin),
					data1.Center.Y - sn * (halfLength + PhysicsConstants.PhysSkin)
				),
				new Vertex2D(
					data1.Center.X + cs * (halfLength + PhysicsConstants.PhysSkin),
					data1.Center.Y + sn * (halfLength + PhysicsConstants.PhysSkin)
				),
				height1,
				height1 + 2.0f * PhysicsConstants.PhysSkin,
				ItemType.Gate,
				item
			);

			LineSeg1 = new LineSeg(
				new Vertex2D(LineSeg0.V2.X, LineSeg0.V2.Y),
				new Vertex2D(LineSeg0.V1.X, LineSeg0.V1.Y),
				height,
				height + 2.0f * PhysicsConstants.PhysSkin,
				ItemType.Gate,
				item
			);

			TwoWay = false;
		}

		public override void SetIndex(int index, int version)
		{
			base.SetIndex(index, version);
			LineSeg0.SetIndex(index, version);
			LineSeg1.SetIndex(index, version);
		}

		public override void CalcHitBBox()
		{
			LineSeg0.CalcHitBBox();
			HitBBox = LineSeg0.HitBBox;
		}
	}
}
