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


using System;

namespace VisualPinball.Engine.Math
{
	/// <summary>
	/// A System.Math wrapper for floats.
	/// </summary>
	///
	/// <see href="https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Mathf.cs">Original Source</see>
	public static class MathF
	{

		// The infamous ''3.14159265358979...'' value (RO).
		public const float PI = (float)System.Math.PI;

		// Returns the sine of angle /f/ in radians.
		public static float Sin(float f) { return (float)System.Math.Sin(f); }

		// Returns the cosine of angle /f/ in radians.
		public static float Cos(float f) { return (float)System.Math.Cos(f); }

		// Returns the arc-sine of /f/ - the angle in radians whose sine is /f/.
		public static float Asin(float f) { return (float)System.Math.Asin(f); }

		// Returns the angle in radians whose ::ref::Tan is @@y/x@@.
		public static float Atan2(float y, float x) { return (float)System.Math.Atan2(y, x); }

		// Returns the sign of /f/.
		public static float Sign(float f) { return f >= 0F ? 1F : -1F; }

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

		/// <summary>
		/// Returns a random number between 0f and 1f.
		/// </summary>
		/// <returns></returns>
		public static float Random()
		{
			var random = new Random();
			return (float) random.NextDouble();
		}

		/// <summary>
		/// Clamps a value between a minimum float and maximum float value.
		/// </summary>
		/// <param name="value">Value to clamp</param>
		/// <param name="min">Minimal value</param>
		/// <param name="max">Maximal value</param>
		/// <returns></returns>
		public static float Clamp(float value, float min, float max)
		{
			if (value < min)
				value = min;
			else if (value > max)
				value = max;
			return value;
		}
	}
}
