// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using System;
using System.IO;

namespace VisualPinball.Engine.Math
{
	[Serializable]
	public struct Vertex2D
	{
		public float X;
		public float Y;

		public Vertex2D(float x, float y)
		{
			X = x;
			Y = y;
		}

		public Vertex2D(BinaryReader reader, int len)
		{
			X = reader.ReadSingle();
			Y = reader.ReadSingle();
			if (len > 8) {
				reader.BaseStream.Seek(len - 8, SeekOrigin.Current);
			}
		}

		public Vertex2D Set(float x, float y)
		{
			X = x;
			Y = y;
			return this;
		}

		public Vertex2D SetZero()
		{
			return Set(0, 0);
		}

		public static Vertex2D operator +(Vertex2D a, Vertex2D b) => new Vertex2D(a.X + b.X, a.Y + b.Y);
		public static Vertex2D operator -(Vertex2D a, Vertex2D b) => new Vertex2D(a.X - b.X, a.Y - b.Y);
		public static Vertex2D operator *(Vertex2D a, float b) => new Vertex2D(a.X * b, a.Y * b);
		public static Vertex2D operator *(float a, Vertex2D b) => new Vertex2D(b.X * a, b.Y * a);

		public Vertex2D Clone()
		{
			return new Vertex2D(X, Y);
		}

		// public Vertex2D Add(Vertex2D v)
		// {
		// 	X += v.X;
		// 	Y += v.Y;
		// 	return this;
		// }

		// public Vertex2D Sub(Vertex2D v)
		// {
		// 	X -= v.X;
		// 	Y -= v.Y;
		// 	return this;
		// }

		public void Normalize()
		{
			var oneOverLength = 1.0f / Length();
			X *= oneOverLength;
			Y *= oneOverLength;
		}

		// public Vertex2D MultiplyScalar(float scalar)
		// {
		// 	X *= scalar;
		// 	Y *= scalar;
		// 	return this;
		// }

		public float Length()
		{
			return MathF.Sqrt(X * X + Y * Y);
		}

		public float LengthSq()
		{
			return X * X + Y * Y;
		}

		public float Dot(Vertex2D pv)
		{
			return X * pv.X + Y * pv.Y;
		}

		public bool Equals(Vertex2D v)
		{
			return X == v.X && Y == v.Y;
		}

		public override string ToString()
		{
			return $"Vertex2D({X}/{Y})";
		}
	}
}
