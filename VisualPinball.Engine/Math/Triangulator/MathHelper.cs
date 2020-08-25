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
