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

using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Physics
{
	public class HitLineZ : HitObject
	{
		public readonly Vertex2D Xy;

		protected HitLineZ(Vertex2D xy, ItemType itemType, IItem item) : base(itemType, item)
		{
			Xy = xy;
		}

		public HitLineZ(Vertex2D xy, float zLow, float zHigh, ItemType itemType, IItem item) : this(xy, itemType, item)
		{
			HitBBox.ZLow = zLow;
			HitBBox.ZHigh = zHigh;
		}

		public HitLineZ Set(float x, float y)
		{
			Xy.X = x;
			Xy.Y = y;
			return this;
		}

		public override void CalcHitBBox()
		{
			HitBBox.Left = Xy.X;
			HitBBox.Right = Xy.X;
			HitBBox.Top = Xy.Y;
			HitBBox.Bottom = Xy.Y;

			// zlow and zhigh set in ctor
		}
	}
}
