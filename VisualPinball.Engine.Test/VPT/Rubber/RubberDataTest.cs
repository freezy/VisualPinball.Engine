using Xunit;

namespace VisualPinball.Engine.Test.VPT.Rubber
{
	public class RubberDataTest
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\RubberData.vpx");
			var data = table.Rubbers["Rubber1"].Data;

			table.Rubbers["Rubber1"].GetRenderObjects(table);

			Assert.Equal(3, data.DragPoints.Length);
		}
	}
}
