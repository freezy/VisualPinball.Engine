using Xunit;

namespace VisualPinball.Engine.Test.VPT.Surface
{
	public class SurfaceDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\SurfaceData.vpx");
			var data = table.Surfaces["Wall"].Data;

			Assert.Equal(0.6985f, data.DisableLightingBelow);
			Assert.Equal(0.1165f, data.DisableLightingTop);
		}
	}
}
