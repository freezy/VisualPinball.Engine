using System;

namespace VisualPinball.Engine.Math
{
	public abstract class CatmullCurve
	{
		public static CatmullCurve Instance<T>(Vertex3D v0, Vertex3D v1, Vertex3D v2, Vertex3D v3) where T: IRenderVertex =>
			typeof(T) == typeof(RenderVertex2D) ? (CatmullCurve)new CatmullCurve2D(v0, v1, v2, v3) :
			typeof(T) == typeof(RenderVertex3D) ? new CatmullCurve3D(v0, v1, v2, v3) :
			null;

		public abstract IRenderVertex GetPointAt(float t);

		protected static Tuple<float, float, float> Clamp(float dt0, float dt1, float dt2) {

			// check for repeated control points
			if (dt1 < 1e-4) {
				dt1 = 1.0f;
			}
			if (dt0 < 1e-4) {
				dt0 = dt1;
			}
			if (dt2 < 1e-4) {
				dt2 = dt1;
			}
			return new Tuple<float, float, float>(dt0, dt1, dt2);
		}

		protected static float[] InitNonuniformCatmullCoeffs(float x0, float x1, float x2, float x3, float dt0, float dt1, float dt2) {

			// compute tangents when parameterized in [t1,t2]
			var t1 = (x1 - x0) / dt0 - (x2 - x0) / (dt0 + dt1) + (x2 - x1) / dt1;
			var t2 = (x2 - x1) / dt1 - (x3 - x1) / (dt1 + dt2) + (x3 - x2) / dt2;

			// rescale tangents for parametrization in [0,1]
			t1 *= dt1;
			t2 *= dt1;

			return InitCubicSplineCoeffs(x1, x2, t1, t2);
		}

		private static float[] InitCubicSplineCoeffs(float x0, float x1, float t0, float t1)
		{
			return new[] {
				x0,
				t0,
				-3.0f * x0 + 3.0f * x1 - 2.0f * t0 - t1,
				2.0f * x0 - 2.0f * x1 + t0 + t1,
			};
		}
	}

	public class CatmullCurve2D : CatmullCurve
	{
		private readonly Coeff2 _c = new Coeff2();

		internal CatmullCurve2D(Vertex2D v0, Vertex2D v1, Vertex2D v2, Vertex2D v3)
		{
			var (dt0, dt1, dt2) = Clamp(
				MathF.Sqrt(v1.Clone().Sub(v0).Length()),
				MathF.Sqrt(v2.Clone().Sub(v1).Length()),
				MathF.Sqrt(v3.Clone().Sub(v2).Length())
			);
			_c.X = InitNonuniformCatmullCoeffs(v0.X, v1.X, v2.X, v3.X, dt0, dt1, dt2);
			_c.Y = InitNonuniformCatmullCoeffs(v0.Y, v1.Y, v2.Y, v3.Y, dt0, dt1, dt2);
		}

		public override IRenderVertex GetPointAt(float t)
		{
			var t2 = t * t;
			var t3 = t2 * t;
			return new RenderVertex2D(
				_c.X[3] * t3 + _c.X[2] * t2 + _c.X[1] * t + _c.X[0],
				_c.Y[3] * t3 + _c.Y[2] * t2 + _c.Y[1] * t + _c.Y[0]
			);
		}

	}

	public class CatmullCurve3D : CatmullCurve
	{
		private readonly Coeff3 _c = new Coeff3();

		internal CatmullCurve3D(Vertex3D v0, Vertex3D v1, Vertex3D v2, Vertex3D v3) {
			var (dt0, dt1, dt2) = Clamp(
				MathF.Sqrt(v1.Clone().Sub(v0).Length()),
				MathF.Sqrt(v2.Clone().Sub(v1).Length()),
				MathF.Sqrt(v3.Clone().Sub(v2).Length())
			);
			_c.X = InitNonuniformCatmullCoeffs(v0.X, v1.X, v2.X, v3.X, dt0, dt1, dt2);
			_c.Y = InitNonuniformCatmullCoeffs(v0.Y, v1.Y, v2.Y, v3.Y, dt0, dt1, dt2);
			_c.Z = InitNonuniformCatmullCoeffs(v0.Z, v1.Z, v2.Z, v3.Z, dt0, dt1, dt2);
		}

		public override IRenderVertex GetPointAt(float t) {
			var t2 = t * t;
			var t3 = t2 * t;
			return new RenderVertex3D(
				_c.X[3] * t3 + _c.X[2] * t2 + _c.X[1] * t + _c.X[0],
				_c.Y[3] * t3 + _c.Y[2] * t2 + _c.Y[1] * t + _c.Y[0],
				_c.Z[3] * t3 + _c.Z[2] * t2 + _c.Z[1] * t + _c.Z[0]
			);
		}
	}

	internal class Coeff2
	{
		public float[] X = {0f, 0f, 0f, 0f};
		public float[] Y = {0f, 0f, 0f, 0f};
	}

	internal class Coeff3 : Coeff2
	{
		public float[] Z = {0f, 0f, 0f, 0f};
	}
}
