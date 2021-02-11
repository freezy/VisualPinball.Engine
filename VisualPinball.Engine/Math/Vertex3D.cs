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
using VisualPinball.Engine.Common;

namespace VisualPinball.Engine.Math
{
	[Serializable]
	public class Vertex3D
	{
		public static readonly Vertex3D One = new Vertex3D(1.0f, 1.0f, 1.0f);
		public static readonly Vertex3D Zero = new Vertex3D(0, 0, 0);

		public float X;
		public float Y;
		public float Z;

		public Vertex3D()
		{
			X = 0;
			Y = 0;
			Z = 0;
		}

		public Vertex3D(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public Vertex3D(Vertex3D v)
		{
			X = v.X;
			Y = v.Y;
			Z = v.Z;
		}

		public Vertex3D(BinaryReader reader, int len)
		{
			X = reader.ReadSingle();
			Y = reader.ReadSingle();
			if (len >= 12) {
				Z = reader.ReadSingle();
			}
			if (len > 12) {
				reader.BaseStream.Seek(len - 12, SeekOrigin.Current);
			}
		}

		public static Vertex3D operator +(Vertex3D a, Vertex3D b) => new Vertex3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		public static Vertex3D operator -(Vertex3D a, Vertex3D b) => new Vertex3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
		public static Vertex3D operator *(Vertex3D a, float b) => new Vertex3D(a.X * b, a.Y * b, a.Z * b);
		public static Vertex3D operator *(float a, Vertex3D b) => b * a;
		public static Vertex3D operator /(Vertex3D a, float b) => new Vertex3D(a.X / b, a.Y / b, a.Z / b);
		public static Vertex3D operator *(Matrix2D matrix, Vertex3D b) => new Vertex3D(
			matrix.Matrix[0][0] * b.X + matrix.Matrix[0][1] * b.Y + matrix.Matrix[0][2] * b.Z,
			matrix.Matrix[1][0] * b.X + matrix.Matrix[1][1] * b.Y + matrix.Matrix[1][2] * b.Z,
			matrix.Matrix[2][0] * b.X + matrix.Matrix[2][1] * b.Y + matrix.Matrix[2][2] * b.Z
		);

		public static void Reset(Vertex3D v)
		{
			v.Set(0, 0, 0);
		}

		public Vertex3D Set(Vertex3D v)
		{
			X = v.X;
			Y = v.Y;
			Z = v.Z;
			return this;
		}

		public Vertex3D Set(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
			return this;
		}

		// public new Vertex3D Clone()
		// {
		// 	return new Vertex3D(this);
		// }

		public new void Normalize()
		{
			var oneOverLength = 1.0f / Length();
			X *= oneOverLength;
			Y *= oneOverLength;
			Z *= oneOverLength;
		}

		public Vertex3D NormalizeSafe()
		{
			if (!IsZero()) {
				Normalize();
			}

			return this;
		}

		public new float Length()
		{
			return MathF.Sqrt(X * X + Y * Y + Z * Z);
		}

		public new float LengthSq()
		{
			return X * X + Y * Y + Z * Z;
		}

		// public new Vertex3D DivideScalar(float scalar)
		// {
		// 	return MultiplyScalar(1 / scalar);
		// }

		// public new Vertex3D MultiplyScalar(float scalar)
		// {
		// 	X *= scalar;
		// 	Y *= scalar;
		// 	Z *= scalar;
		// 	return this;
		// }

		// public Vertex3D ApplyMatrix2D(Matrix2D matrix)
		// {
		// 	var x = matrix.Matrix[0][0] * X + matrix.Matrix[0][1] * Y + matrix.Matrix[0][2] * Z;
		// 	var y = matrix.Matrix[1][0] * X + matrix.Matrix[1][1] * Y + matrix.Matrix[1][2] * Z;
		// 	var z = matrix.Matrix[2][0] * X + matrix.Matrix[2][1] * Y + matrix.Matrix[2][2] * Z;
		// 	X = x;
		// 	Y = y;
		// 	Z = z;
		// 	return this;
		// }

		public float Dot(Vertex3D v)
		{
			return X * v.X + Y * v.Y + Z * v.Z;
		}

		// public Vertex3D Sub(Vertex3D v)
		// {
		// 	X -= v.X;
		// 	Y -= v.Y;
		// 	Z -= v.Z;
		// 	return this;
		// }

		// public Vertex3D Add(Vertex3D v)
		// {
		// 	X += v.X;
		// 	Y += v.Y;
		// 	Z += v.Z;
		// 	return this;
		// }

		// public Vertex3D Cross(Vertex3D v)
		// {
		// 	return CrossVectors(this, v);
		// }

		// public static Vertex3D CrossVectors(Vertex3D a, Vertex3D b)
		// {
		// 	var ax = a.X;
		// 	var ay = a.Y;
		// 	var az = a.Z;
		// 	var bx = b.X;
		// 	var by = b.Y;
		// 	var bz = b.Z;
		//
		// 	return new Vertex3D(
		// 		ay * bz - az * by,
		// 		az * bx - ax * bz,
		// 		ax * by - ay * bx
		// 	);
		// }

		public Vertex2D xy()
		{
			return new Vertex2D(X, Y);
		}

		public new Vertex3D SetZero()
		{
			return Set(0f, 0f, 0f);
		}

		public bool IsZero()
		{
			return MathF.Abs(X) < Constants.FloatMin && MathF.Abs(Y) < Constants.FloatMin &&
			       MathF.Abs(Z) < Constants.FloatMin;
		}

		public bool Equals(Vertex3D v)
		{
			return v.X == X && v.Y == Y && v.Z == Z;
		}

		public static Vertex3D CrossProduct(Vertex3D pv1, Vertex3D pv2)
		{
			return new Vertex3D(
				pv1.Y * pv2.Z - pv1.Z * pv2.Y,
				pv1.Z * pv2.X - pv1.X * pv2.Z,
				pv1.X * pv2.Y - pv1.Y * pv2.X
			);
		}

		public static Vertex3D CrossZ(float rz, Vertex3D v)
		{
			return new Vertex3D(-rz * v.Y, rz * v.X, 0);
		}

		public static Vertex3D GetRotatedAxis(float angle, Vertex3D axis, Vertex3D temp)
		{
			var u = axis;
			u.Normalize();

			var sinAngle = MathF.Sin((float)(System.Math.PI / 180.0)*angle);
			var cosAngle = MathF.Cos((float)(System.Math.PI / 180.0)*angle);
			var oneMinusCosAngle = 1.0f - cosAngle;

			var rotMatrixRow0 = new Vertex3D();
			var rotMatrixRow1 = new Vertex3D();
			var rotMatrixRow2 = new Vertex3D();

			rotMatrixRow0.X = u.X * u.X + cosAngle * (1.0f - u.X * u.X);
			rotMatrixRow0.Y = u.X * u.Y * oneMinusCosAngle - sinAngle * u.Z;
			rotMatrixRow0.Z = u.X * u.Z * oneMinusCosAngle + sinAngle * u.Y;

			rotMatrixRow1.X = u.X * u.Y * oneMinusCosAngle + sinAngle * u.Z;
			rotMatrixRow1.Y = u.Y * u.Y + cosAngle * (1.0f - u.Y * u.Y);
			rotMatrixRow1.Z = u.Y * u.Z * oneMinusCosAngle - sinAngle * u.X;

			rotMatrixRow2.X = u.X * u.Z * oneMinusCosAngle - sinAngle * u.Y;
			rotMatrixRow2.Y = u.Y * u.Z * oneMinusCosAngle + sinAngle * u.X;
			rotMatrixRow2.Z = u.Z * u.Z + cosAngle * (1.0f - u.Z * u.Z);

			return new Vertex3D(temp.Dot(rotMatrixRow0), temp.Dot(rotMatrixRow1), temp.Dot(rotMatrixRow2));
		}

		public Vertex3D MultiplyMatrix(Matrix3D matrix)
		{
			return matrix.MultiplyMatrix(this);
		}

		public Vertex3D MultiplyMatrixNoTranslate(Matrix3D matrix)
		{
			return matrix.MultiplyMatrixNoTranslate(this);
		}

		public override string ToString()
		{
			return $"Vertex3D({X}/{Y}/{Z})";
		}

		public float Magnitude() => MathF.Sqrt(this.Dot(this));
	}
}
