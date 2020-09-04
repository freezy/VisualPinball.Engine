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

namespace VisualPinball.Engine.Math.Mesh
{
	internal readonly struct ProgMeshFloat3
	{
		public readonly float X;
		public readonly float Y;
		public readonly float Z;

		public ProgMeshFloat3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public ProgMeshFloat3 Sub(ProgMeshFloat3 b)
		{
			return new ProgMeshFloat3(X - b.X, Y - b.Y, Z - b.Z);
		}

		public ProgMeshFloat3 DivideScalar(float s)
		{
			return MultiplyScalar(1f / s);
		}

		public float Magnitude()
		{
			return MathF.Sqrt(Dot(this));
		}

		public float Dot(ProgMeshFloat3 b) {
			return X * b.X + Y * b.Y + Z * b.Z;
		}

		public static ProgMeshFloat3 Cross(ProgMeshFloat3 a, ProgMeshFloat3 b)
		{
			return new ProgMeshFloat3(
				a.Y * b.Z - a.Z * b.Y,
				a.Z * b.X - a.X * b.Z,
				a.X * b.Y - a.Y * b.X
			);
		}

		private ProgMeshFloat3 MultiplyScalar(float s)
		{
			return new ProgMeshFloat3(X * s, Y * s, Z * s);
		}
	}
}
