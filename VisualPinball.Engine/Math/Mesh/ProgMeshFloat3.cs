// Progressive Mesh type Polygon Reduction Algorithm
//   by Stan Melax (c) 1998
//
// Permission to use any of this code wherever you want is granted..
// Although, please do acknowledge authorship if appropriate.

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
