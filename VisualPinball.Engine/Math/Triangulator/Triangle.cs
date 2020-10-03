// Triangulator
//
// The MIT License (MIT)
//
// Copyright (c) 2017, Nick Gravelyn
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

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
