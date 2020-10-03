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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Engine.Physics
{
	public class LineSegSlingshot : LineSeg
	{
		public float Force = 0;
		public bool DoHitEvent = false;

		private readonly SurfaceData _surfaceData;
		private float _eventTimeReset = 0;

		public LineSegSlingshot(SurfaceData surfaceData, Vertex2D p1, Vertex2D p2, float zLow, float zHigh, ItemType itemType, IItem item)
			: base(p1, p2, zLow, zHigh, itemType, item)
		{
			_surfaceData = surfaceData;
		}
	}
}
