using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Flasher
{
	public class FlasherDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flasher);
			var data = table.Flashers["Data"].Data;

			Assert.Equal(false, data.AddBlend);
			Assert.Equal(69, data.Alpha);
			Assert.Equal(383f, data.Center.X);
			Assert.Equal(785f, data.Center.Y);
			Assert.Equal(64, data.Color.Red);
			Assert.Equal(153, data.Color.Green);
			Assert.Equal(225, data.Color.Blue);
			Assert.Equal(0.282f, data.DepthBias);
			Assert.Equal(false, data.DisplayTexture);
			Assert.Equal(Filters.Filter_Overlay, data.Filter);
			Assert.Equal(100, data.FilterAmount);
			Assert.Equal(50.22f, data.Height);
			Assert.Equal("", data.ImageA);
			Assert.Equal(ImageAlignment.ImageAlignTopLeft, data.ImageAlignment);
			Assert.Equal("", data.ImageB);
			Assert.Equal(false, data.IsDMD);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal(0.921f, data.ModulateVsAdd);
			Assert.Equal(15.651f, data.RotX);
			Assert.Equal(32.918f, data.RotY);
			Assert.Equal(14.32f, data.RotZ);
		}
	}
}
