using VisualPinball.Engine.Math;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Table
{
	public class TableDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\VPX\TableData.vpx");
			var data = table.Data;

			Assert.Equal(2224, data.Bottom);
			Assert.Equal("Option Explicit\r\n", data.Code);
			Assert.Equal(0.23f, data.Elasticity);
			Assert.Equal(0.076f, data.Friction);
			Assert.Equal(0.8f, data.Gravity * (float) (1.0 / Constants.Gravity));
			Assert.Equal("test_pattern", data.Image);
			Assert.Equal(0f, data.Left); // set in debug mode
			Assert.Equal(1112f, data.Right);
			Assert.Equal(2.02f, data.Scatter);
			Assert.Equal(0f, data.Top); // set in debug mode
			Assert.Equal(0.5f, data.Zoom); // set in debug mode
			Assert.Equal(0.015f, data._3DmaxSeparation); // ???
			Assert.Equal(0f, data._3DOffset); // ???
			Assert.Equal(0.1f, data._3DZPD); // ???
			Assert.Equal(0.2033f, data.AngletiltMin);
			Assert.Equal(1.23f, data.AoScale);
			Assert.Equal(1.5055f, data.BloomStrength);
			Assert.Equal(35, data.ColorBackdrop.Red); // ???

			Assert.Equal(255, data.Light[0].Emission.Red);
			Assert.Equal(255, data.Light[0].Emission.Green);
			Assert.Equal(17, data.Light[0].Emission.Blue);
			Assert.Equal("Material1", data.Materials[0].Name);
			Assert.Equal(556f, data.Offset[0]); // set in debug mode
			Assert.Equal(1112f, data.Offset[1]); // set in debug mode
		}
	}
}
