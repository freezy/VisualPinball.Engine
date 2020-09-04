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
	public class HitPoint : HitObject
	{
		public readonly Vertex3D P;

		public HitPoint(Vertex3D p, ItemType itemType, IItem item) : base(itemType, item)
		{
			P = p;
		}

		public override void CalcHitBBox()
		{
			HitBBox = new Rect3D(P.X, P.X, P.Y, P.Y, P.Z, P.Z);
		}
	}
}
