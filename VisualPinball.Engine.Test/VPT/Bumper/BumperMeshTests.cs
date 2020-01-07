using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Bumper
{
	public class BumperMeshTests
	{
		[Fact]
		public void ShouldGenerateAMesh()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\BumperData.vpx");
			var bumper = table.Bumpers["Bumper1"];
			var ros = bumper.GetRenderObjects(table);
		}
	}
}
