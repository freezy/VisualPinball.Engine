using VisualPinball.Engine.Math;
using Xunit;

namespace VisualPinball.Engine.Test.Math
{
	public class TableDataTests
	{
		[Fact]
		public void ShouldCorrectlyParseRgbColor()
		{
			var color = new Color(0x123456, ColorFormat.Bgr);
			Assert.Equal(0x56, color.Red);
			Assert.Equal(0x34, color.Green);
			Assert.Equal(0x12, color.Blue);
		}

		[Fact]
		public void ShouldCorrectlyParseArgbColor()
		{
			var color = new Color(0x12345678, ColorFormat.Argb);
			Assert.Equal(0x34, color.Red);
			Assert.Equal(0x56, color.Green);
			Assert.Equal(0x78, color.Blue);
			Assert.Equal(0x12, color.Alpha);
		}
	}
}
