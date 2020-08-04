using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Engine.Test.VPT.Bumper
{
	public class BumperDataTests
	{
		[Test]
		public void ShouldReadBumperData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Bumper);
			var data = table.Bumper("Bumper1").Data;
			ValidateTableData(data);
		}

		[Test]
		public void ShouldWriteBumperData()
		{
			const string tmpFileName = "ShouldWriteBumperData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Bumper);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTableData(writtenTable.Bumper("Bumper1").Data);
		}

		private static void ValidateTableData(BumperData data)
		{
			data.BaseMaterial.Should().Be("Material2");
			data.CapMaterial.Should().Be("Material1");
			data.Center.X.Should().Be(500f);
			data.Center.Y.Should().Be(1250f);
			data.Force.Should().Be(12.2234f);
			data.HeightScale.Should().Be(80.654f);
			data.HitEvent.Should().Be(true);
			data.IsBaseVisible.Should().Be(true);
			data.IsCapVisible.Should().Be(false);
			data.IsCollidable.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(false);
			data.IsRingVisible.Should().Be(true);
			data.IsSocketVisible.Should().Be(true);
			data.Orientation.Should().Be(9.17826f);
			data.Radius.Should().Be(30.38182f);
			data.RingDropOffset.Should().Be(0.005561f);
			data.RingMaterial.Should().Be("Material4");
			data.RingSpeed.Should().Be(0.52098f);
			data.Scatter.Should().Be(0.0068f);
			data.SocketMaterial.Should().Be("Material3");
			data.Surface.Should().Be("");
			data.Threshold.Should().Be(1.00658f);


			data.TimerInterval.Should().Be(100);
			data.IsTimerEnabled.Should().Be(false);

			data.EditorLayer.Should().Be(0);
			data.EditorLayerName.Should().Be(string.Empty);
			data.EditorLayerVisibility.Should().Be(true);
			data.IsLocked.Should().Be(false);
		}
	}
}
