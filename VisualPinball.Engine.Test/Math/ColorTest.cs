using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.Test.Math
{
	public class TableDataTests
	{
		[Test]
		public void ShouldCorrectlyParseRgbColor()
		{
			var color = new Color(0x123456, ColorFormat.Bgr);
			color.Red.Should().Be(0x56);
			color.Green.Should().Be(0x34);
			color.Blue.Should().Be(0x12);
		}

		[Test]
		public void ShouldCorrectlyParseArgbColor()
		{
			var color = new Color(0x12345678, ColorFormat.Argb);
			color.Red.Should().Be(0x34);
			color.Green.Should().Be(0x56);
			color.Blue.Should().Be(0x78);
			color.Alpha.Should().Be(0x12);
		}
	}
}
