// ReSharper disable CompareOfFloatsByEqualityOperator

using System;

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

		public Tuple<float, float, float, float> Column1 => new Tuple<float, float, float, float>(_11, _12, _13, _14);
		public Tuple<float, float, float, float> Column2 => new Tuple<float, float, float, float>(_21, _22, _23, _24);
		public Tuple<float, float, float, float> Column3 => new Tuple<float, float, float, float>(_31, _32, _33, _34);
		public Tuple<float, float, float, float> Column4 => new Tuple<float, float, float, float>(_41, _42, _43, _44);

		public Matrix3D Set(float[][] matrix) {
			for (var i = 0; i < 4; i++) {
				for (var j = 0; j < 4; j++) {
					_matrix[i][j] = matrix[i][j];
				}
			}
			return this;
		}

		public bool IsIdentity()
		{
			return _11 == 1f && _22 == 1f && _33 == 1f &&_44 == 1f
				&& _12 == 0f && _13 == 0f && _14 == 0f && _41 == 0f
				&& _21 == 0f && _23 == 0f && _24 == 0f && _42 == 0f
				&& _31 == 0f && _32 == 0f && _34 == 0f && _43 == 0f;
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
			_11 = _22 = _33 = _44 = 1.0f;
			_12 = _13 = _14 = _41 =
			_21 = _23 = _24 = _42 =
			_31 = _32 = _34 = _43 = 0.0f;
			return this;
		}

		public Matrix3D SetTranslation(float tx, float ty, float tz) {
			SetIdentity();
			_41 = tx;
			_42 = ty;
			_43 = tz;
			return this;
		}

		public Vertex3D GetTranslation()
		{
			return new Vertex3D(_41, _42, _43);
		}

		public Matrix3D SetScaling(float sx, float sy, float sz) {
			SetIdentity();
			_11 = sx;
			_22 = sy;
			_33 = sz;
			return this;
		}

		public Vertex3D GetScaling()
		{
			return new Vertex3D(_11, _22, _33);
		}

		public Matrix3D RotateXMatrix(float x) {
			SetIdentity();
			_22 = _33 = MathF.Cos((x));
			_23 = MathF.Sin((x));
			_32 = -_23;
			return this;
		}

		public Matrix3D RotateYMatrix(float y) {
			SetIdentity();
			_11 = _33 = MathF.Cos((y));
			_31 = MathF.Sin((y));
			_13 = -_31;
			return this;
		}

		public Matrix3D RotateZMatrix(float z) {
			SetIdentity();
			_11 = _22 = MathF.Cos((z));
			_12 = MathF.Sin((z));
			_21 = -_12;
			return this;
		}

		/* multiplyVector() has moved to {@link Vertex3D.multiplyMatrix()} */
		/* multiplyVectorNoTranslate() has moved to {@link Vertex3D.multiplyMatrixNoTranslate()} */

		public Matrix3D Multiply(Matrix3D a, Matrix3D b = null) {
			var product = b != null
				? MultiplyMatrices(a, b)
				: MultiplyMatrices(this, a);

			return Set(product._matrix);
		}

		public Matrix3D PreMultiply(Matrix3D a) {
			var product = MultiplyMatrices(a, this);
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
