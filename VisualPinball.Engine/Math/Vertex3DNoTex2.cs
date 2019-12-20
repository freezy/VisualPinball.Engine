using System.Collections.Generic;
using System.IO;

namespace VisualPinball.Engine.Math
{
	public class Vertex3DNoTex2
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

		public Vertex3DNoTex2(BinaryReader reader) {
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
				reader.BaseStream.Seek(remainingSize, SeekOrigin.Current);
			}
		}

		public Vertex3DNoTex2(IReadOnlyList<float> arr) {
			X  = arr.Count > 0 ? arr[0] : float.NaN;
			Y = arr.Count > 0 ? arr[1] : float.NaN;
			Z = arr.Count > 0 ? arr[2] : float.NaN;
			Nx = arr.Count > 0 ? arr[3] : float.NaN;
			Ny = arr.Count > 0 ? arr[4] : float.NaN;
			Nz = arr.Count > 0 ? arr[5] : float.NaN;
			Tu = arr.Count > 0 ? arr[6] : float.NaN;
			Tv = arr.Count > 0 ? arr[7] : float.NaN;
		}

		public Vertex3DNoTex2()
		{
		}

		public Vertex3D GetVertex() {
			return new Vertex3D(X, Y, Z);
		}

		public Vertex3DNoTex2 Clone() {
			var vertex = new Vertex3DNoTex2 {
				X = X,
				Y = Y,
				Z = Z,
				Nx = Nx,
				Ny = Ny,
				Nz = Nz,
				Tu = Tu,
				Tv = Tv
			};
			return vertex;
		}

		public bool HasTextureCoordinates() {
			return !float.IsNaN(Tu) && !float.IsNaN(Tv);
		}
	}
}
