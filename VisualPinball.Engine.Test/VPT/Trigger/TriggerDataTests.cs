using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Trigger;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Trigger
{
	public class TriggerDataTests
	{
		[Fact]
		public void ShouldReadTriggerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Trigger);
			ValidateTriggerData(table.Triggers["Data"].Data);
		}

		[Fact]
		public void ShouldWriteTriggerData()
		{
			const string tmpFileName = "ShouldWriteTriggerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Trigger);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTriggerData(writtenTable.Triggers["Data"].Data);
		}

		private static void ValidateTriggerData(TriggerData data)
		{
			Assert.Equal(12.432f, data.AnimSpeed);
			Assert.Equal(542.732f, data.Center.X);
			Assert.Equal(1875.182f, data.Center.Y);
			Assert.Equal(4, data.DragPoints.Length);
			Assert.Equal(52.668f, data.HitHeight);
			Assert.Equal(true, data.IsEnabled);
			Assert.Equal(true, data.IsReflectionEnabled);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal("Red", data.Material);
			Assert.Equal(25f, data.Radius);
			Assert.Equal(66.94f, data.Rotation);
			Assert.Equal(1f, data.ScaleX);
			Assert.Equal(1f, data.ScaleY);
			Assert.Equal(TriggerShape.TriggerWireC, data.Shape);
			Assert.Equal("", data.Surface);
			Assert.Equal(3.628f, data.WireThickness);
		}
	}
}
