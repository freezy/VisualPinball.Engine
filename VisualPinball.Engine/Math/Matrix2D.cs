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

namespace VisualPinball.Engine.Math
{
	public struct Matrix2D
	{
		public float M00;
		public float M01;
		public float M02;
		public float M10;
		public float M11;
		public float M12;
		public float M20;
		public float M21;
		public float M22;

		public Matrix2D(float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22)
		{
			M00 = m00;
			M01 = m01;
			M02 = m02;
			M10 = m10;
			M11 = m11;
			M12 = m12;
			M20 = m20;
			M21 = m21;
			M22 = m22;
		}

		public static Matrix2D Identity = new Matrix2D(
			1f, 0f, 0f,
			0f, 1f, 0f,
			0f, 0f, 1f
		);

		public Matrix2D RotationAroundAxis(Vertex3D axis, float rSin, float rCos)
		{
			M00 = axis.X * axis.X + rCos * (1.0f - axis.X * axis.X);
			M10 = axis.X * axis.Y * (1.0f - rCos) - axis.Z * rSin;
			M20 = axis.Z * axis.X * (1.0f - rCos) + axis.Y * rSin;

			M01 = axis.X * axis.Y * (1.0f - rCos) + axis.Z * rSin;
			M11 = axis.Y * axis.Y + rCos * (1.0f - axis.Y * axis.Y);
			M21 = axis.Y * axis.Z * (1.0f - rCos) - axis.X * rSin;

			M02 = axis.Z * axis.X * (1.0f - rCos) - axis.Y * rSin;
			M12 = axis.Y * axis.Z * (1.0f - rCos) + axis.X * rSin;
			M22 = axis.Z * axis.Z + rCos * (1.0f - axis.Z * axis.Z);

			return this;
		}
	}
}
