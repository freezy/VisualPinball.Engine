
using System;
using System.Diagnostics.CodeAnalysis;

namespace VisualPinball.Engine.Math
{
	/// <summary>
	/// A System.Math wrapper for floats.
	/// </summary>
	///
	/// <see href="https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Mathf.cs">Original Source</see>
	[ExcludeFromCodeCoverage]
	public static class MathF
	{

		// The infamous ''3.14159265358979...'' value (RO).
		public const float PI = (float)System.Math.PI;

		// Returns the sine of angle /f/ in radians.
		public static float Sin(float f) { return (float)System.Math.Sin(f); }

		// Returns the cosine of angle /f/ in radians.
		public static float Cos(float f) { return (float)System.Math.Cos(f); }

		// Returns the absolute value of /f/.
		public static float Abs(float f) { return (float)System.Math.Abs(f); }

		// Returns square root of /f/.
		public static float Sqrt(float f) { return (float)System.Math.Sqrt(f); }

		public static float DegToRad(float deg) { return deg * (PI / 180.0f); }

		public static float RadToDeg(float rad) { return rad * (180.0f / PI); }

		// Returns /f/ raised to power /p/.
		public static float Pow(float f, float p) { return (float)System.Math.Pow(f, p); }

		/// *listonly*
		public static float Min(float a, float b)
		{
			return a < b ? a : b;
		}

		// Returns the smallest of two or more values.
		public static float Min(params float[] values)
		{
			var len = values.Length;
			if (len == 0)
				return 0;
			var m = values[0];
			for (var i = 1; i < len; i++) {
				if (values[i] < m)
					m = values[i];
			}

			return m;
		}

		/// *listonly*
		public static int Min(int a, int b)
		{
			return a < b ? a : b;
		}

		// Returns the smallest of two or more values.
		public static int Min(params int[] values)
		{
			var len = values.Length;
			if (len == 0)
				return 0;
			var m = values[0];
			for (var i = 1; i < len; i++) {
				if (values[i] < m)
					m = values[i];
			}

			return m;
		}

		/// *listonly*
		public static float Max(float a, float b)
		{
			return a > b ? a : b;
		}

		// Returns largest of two or more values.
		public static float Max(params float[] values)
		{
			var len = values.Length;
			if (len == 0)
				return 0;
			var m = values[0];
			for (var i = 1; i < len; i++) {
				if (values[i] > m)
					m = values[i];
			}

			return m;
		}

		/// *listonly*
		public static int Max(int a, int b)
		{
			return a > b ? a : b;
		}

		// Returns the largest of two or more values.
		public static int Max(params int[] values)
		{
			var len = values.Length;
			if (len == 0)
				return 0;
			var m = values[0];
			for (var i = 1; i < len; i++) {
				if (values[i] > m)
					m = values[i];
			}

			return m;
		}

		public static float Random()
		{
			var random = new Random();
			return (float) random.NextDouble();
		}
	}
}
