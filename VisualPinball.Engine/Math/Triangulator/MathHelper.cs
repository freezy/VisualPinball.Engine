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
			value = (value > max) ? max : value;

			// Then we check to see if we're less than the min.
			value = (value < min) ? min : value;

			// There's no check to see if min > max.
			return value;
		}
	}
}
