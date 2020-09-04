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
	/// <summary>
	/// A basic triangle structure that holds the three vertices that make up a given triangle.
	/// </summary>
	internal struct Triangle
	{
		public readonly Vertex A;
		public readonly Vertex B;
		public readonly Vertex C;

		public Triangle(Vertex a, Vertex b, Vertex c)
		{
			A = a;
			B = b;
			C = c;
		}

		public bool ContainsPoint(Vertex point)
		{
			//return true if the point to test is one of the vertices
			if (point.Equals(A) || point.Equals(B) || point.Equals(C))
				return true;

			bool oddNodes = false;

			if (checkPointToSegment(C, A, point))
				oddNodes = !oddNodes;
			if (checkPointToSegment(A, B, point))
				oddNodes = !oddNodes;
			if (checkPointToSegment(B, C, point))
				oddNodes = !oddNodes;

			return oddNodes;
		}

		public static bool ContainsPoint(Vertex a, Vertex b, Vertex c, Vertex point)
		{
			return new Triangle(a, b, c).ContainsPoint(point);
		}

		static bool checkPointToSegment(Vertex sA, Vertex sB, Vertex point)
		{
			if (sA.Position.Y < point.Position.Y && sB.Position.Y >= point.Position.Y ||
			    sB.Position.Y < point.Position.Y && sA.Position.Y >= point.Position.Y)
			{
				float x =
					sA.Position.X +
					(point.Position.Y - sA.Position.Y) /
					(sB.Position.Y - sA.Position.Y) *
					(sB.Position.X - sA.Position.X);

				if (x < point.Position.X)
					return true;
			}

			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() != typeof (Triangle))
				return false;
			return Equals((Triangle) obj);
		}

		public bool Equals(Triangle obj)
		{
			return obj.A.Equals(A) && obj.B.Equals(B) && obj.C.Equals(C);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int result = A.GetHashCode();
				result = (result * 397) ^ B.GetHashCode();
				result = (result * 397) ^ C.GetHashCode();
				return result;
			}
		}
	}
}
