// Triangulator
//
// The MIT License (MIT)
//
// Copyright (c) 2017, Nick Gravelyn
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

namespace VisualPinball.Engine.Math.Triangulator
{
	internal static class MathHelper
	{
		/// <summary>
		/// Returns the greater of two values.
		/// </summary>
		/// <param name="value1">Source value.</param>
		/// <param name="value2">Source value.</param>
		/// <returns>The greater value.</returns>
		public static float Max(float value1, float value2)
		{
			return value1 > value2 ? value1 : value2;
		}

		/// <summary>
		/// Restricts a value to be within a specified range.
		/// </summary>
		/// <param name="value">The value to clamp.</param>
		/// <param name="min">
		/// The minimum value. If <c>value</c> is less than <c>min</c>, <c>min</c>
		/// will be returned.
		/// </param>
		/// <param name="max">
		/// The maximum value. If <c>value</c> is greater than <c>max</c>, <c>max</c>
		/// will be returned.
		/// </param>
		/// <returns>The clamped value.</returns>
		public static float Clamp(float value, float min, float max)
		{
			// First we check to see if we're greater than the max.
			value = value > max ? max : value;

			// Then we check to see if we're less than the min.
			value = value < min ? min : value;

			// There's no check to see if min > max.
			return value;
		}
	}
}
