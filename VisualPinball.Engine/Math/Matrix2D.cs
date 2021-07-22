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
	}
}
