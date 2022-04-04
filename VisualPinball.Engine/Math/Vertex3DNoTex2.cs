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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace VisualPinball.Engine.Math
{
	[Serializable]
	public struct Vertex3DNoTex2 : IEquatable<Vertex3DNoTex2>
	{
		public const int Size = 32;

		public float X;
		public float Y;
		public float Z;

		public float Nx;
		public float Ny;
		public float Nz;

		public float Tu;
		public float Tv;

		public Vertex3DNoTex2(BinaryReader reader)
		{
			var startPos = reader.BaseStream.Position;
			X = reader.ReadSingle();
			Y = reader.ReadSingle();
			Z = reader.ReadSingle();
			Nx = reader.ReadSingle();
			Ny = reader.ReadSingle();
			Nz = reader.ReadSingle();
			Tu = reader.ReadSingle();
			Tv = reader.ReadSingle();
			var remainingSize = Size - (reader.BaseStream.Position - startPos);
			if (remainingSize > 0) {
				throw new InvalidOperationException();
			}
		}

		public Vertex3DNoTex2(IReadOnlyList<float> arr) {
			X  = arr.Count > 0 ? arr[0] : float.NaN;
			Y = arr.Count > 1 ? arr[1] : float.NaN;
			Z = arr.Count > 2 ? arr[2] : float.NaN;
			Nx = arr.Count > 3 ? arr[3] : float.NaN;
			Ny = arr.Count > 4 ? arr[4] : float.NaN;
			Nz = arr.Count > 5 ? arr[5] : float.NaN;
			Tu = arr.Count > 6 ? arr[6] : float.NaN;
			Tv = arr.Count > 7 ? arr[7] : float.NaN;
		}

		public Vertex3DNoTex2(float x, float y, float z, float nx, float ny, float nz, float tu, float tv)
		{
			X = x;
			Y = y;
			Z = z;
			Nx = nx;
			Ny = ny;
			Nz = nz;
			Tu = tu;
			Tv = tv;
		}

		public Vertex3DNoTex2(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
			Nx = 0;
			Ny = 0;
			Nz = 0;
			Tu = 0;
			Tv = 0;
		}

		public void Write(BinaryWriter writer)
		{
			writer.Write(X);
			writer.Write(Y);
			writer.Write(Z);
			writer.Write(Nx);
			writer.Write(Ny);
			writer.Write(Nz);
			writer.Write(Tu);
			writer.Write(Tv);
		}

		public Vertex3D GetVertex() {
			return new Vertex3D(X, Y, Z);
		}

		[ExcludeFromCodeCoverage]
		public override string ToString()
		{
			return $"Vertex3DNoTex2({X}/{Y}/{Z}, {Nx}/{Ny}/{Nz}, {Tu}/{Tv})";
		}
		public void MultiplyMatrix(Matrix3D m)
		{
			var v = new Vertex3D(X, Y, Z).MultiplyMatrix(m);
			X = v.X;
			Y = v.Y;
			Z = v.Z;
		}

		public bool Equals(Vertex3DNoTex2 other)
		{
			return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && Nx.Equals(other.Nx) && Ny.Equals(other.Ny) && Nz.Equals(other.Nz) && Tu.Equals(other.Tu) && Tv.Equals(other.Tv);
		}

		public bool Equals(Vertex3DNoTex2 other, int precision)
		{
			return Round(X, precision) == Round(other.X, precision)
			       && Round(Y, precision) == Round(other.Y, precision)
			       && Round(Z, precision) == Round(other.Z, precision)
			       && Round(Nx, precision) == Round(other.Nx, precision)
			       && Round(Ny, precision) == Round(other.Ny, precision)
			       && Round(Nz, precision) == Round(other.Nz, precision)
			       && Round(Tu, precision) == Round(other.Tu, precision)
			       && Round(Tv, precision) == Round(other.Tv, precision);
		}

		public override bool Equals(object obj)
		{
			return obj is Vertex3DNoTex2 other && Equals(other);
		}

		public override int GetHashCode()
		{
			return (X, Y, Z, Nx, Ny, Nz, Tu, Tv).GetHashCode();
		}

		public int GetRoundedHash(int digits) => (
			Round(X, digits), Round(Y, digits), Round(Z, digits),
			Round(Nx, digits), Round(Ny, digits), Round(Nz, digits),
			Round(Tu, digits), Round(Tv, digits)
		).GetHashCode();

		private static int Round(float value, int digits) => (int)System.Math.Round(value * System.Math.Pow(10, digits));
	}
}
