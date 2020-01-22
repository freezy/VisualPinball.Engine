using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Spinner
{
	public class SpinnerDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Spinner);
			var data = table.Spinners["Data"].Data;

			Assert.Equal(50.698f, data.AngleMax);
			Assert.Equal(-12.87f, data.AngleMin);
			Assert.Equal(494f, data.Center.X);
			Assert.Equal(1401.62f, data.Center.Y);
			Assert.Equal(0.6824f, data.Elasticity);
			Assert.Equal(13.532f, data.Height);
			Assert.Equal("", data.Image);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal(124.31f, data.Length);
			Assert.Equal("Red", data.Material);
			Assert.Equal(47.98f, data.Rotation);
			Assert.Equal(true, data.ShowBracket);
			Assert.Equal("", data.Surface);
		}
	}
}
