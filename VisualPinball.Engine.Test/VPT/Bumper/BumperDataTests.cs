using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Bumper
{
	public class BumperDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\BumperData.vpx");
			var data = table.Data;
		}
	}
}
