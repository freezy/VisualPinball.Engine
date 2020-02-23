using System;
using System.IO;

namespace VisualPinball.Engine.Math
{
	[Serializable]
	public class Vertex2D
	{
		public float X;
		public float Y;

		public float GetX() => X;
		public float GetY() => Y;

		public Vertex2D() : this(0.0f, 0.0f) { }

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

		public Vertex2D Clone()
		{
			return new Vertex2D(X, Y);
		}

		public Vertex2D Add(Vertex2D v)
		{
			X += v.X;
			Y += v.Y;
			return this;
		}

		public Vertex2D Sub(Vertex2D v)
		{
			X -= v.X;
			Y -= v.Y;
			return this;
		}

		public Vertex2D Normalize()
		{
			var len = Length();
			return DivideScalar(len == 0 ? 1 : len);
		}

		public Vertex2D DivideScalar(float scalar)
		{
			return MultiplyScalar(1 / scalar);
		}

		public Vertex2D MultiplyScalar(float scalar)
		{
			X *= scalar;
			Y *= scalar;
			return this;
		}

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
			if (v == null) {
				return false;
			}
			return X == v.X && Y == v.Y;
		}

		public override string ToString()
		{
			return $"Vertex2D({X}/{Y})";
		}
	}
}
