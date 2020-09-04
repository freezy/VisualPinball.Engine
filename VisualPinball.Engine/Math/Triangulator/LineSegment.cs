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

namespace VisualPinball.Engine.Math.Triangulator
{
	internal struct LineSegment
	{
		public Vertex A;
		public Vertex B;

		public LineSegment(Vertex a, Vertex b)
		{
			A = a;
			B = b;
		}

		public float? IntersectsWithRay(Vector2 origin, Vector2 direction)
		{
			float largestDistance = MathHelper.Max(A.Position.X - origin.X, B.Position.X - origin.X) * 2f;
			LineSegment raySegment = new LineSegment(new Vertex(origin, 0), new Vertex(origin + (direction * largestDistance), 0));

			Vector2? intersection = FindIntersection(this, raySegment);
			float? value = null;

			if (intersection != null)
				value = Vector2.Distance(origin, intersection.Value);

			return value;
		}

		public static Vector2? FindIntersection(LineSegment a, LineSegment b)
		{
			float x1 = a.A.Position.X;
			float y1 = a.A.Position.Y;
			float x2 = a.B.Position.X;
			float y2 = a.B.Position.Y;
			float x3 = b.A.Position.X;
			float y3 = b.A.Position.Y;
			float x4 = b.B.Position.X;
			float y4 = b.B.Position.Y;

			float denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);

			float uaNum = (x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3);
			float ubNum = (x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3);

			float ua = uaNum / denom;
			float ub = ubNum / denom;

			if (MathHelper.Clamp(ua, 0f, 1f) != ua || MathHelper.Clamp(ub, 0f, 1f) != ub)
				return null;

			return a.A.Position + (a.B.Position - a.A.Position) * ua;
		}
	}
}
