namespace VisualPinball.Engine.Math
{
	public class Matrix3D
	{
		public static readonly Matrix3D RightHanded = new Matrix3D().SetEach(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1);
		public static readonly Matrix3D Identity = new Matrix3D().SetEach(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);

		private readonly float[][] _matrix = {
			new [] { 1f, 0f, 0f, 0f },
			new [] { 0f, 1f, 0f, 0f },
			new [] { 0f, 0f, 1f, 0f },
			new [] { 0f, 0f, 0f, 1f },
		};

		public Matrix3D Set(float[][] matrix) {
			for (var i = 0; i < 4; i++) {
				for (var j = 0; j < 4; j++) {
					_matrix[i][j] = matrix[i][j];
				}
			}
			return this;
		}

		public Matrix3D SetEach(params float[] m) {
			for (var i = 0; i < 4; i++) {
				for (var j = 0; j < 4; j++) {
					_matrix[i][j] = m[i * 4 + j];
				}
			}
			return this;
		}

		public Matrix3D SetIdentity() {
			this._11 = this._22 = this._33 = this._44 = 1.0f;
			this._12 = this._13 = this._14 = this._41 =
			this._21 = this._23 = this._24 = this._42 =
			this._31 = this._32 = this._34 = this._43 = 0.0f;
			return this;
		}

		public Matrix3D SetTranslation(float tx, float ty, float tz) {
			SetIdentity();
			this._41 = tx;
			this._42 = ty;
			this._43 = tz;
			return this;
		}

		public Matrix3D SetScaling(float sx, float sy, float sz) {
			SetIdentity();
			this._11 = sx;
			this._22 = sy;
			this._33 = sz;
			return this;
		}

		public Matrix3D RotateXMatrix(float x) {
			SetIdentity();
			this._22 = this._33 = MathF.Cos((x));
			this._23 = MathF.Sin((x));
			this._32 = -this._23;
			return this;
		}

		public Matrix3D RotateYMatrix(float y) {
			SetIdentity();
			this._11 = this._33 = MathF.Cos((y));
			this._31 = MathF.Sin((y));
			this._13 = -this._31;
			return this;
		}

		public Matrix3D RotateZMatrix(float z) {
			SetIdentity();
			this._11 = this._22 = MathF.Cos((z));
			this._12 = MathF.Sin((z));
			this._21 = -this._12;
			return this;
		}

		/* multiplyVector() has moved to {@link Vertex3D.multiplyMatrix()} */
		/* multiplyVectorNoTranslate() has moved to {@link Vertex3D.multiplyMatrixNoTranslate()} */

		public Matrix3D Multiply(Matrix3D a, Matrix3D b = null) {
			var product = b != null
				? Matrix3D.MultiplyMatrices(a, b)
				: Matrix3D.MultiplyMatrices(this, a);

			return Set(product._matrix);
		}

		public Matrix3D PreMultiply(Matrix3D a) {
			var product = Matrix3D.MultiplyMatrices(a, this);
			return Set(product._matrix);
		}

		public Matrix3D ToRightHanded() {
			var tempMat = new Matrix3D().SetScaling(1, 1, -1);
			return Multiply(tempMat);
		}

		private static Matrix3D MultiplyMatrices(Matrix3D a, Matrix3D b) {
			/* istanbul ignore else: we always recycle now */
			var result = new Matrix3D();
			for (var i = 0; i < 4; ++i) {
				for (var l = 0; l < 4; ++l) {
					result._matrix[i][l] =
						((((a._matrix[0][l] * b._matrix[i][0]) +
						(a._matrix[1][l] * b._matrix[i][1])) +
						(a._matrix[2][l] * b._matrix[i][2])) +
						(a._matrix[3][l] * b._matrix[i][3]));
				}
			}
			return result;
		}

		public Vertex3D MultiplyMatrix(Vertex3D v) {
			// Transform it through the current matrix set
			var xp = ((((_11 * v.X) + (_21 * v.Y)) + (_31 * v.Z)) + _41);
			var yp = ((((_12 * v.X) + (_22 * v.Y)) + (_32 * v.Z)) + _42);
			var zp = ((((_13 * v.X) + (_23 * v.Y)) + (_33 * v.Z)) + _43);
			var wp = ((((_14 * v.X) + (_24 * v.Y)) + (_34 * v.Z)) + _44);
			var invWp = (1.0f / wp);
			return v.Set(xp * invWp, yp * invWp, zp * invWp);
		}

		public Vertex3D MultiplyMatrixNoTranslate(Vertex3D v) {
			// Transform it through the current matrix set
			var xp = ((_11 * v.X) + (_21 * v.Y)) + (_31 * v.Z);
			var yp = ((_12 * v.X) + (_22 * v.Y)) + (_32 * v.Z);
			var zp = ((_13 * v.X) + (_23 * v.Y)) + (_33 * v.Z);
			return v.Set(xp, yp, zp);
		}

		public Matrix3D Clone() {
			return new Matrix3D().Set(_matrix);
		}

		public bool Equals(Matrix3D matrix)  {
			for (var i = 0; i < 4; i++) {
				for (var j = 0; j < 4; j++) {
					if (_matrix[i][j] != matrix._matrix[i][j]) {
						return false;
					}
				}
			}
			return true;
		}

		private float _11 {
			get => _matrix[0][0];
			set => _matrix[0][0] = value;
		}
		private float _12 {
			get => _matrix[1][0];
			set => _matrix[1][0] = value;
		}
		private float _13 {
			get => _matrix[2][0];
			set => _matrix[2][0] = value;
		}
		private float _14 {
			get => _matrix[3][0];
			set => _matrix[3][0] = value;
		}
		private float _21 {
			get => _matrix[0][1];
			set => _matrix[0][1] = value;
		}
		private float _22 {
			get => _matrix[1][1];
			set => _matrix[1][1] = value;
		}
		private float _23 {
			get => _matrix[2][1];
			set => _matrix[2][1] = value;
		}
		private float _24 {
			get => _matrix[3][1];
			set => _matrix[3][1] = value;
		}
		private float _31 {
			get => _matrix[0][2];
			set => _matrix[0][2] = value;
		}
		private float _32 {
			get => _matrix[1][2];
			set => _matrix[1][2] = value;
		}
		private float _33 {
			get => _matrix[2][2];
			set => _matrix[2][2] = value;
		}
		private float _34 {
			get => _matrix[3][2];
			set => _matrix[3][2] = value;
		}
		private float _41 {
			get => _matrix[0][3];
			set => _matrix[0][3] = value;
		}
		private float _42 {
			get => _matrix[1][3];
			set => _matrix[1][3] = value;
		}
		private float _43 {
			get => _matrix[2][3];
			set => _matrix[2][3] = value;
		}
		private float _44 {
			get => _matrix[3][3];
			set => _matrix[3][3] = value;
		}
	}
}
