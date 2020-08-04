using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Engine.Test.VPT.Trigger
{
	public class TriggerDataTests
	{
		[Test]
		public void ShouldReadTriggerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Trigger);
			ValidateTriggerData(table.Trigger("Data").Data);
		}

		[Test]
		public void ShouldWriteTriggerData()
		{
			const string tmpFileName = "ShouldWriteTriggerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Trigger);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTriggerData(writtenTable.Trigger("Data").Data);
		}

		private static void ValidateTriggerData(TriggerData data)
		{
			data.AnimSpeed.Should().Be(12.432f);
			data.Center.X.Should().Be(542.732f);
			data.Center.Y.Should().Be(1875.182f);
			data.DragPoints.Length.Should().Be(4);
			data.HitHeight.Should().Be(52.668f);
			data.IsEnabled.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.Material.Should().Be("Red");
			data.Radius.Should().Be(25f);
			data.Rotation.Should().Be(66.94f);
			data.ScaleX.Should().Be(1f);
			data.ScaleY.Should().Be(1f);
			data.Shape.Should().Be(TriggerShape.TriggerWireC);
			data.Surface.Should().Be("");
			data.WireThickness.Should().Be(3.628f);
		}
	}
}
