namespace VisualPinball.Engine.Math
{
	public class Matrix2D
	{
		public readonly float[][] Matrix = {
			new[] {1f, 0f, 0f},
			new[] {0f, 1f, 0f},
			new[] {0f, 0f, 1f},
		};

		public Matrix2D SetIdentity()
		{
			Matrix[0][0] = 1;
			Matrix[0][1] = 0;
			Matrix[0][2] = 0;
			Matrix[1][0] = 0;
			Matrix[1][1] = 1;
			Matrix[1][2] = 0;
			Matrix[2][0] = 0;
			Matrix[2][1] = 0;
			Matrix[2][2] = 1;
			return this;
		}

		public Vertex3D MultiplyVectorT(Vertex3D v)
		{
			return new Vertex3D(
				Matrix[0][0] * v.X + Matrix[1][0] * v.Y + Matrix[2][0] * v.Z,
				Matrix[0][1] * v.X + Matrix[1][1] * v.Y + Matrix[2][1] * v.Z,
				Matrix[0][2] * v.X + Matrix[1][2] * v.Y + Matrix[2][2] * v.Z);
		}

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

		public Matrix2D CreateSkewSymmetric(Vertex3D pv3D)
		{
			Matrix[0][0] = 0;
			Matrix[0][1] = -pv3D.Z;
			Matrix[0][2] = pv3D.Y;
			Matrix[1][0] = pv3D.Z;
			Matrix[1][1] = 0;
			Matrix[1][2] = -pv3D.X;
			Matrix[2][0] = -pv3D.Y;
			Matrix[2][1] = pv3D.X;
			Matrix[2][2] = 0;
			return this;
		}

		public Matrix2D Clone()
		{
			var m = new Matrix2D();
			for (var i = 0; i < 3; ++i) {
				for (var l = 0; l < 3; ++l) {
					m.Matrix[i][l] = Matrix[i][l];
				}
			}

			return m;
		}

		public Matrix2D Set(Matrix2D m)
		{
			for (var i = 0; i < 3; ++i) {
				for (var l = 0; l < 3; ++l) {
					Matrix[i][l] = m.Matrix[i][l];
				}
			}

			return this;
		}

		public void MultiplyMatrix(Matrix2D m1, Matrix2D m2)
		{
			for (var i = 0; i < 3; ++i) {
				for (var l = 0; l < 3; ++l) {
					Matrix[i][l] =
						m1.Matrix[i][0] * m2.Matrix[0][l] +
						m1.Matrix[i][1] * m2.Matrix[1][l] +
						m1.Matrix[i][2] * m2.Matrix[2][l];
				}
			}
		}

		public Matrix2D MultiplyScalar(float scalar)
		{
			for (var i = 0; i < 3; ++i) {
				for (var l = 0; l < 3; ++l) {
					Matrix[i][l] *= scalar;
				}
			}

			return this;
		}

		public Matrix2D AddMatrix(Matrix2D m1, Matrix2D m2)
		{
			for (var i = 0; i < 3; ++i) {
				for (var l = 0; l < 3; ++l) {
					Matrix[i][l] = m1.Matrix[i][l] + m2.Matrix[i][l];
				}
			}

			return this;
		}

		public void OrthoNormalize()
		{
			var vX = new Vertex3D(Matrix[0][0], Matrix[1][0], Matrix[2][0]);
			var vY = new Vertex3D(Matrix[0][1], Matrix[1][1], Matrix[2][1]);
			var vZ = Vertex3D.CrossProduct(vX, vY);
			vX.Normalize();
			vZ.Normalize();
			var vYY = Vertex3D.CrossProduct(vZ, vX);

			Matrix[0][0] = vX.X;
			Matrix[0][1] = vYY.X;
			Matrix[0][2] = vZ.X;
			Matrix[1][0] = vX.Y;
			Matrix[1][1] = vYY.Y;
			Matrix[1][2] = vZ.Y;
			Matrix[2][0] = vX.Z;
			Matrix[2][1] = vYY.Z;
			Matrix[2][2] = vZ.Z;
		}

		public bool Equals(Matrix2D m)
		{
			return Matrix[0][0] == m.Matrix[0][0] && Matrix[0][1] == m.Matrix[0][1] && Matrix[0][2] == m.Matrix[0][2]
			    && Matrix[1][0] == m.Matrix[1][0] && Matrix[1][1] == m.Matrix[1][1] && Matrix[1][2] == m.Matrix[1][2]
			    && Matrix[2][0] == m.Matrix[2][0] && Matrix[2][1] == m.Matrix[2][1] && Matrix[2][2] == m.Matrix[2][2];
		}

		public override string ToString()
		{
			return
				$"[{System.Math.Round(Matrix[0][0], 3)}, {System.Math.Round(Matrix[0][1], 3)}, {System.Math.Round(Matrix[0][2], 3)}]\n" +
				$"[{System.Math.Round(Matrix[1][0], 3)}, {System.Math.Round(Matrix[1][1], 3)}, {System.Math.Round(Matrix[1][2], 3)}]\n" +
				$"[{System.Math.Round(Matrix[2][0], 3)}, {System.Math.Round(Matrix[2][1], 3)}, {System.Math.Round(Matrix[2][2], 3)}]\n";
		}
	}
}
