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

namespace VisualPinball.Engine.VPT.Spinner
{
	public class SpinnerHit : HitObject
	{
		public readonly LineSeg LineSeg0;
		public readonly LineSeg LineSeg1;

		public SpinnerHit(SpinnerData data, float height, IItem item) : base(ItemType.Spinner, item)
		{
			var halfLength = data.Length * 0.5f;

			var radAngle = MathF.DegToRad(data.Rotation);
			var sn = MathF.Sin(radAngle);
			var cs = MathF.Cos(radAngle);

			var v1 = new Vertex2D(
				data.Center.X - cs * (halfLength + PhysicsConstants.PhysSkin), // through the edge of the
				data.Center.Y - sn * (halfLength + PhysicsConstants.PhysSkin)  // spinner
			);
			var v2 = new Vertex2D(
				data.Center.X + cs * (halfLength + PhysicsConstants.PhysSkin), // oversize by the ball radius
				data.Center.Y + sn * (halfLength + PhysicsConstants.PhysSkin)  // this will prevent clipping
			);

			LineSeg0 = new LineSeg(v1, v2, height, height + 2.0f * PhysicsConstants.PhysSkin, ItemType.Spinner, item);
			LineSeg1 = new LineSeg(v2.Clone(), v1.Clone(), height, height + 2.0f * PhysicsConstants.PhysSkin, ItemType.Spinner, item);
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
