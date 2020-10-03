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

namespace VisualPinball.Engine.Math
{
	/// <summary>
	/// A System.Math wrapper for floats.
	/// </summary>
	public static class MathF
	{
		public const float PI = (float)System.Math.PI;

		/// <summary>
		/// Returns the sine of the specified angle.
		/// </summary>
		/// <param name="a">An angle, measured in radians.</param>
		/// <returns>The sine of <paramref name="a">a</paramref>. If <paramref name="a">a</paramref> is equal to <see cref="F:System.Single.NaN"></see>, <see cref="F:System.Single.NegativeInfinity"></see>, or <see cref="F:System.Single.PositiveInfinity"></see>, this method returns <see cref="F:System.Single.NaN"></see>.</returns>
		public static float Sin(float a) => (float)System.Math.Sin(a);

		/// <summary>
		/// Returns the cosine of the specified angle.
		/// </summary>
		/// <param name="value">An angle, measured in radians.</param>
		/// <returns>The hyperbolic cosine of <paramref name="value">value</paramref>. If <paramref name="value">value</paramref> is equal to <see cref="F:System.Single.NegativeInfinity"></see> or <see cref="F:System.Single.PositiveInfinity"></see>, <see cref="F:System.Single.PositiveInfinity"></see> is returned. If <paramref name="value">value</paramref> is equal to <see cref="F:System.Single.NaN"></see>, <see cref="F:System.Single.NaN"></see> is returned.</returns>
		public static float Cos(float value) => (float)System.Math.Cos(value);

		/// <summary>
		/// Returns the angle whose sine is the specified number.
		/// </summary>
		/// <param name="d">A number representing a sine, where d must be greater than or equal to -1, but less than or equal to 1.</param>
		/// <returns>An angle, θ, measured in radians, such that -π/2 ≤θ≤π/2   -or-  <see cref="F:System.Single.NaN"></see> if <paramref name="d">d</paramref> &lt; -1 or <paramref name="d">d</paramref> &gt; 1 or <paramref name="d">d</paramref> equals <see cref="F:System.Single.NaN"></see>.</returns>
		public static float Asin(float d) => (float)System.Math.Asin(d);

		/// <summary>
		/// Returns the angle whose tangent is the quotient of two specified numbers.
		/// </summary>
		/// <param name="y">The y coordinate of a point.</param>
		/// <param name="x">The x coordinate of a point.</param>
		/// <returns>An angle, θ, measured in radians, such that -π≤θ≤π, and tan(θ) = <paramref name="y">y</paramref> / <paramref name="x">x</paramref>, where (<paramref name="x">x</paramref>, <paramref name="y">y</paramref>) is a point in the Cartesian plane. Observe the following:  For (<paramref name="x">x</paramref>, <paramref name="y">y</paramref>) in quadrant 1, 0 &lt; θ &lt; π/2.  For (<paramref name="x">x</paramref>, <paramref name="y">y</paramref>) in quadrant 2, π/2 &lt; θ≤π.  For (<paramref name="x">x</paramref>, <paramref name="y">y</paramref>) in quadrant 3, -π &lt; θ &lt; -π/2.  For (<paramref name="x">x</paramref>, <paramref name="y">y</paramref>) in quadrant 4, -π/2 &lt; θ &lt; 0.   For points on the boundaries of the quadrants, the return value is the following:  If y is 0 and x is not negative, θ = 0.  If y is 0 and x is negative, θ = π.  If y is positive and x is 0, θ = π/2.  If y is negative and x is 0, θ = -π/2.  If y is 0 and x is 0, θ = 0.   If <paramref name="x">x</paramref> or <paramref name="y">y</paramref> is <see cref="F:System.Single.NaN"></see>, or if <paramref name="x">x</paramref> and <paramref name="y">y</paramref> are either <see cref="F:System.Single.PositiveInfinity"></see> or <see cref="F:System.Single.NegativeInfinity"></see>, the method returns <see cref="F:System.Single.NaN"></see>.</returns>
		public static float Atan2(float y, float x) => (float)System.Math.Atan2(y, x);

