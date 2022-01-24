// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using VisualPinball.Engine.Common;

namespace VisualPinball.Engine.Math
{
	[Serializable]
	public struct Vertex3D
	{
		public static readonly Vertex3D One = new Vertex3D(1.0f, 1.0f, 1.0f);
		public static readonly Vertex3D Zero = new Vertex3D(0, 0, 0);

		public float X;
		public float Y;
		public float Z;

		public Vertex3D(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public Vertex3D(BinaryReader reader, int len)
		{
			X = reader.ReadSingle();
			Y = reader.ReadSingle();
			if (len >= 12) {
				Z = reader.ReadSingle();
			} else {
				Z = 0;
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

		public void Normalize()
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

		public float Length()
		{
			return MathF.Sqrt(X * X + Y * Y + Z * Z);
		}

		public float LengthSq()
		{
			return X * X + Y * Y + Z * Z;
		}

		public float Dot(Vertex3D v)
		{
			return X * v.X + Y * v.Y + Z * v.Z;
		}

		public static Vertex3D CrossVectors(Vertex3D a, Vertex3D b)
		{
			var ax = a.X;
			var ay = a.Y;
			var az = a.Z;
			var bx = b.X;
			var by = b.Y;
			var bz = b.Z;

			return new Vertex3D(
				ay * bz - az * by,
				az * bx - ax * bz,
				ax * by - ay * bx
			);
		}

		public bool IsZero()
		{
			return MathF.Abs(X) < Constants.FloatMin && MathF.Abs(Y) < Constants.FloatMin &&
			       MathF.Abs(Z) < Constants.FloatMin;
		}

		public static Vertex3D CrossProduct(Vertex3D pv1, Vertex3D pv2)
		{
			return new Vertex3D(
				pv1.Y * pv2.Z - pv1.Z * pv2.Y,
				pv1.Z * pv2.X - pv1.X * pv2.Z,
				pv1.X * pv2.Y - pv1.Y * pv2.X
			);
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

		[ExcludeFromCodeCoverage]
		public override string ToString()
		{
			return $"Vertex3D({X}/{Y}/{Z})";
		}
	}
}
