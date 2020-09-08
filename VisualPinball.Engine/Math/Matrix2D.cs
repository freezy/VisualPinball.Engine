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

namespace VisualPinball.Engine.Math
{
	public class Matrix2D
	{
		public readonly float[][] Matrix = {
			new[] {1f, 0f, 0f},
			new[] {0f, 1f, 0f},
			new[] {0f, 0f, 1f},
		};

		public Matrix2D RotationAroundAxis(Vertex3D axis, float rSin, float rCos)
		{
			Matrix[0][0] = axis.X * axis.X + rCos * (1.0f - axis.X * axis.X);
			Matrix[1][0] = axis.X * axis.Y * (1.0f - rCos) - axis.Z * rSin;
			Matrix[2][0] = axis.Z * axis.X * (1.0f - rCos) + axis.Y * rSin;

			Matrix[0][1] = axis.X * axis.Y * (1.0f - rCos) + axis.Z * rSin;
			Matrix[1][1] = axis.Y * axis.Y + rCos * (1.0f - axis.Y * axis.Y);
			Matrix[2][1] = axis.Y * axis.Z * (1.0f - rCos) - axis.X * rSin;

			Matrix[0][2] = axis.Z * axis.X * (1.0f - rCos) - axis.Y * rSin;
			Matrix[1][2] = axis.Y * axis.Z * (1.0f - rCos) + axis.X * rSin;
			Matrix[2][2] = axis.Z * axis.Z + rCos * (1.0f - axis.Z * axis.Z);

			return this;
		}
	}
}
