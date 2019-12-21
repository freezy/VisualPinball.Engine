using Xunit;

namespace VisualPinball.Engine.Test.VPT.Table
{
	public class TableDataTests
	{
		[Fact]
		public void ShouldLoadFile()
		{
			// var table = Engine.VPT.Table.Table.Load(@"D:\Pinball\Visual Pinball\Tables\Batman Dark Knight tt&NZ 1.2.vpx");
			var table = Engine.VPT.Table.Table.Load(@"D:\Pinball\Visual Pinball\Tables\Medieval Madness X VPX- NZ&TT 1.1.vpx");
		}
	}
}
