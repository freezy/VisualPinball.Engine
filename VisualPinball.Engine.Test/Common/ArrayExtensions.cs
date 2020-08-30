using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Common;

namespace VisualPinball.Engine.Test.Common
{
	public class ArrayExtensions
	{
		[Test]
		public void ShouldAddResizeAndAddTail()
		{
			int[] array = { 0, 1 };
			VisualPinball.Engine.Common.ArrayExtensions.Add(ref array, 2);
			array.Length.Should().Be(3);
			array[2].Should().Be(2);
		}

		[Test]
		public void ShouldRemoveResizeAndMoveElements()
		{
			int[] array = { 0, 1, 2, 3 };
			VisualPinball.Engine.Common.ArrayExtensions.Remove(ref array, 1);
			array.Length.Should().Be(3);
			array[0].Should().Be(0);
			array[1].Should().Be(2);
			array[2].Should().Be(3);
		}

		[Test]
		public void ShouldRemoveUnorderedResizeAndSwapLast()
		{
			int[] array = { 0, 1, 2, 3 };
			VisualPinball.Engine.Common.ArrayExtensions.RemoveUnordered(ref array, 1);
			array.Length.Should().Be(3);
			array[0].Should().Be(0);
			array[1].Should().Be(3);
			array[2].Should().Be(2);
		}

		[Test]
		public void ShouldRemoveDoNothingIfNotFound()
		{
			int[] array = { 0, 1, 2 };
			VisualPinball.Engine.Common.ArrayExtensions.Remove(ref array, 3);
			array.Length.Should().Be(3);
			array[0].Should().Be(0);
			array[1].Should().Be(1);
			array[2].Should().Be(2);
			VisualPinball.Engine.Common.ArrayExtensions.RemoveUnordered(ref array, 3);
			array.Length.Should().Be(3);
			array[0].Should().Be(0);
			array[1].Should().Be(1);
			array[2].Should().Be(2);
		}

		[Test]
		public void ShouldOffsetDoNothingIfArraySizeOne()
		{
			int[] array = { 0 };
			array.Offset(0, 1, false);
			array[0].Should().Be(0);
			array.Offset(0, -1, false);
			array[0].Should().Be(0);
		}

		[Test]
		public void ShouldOffsetDoNothingIfZero()
		{
			int[] array = { 0, 1 };
			array.Offset(0, 0, false);
			array[0].Should().Be(0);
			array[1].Should().Be(1);
		}

		[Test]
		public void ShouldOffsetMoveOtherElements()
		{
			int[] array = { 0, 1, 2 };
			array.Offset(0, 2, false);
			array[0].Should().Be(1);
			array[1].Should().Be(2);
			array[2].Should().Be(0);
			array.Offset(2, -2, false);
			array[0].Should().Be(0);
			array[1].Should().Be(1);
			array[2].Should().Be(2);
		}

		[Test]
		public void ShouldOffsetClampInsideArray()
		{
			int[] array = { 0, 1, 2 };
			array.Offset(0, 10, true);
			array[0].Should().Be(1);
			array[1].Should().Be(2);
			array[2].Should().Be(0);
			array.Offset(2, -10, true);
			array[0].Should().Be(0);
			array[1].Should().Be(1);
			array[2].Should().Be(2);
		}

		[Test]
		public void ShouldOffsetDoNothingIfNotFound()
		{
			int[] array = { 0, 1, 2 };
			array.OffsetElement(3, 1, true);
			array[0].Should().Be(0);
			array[1].Should().Be(1);
			array[2].Should().Be(2);
		}
	}
}
