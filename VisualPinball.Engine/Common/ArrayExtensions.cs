using System;

namespace VisualPinball.Engine.Common
{
	public static class ArrayExtensions
	{
		/// <summary>
		/// Add an element to an array, increasing its size
		/// </summary>
		/// <typeparam name="T">The array generic type</typeparam>
		/// <param name="array">The array where to add an element to</param>
		/// <param name="element">The element to add</param>
		public static void Add<T>(ref T[] array, T element)
		{
			Array.Resize<T>(ref array, array.Length + 1);
			array[array.Length - 1] = element;
		}

		/// <summary>
		/// Remove an element from an array, if found
		/// Will keep the array elements ordering
		/// Will resize the array if the element was removed
		/// </summary>
		/// <typeparam name="T">The array generic type</typeparam>
		/// <param name="array">The array where the element has to be removed</param>
		/// <param name="element">The element to remove from the array</param>
		public static void Remove<T>(ref T[] array, T element) 
		{
			int index = Array.IndexOf(array, element);
			if (index >= 0) {
				for (var i = index+1; i < array.Length; ++i) {
					array.SetValue(array.GetValue(i), i - 1);
				}
				Array.Resize<T>(ref array, array.Length - 1);
			}
		}

		/// <summary>
		/// Remove an element from an array, if found
		/// Won't keep the array elements ordering, will swap the removed element with the last one from the array
		/// Will resize the array if the element was removed
		/// </summary>
		/// <typeparam name="T">The array generic type</typeparam>
		/// <param name="array">The array where the element has to be removed</param>
		/// <param name="element">The element to remove from the array</param>
		public static void RemoveUnordered<T>(ref T[]array, T element)
		{
			int index = Array.IndexOf(array, element);
			if (index >= 0) {
				array.SetValue(array.GetValue(array.Length - 1), index);
				Array.Resize<T>(ref array, array.Length - 1);
			}
		}

		/// <summary>
		/// Offset an element in an array providing its index
		/// </summary>
		/// <param name="array">The array where we offset the element</param>
		/// <param name="index">The original index of the element</param>
		/// <param name="offset">The offset applied to the element</param>
		/// <param name="clampOffset">Tells if we want to clamp the offset so it will be applied whatever its value</param>
		public static void Offset(this Array array, int index, int offset, bool clampOffset)
		{
			if (array.Length < 2) {
				return;
			}
			var increment = System.Math.Sign(offset);
			int newIdx = clampOffset ? System.Math.Min(System.Math.Max(index + offset, 0), array.Length - 1) : index+offset;
			if (newIdx != index && newIdx >= 0 && newIdx < array.Length) {
				var value = array.GetValue(index);
				for (var i = index; i != newIdx; i += increment) {
					array.SetValue(array.GetValue(i + increment), i);
				}
				array.SetValue(value, newIdx);
			}
		}

		/// <summary>
		/// Offset an element in an array
		/// </summary>
		/// <typeparam name="T">The array generic type</typeparam>
		/// <param name="array">The array where we offset the element</param>
		/// <param name="element">The element to offset</param>
		/// <param name="offset">The offset applied to the element</param>
		/// <param name="clampOffset">Tells if we want to clamp the offset so it will be applied whatever its value</param>
		public static void OffsetElement<T>(this T[] array, T element, int offset, bool clampOffset)
		{
			if (array.Length < 2) {
				return;
			}

			var index = Array.IndexOf(array, element);
			if (index >= 0) {
				Offset(array, index, offset, clampOffset);
			}
		}

	}
}
