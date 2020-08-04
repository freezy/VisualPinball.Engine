using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Engine.Test.VPT.Plunger
{
	public class PlungerDataTests
	{
		[Test]
		public void ShouldReadPlungerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Plunger);
			ValidatePlungerData1(table.Plunger("Plunger1").Data);
			ValidatePlungerData2(table.Plunger("Plunger2").Data);
		}

		[Test]
		public void ShouldWritePlungerData()
		{
			const string tmpFileName = "ShouldWritePlungerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Plunger);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidatePlungerData1(writtenTable.Plunger("Plunger1").Data);
			ValidatePlungerData2(writtenTable.Plunger("Plunger2").Data);
		}

		private static void ValidatePlungerData1(PlungerData data)
		{
			data.AnimFrames.Should().Be(7);
			data.AnimFrames.Should().Be(7);
			data.AutoPlunger.Should().Be(true);
			data.Center.X.Should().Be(477f);
			data.Center.Y.Should().Be(983.2f);
			data.Height.Should().Be(20f);
			data.Image.Should().Be("alphatest_100_50_0");
			data.IsLocked.Should().Be(true);
			data.IsMechPlunger.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsTimerEnabled.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.Material.Should().Be("PlungerMat");
			data.MechStrength.Should().Be(82.3f);
			data.MomentumXfer.Should().Be(1.231f);
			data.ParkPosition.Should().Be(0.162f);
			data.RingDiam.Should().Be(0.912f);
			data.RingGap.Should().Be(4.665f);
			data.RingWidth.Should().Be(3.223f);
			data.RodDiam.Should().Be(0.3554f);
			data.ScatterVelocity.Should().Be(0.22f);
			data.SpeedFire.Should().Be(80.88f);
			data.SpeedPull.Should().Be(5.238f);
			data.SpringDiam.Should().Be(0.6256f);
			data.SpringEndLoops.Should().Be(2.8836f);
			data.SpringGauge.Should().Be(3.2245f);
			data.SpringLoops.Should().Be(7.882f);
			data.Stroke.Should().Be(78.992f);
			data.Surface.Should().Be("Wall001");
			data.TimerInterval.Should().Be(1332);
			data.TipShape.Should().Be("0 .34; 2 .6; 3 .64; 5 .7; 7 .84; 8 .88; 9 .9; 11 .92; 12 .91; 35 .84");
			data.Type.Should().Be(PlungerType.PlungerTypeFlat);
			data.Width.Should().Be(22.3378f);
			data.ZAdjust.Should().Be(1.223f);
		}

		private static void ValidatePlungerData2(PlungerData data)
		{
			data.AnimFrames.Should().Be(1);
			data.AutoPlunger.Should().Be(false);
			data.IsLocked.Should().Be(false);
			data.IsMechPlunger.Should().Be(false);
			data.IsReflectionEnabled.Should().Be(false);
			data.IsTimerEnabled.Should().Be(false);
			data.IsVisible.Should().Be(false);
			data.Material.Should().Be("");
			data.Surface.Should().Be("");
			data.Type.Should().Be(PlungerType.PlungerTypeModern);
		}
	}
}
