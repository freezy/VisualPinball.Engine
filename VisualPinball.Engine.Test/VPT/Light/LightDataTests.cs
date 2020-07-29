using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;

namespace VisualPinball.Engine.Test.VPT.Light
{
	public class LightDataTests
	{
		[Test]
		public void ShouldReadLightData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Light);
			ValidateLightData(table.Lights["Light1"].Data);
		}

		[Test]
		public void ShouldWriteLightData()
		{
			const string tmpFileName = "ShouldWriteLightData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Light);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateLightData(writtenTable.Lights["Light1"].Data);
		}

		private static void ValidateLightData(LightData data)
		{
			data.BlinkInterval.Should().Be(126);
			data.BlinkPattern.Should().Be("10011");
			data.BulbHaloHeight.Should().Be(28.298f);
			data.BulbModulateVsAdd.Should().Be(0.9723f);
			data.Center.X.Should().Be(450.7777f);
			data.Center.Y.Should().Be(109.552f);
			data.Color.Red.Should().Be(151);
			data.Color.Green.Should().Be(221);
			data.Color.Blue.Should().Be(34);
			data.Color2.Red.Should().Be(235);
			data.Color2.Green.Should().Be(50);
			data.Color2.Blue.Should().Be(193);
			data.DepthBias.Should().Be(0.0012f);
			data.DragPoints.Length.Should().Be(8);
			data.FadeSpeedDown.Should().Be(0.223f);
			data.FadeSpeedUp.Should().Be(0.265f);
			data.FalloffPower.Should().Be(2.021f);
			data.Intensity.Should().Be(1.293f);
			data.IsBackglass.Should().Be(false);
			data.IsBulbLight.Should().Be(true);
			data.IsImageMode.Should().Be(false);
			data.IsRoundLight.Should().Be(false);
			data.MeshRadius.Should().Be(20.231f);
			data.OffImage.Should().Be("smiley");
			data.ShowBulbMesh.Should().Be(false);
			data.ShowReflectionOnBall.Should().Be(true);
			data.State.Should().Be(LightStatus.LightStateOff);
			data.Surface.Should().Be("");
			data.TransmissionScale.Should().Be(0.5916f);
		}

		[Test]
		public void ShouldLoadCorrectDragPointData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Light);
			var dragPoints = table.Lights["PlayfieldLight"].Data.DragPoints;

			dragPoints[0].IsSmooth.Should().Be(false);
			dragPoints[0].IsSlingshot.Should().Be(true);
			dragPoints[0].Center.X.Should().Be(491.6666f);
			dragPoints[0].Center.Y.Should().Be(376.882f);
			dragPoints[6].IsSmooth.Should().Be(true);
			dragPoints[7].IsSmooth.Should().Be(false);
		}
	}
}
