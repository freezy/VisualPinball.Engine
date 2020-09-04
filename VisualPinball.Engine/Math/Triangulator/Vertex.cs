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
	internal readonly struct Vertex
	{
		public readonly Vector2 Position;
		public readonly int Index;

		public Vertex(Vector2 position, int index)
		{
			Position = position;
			Index = index;
		}

		public override bool Equals(object obj)
		{
			return obj.GetType() == typeof(Vertex) && Equals((Vertex)obj);
		}

		public bool Equals(Vertex obj)
		{
			return obj.Position.Equals(Position) && obj.Index == Index;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (Position.GetHashCode() * 397) ^ Index;
			}
		}

		public override string ToString()
		{
			return $"{Position} ({Index})";
		}
	}
}