		/// <summary>
		/// Returns the sign of a given number.
		/// </summary>
		/// <param name="value">A number</param>
		/// <returns>1f if greater or equal 0, -1f otherwise.</returns>
		public static float Sign(float value) => value >= 0f ? 1f : -1f;

		/// <summary>
		/// Returns the absolute value of a single-precision floating-point number.
		/// </summary>
		/// <param name="value">A number that is greater than or equal to <see cref="F:System.Single.MinValue"></see>, but less than or equal to <see cref="F:System.Single.MaxValue"></see>.</param>
		/// <returns>A single-precision floating-point number, x, such that 0 ≤ x ≤<see cref="F:System.Single.MaxValue"></see>.</returns>
		public static float Abs(float value) => System.Math.Abs(value);

		/// <summary>
		/// Returns square root of a single-precision floating-point number.
		/// </summary>
		/// <param name="value">A number that is greater than or equal to 0f.</param>
		/// <returns></returns>
		public static float Sqrt(float value) => (float)System.Math.Sqrt(value);

		/// <summary>
		/// Returns the radian value of a single-precision floating-point number in degrees.
		/// </summary>
		/// <param name="deg">Number in degrees to be converted to radian.</param>
		/// <returns>Radian value</returns>
		public static float DegToRad(float deg) => deg * (float)(System.Math.PI / 180.0);

		/// <summary>
		/// Returns the degree value of a single-precision floating-point number in radian.
		/// </summary>
		/// <param name="rad">Number in radian to be converted to degrees.</param>
		/// <returns>Degree value</returns>
		public static float RadToDeg(float rad) => rad * (float)(180.0 / System.Math.PI);

		/// <summary>
		/// Returns a specified number raised to the specified power.
		/// </summary>
		/// <param name="x">A double-precision floating-point number to be raised to a power.</param>
		/// <param name="y">A double-precision floating-point number that specifies a power.</param>
		/// <returns>The number <paramref name="x">x</paramref> raised to the power <paramref name="y">y</paramref>.</returns>
		public static float Pow(float x, float y) => (float)System.Math.Pow(x, y);

		/// <summary>
		/// Returns the smaller of two single-precision floating-point numbers.
		/// </summary>
		/// <param name="val1">The first of two single-precision floating-point numbers to compare.</param>
		/// <param name="val2">The second of two single-precision floating-point numbers to compare.</param>
		/// <returns>Parameter <paramref name="val1">val1</paramref> or <paramref name="val2">val2</paramref>, whichever is smaller. If <paramref name="val1">val1</paramref>, <paramref name="val2">val2</paramref>, or both <paramref name="val1">val1</paramref> and <paramref name="val2">val2</paramref> are equal to <see cref="F:System.Single.NaN"></see>, <see cref="F:System.Single.NaN"></see> is returned.</returns>
		public static float Min(float val1, float val2) => val1 < val2 ? val1 : val2;

		/// <summary>Returns the larger of two single-precision floating-point numbers.</summary>
		/// <param name="val1">The first of two single-precision floating-point numbers to compare.</param>
		/// <param name="val2">The second of two single-precision floating-point numbers to compare.</param>
		/// <returns>Parameter <paramref name="val1">val1</paramref> or <paramref name="val2">val2</paramref>, whichever is larger. If <paramref name="val1">val1</paramref>, or <paramref name="val2">val2</paramref>, or both <paramref name="val1">val1</paramref> and <paramref name="val2">val2</paramref> are equal to <see cref="F:System.Single.NaN"></see>, <see cref="F:System.Single.NaN"></see> is returned.</returns>
		public static float Max(float val1, float val2) => val1 > val2 ? val1 : val2;

		/// <summary>
		/// Clamps a value between a minimum float and maximum float value.
		/// </summary>
		/// <param name="value">Value to clamp</param>
		/// <param name="min">Minimal value</param>
		/// <param name="max">Maximal value</param>
		/// <returns></returns>
		public static float Clamp(float value, float min, float max)
		{
			if (value < min) {
				value = min;

			} else if (value > max) {
				value = max;
			}

			return value;
		}
	}
}
