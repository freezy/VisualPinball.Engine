using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Ramp;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Plunger
{
	public class PlungerDataTests
	{
		[Fact]
		public void ShouldReadPlungerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Plunger);
			ValidatePlungerData1(table.Plungers["Plunger1"].Data);
			ValidatePlungerData2(table.Plungers["Plunger2"].Data);
		}

		[Fact]
		public void ShouldWritePlungerData()
		{
			const string tmpFileName = "ShouldWritePlungerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Plunger);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidatePlungerData1(writtenTable.Plungers["Plunger1"].Data);
			ValidatePlungerData2(writtenTable.Plungers["Plunger2"].Data);
		}

		private static void ValidatePlungerData1(PlungerData data)
		{
			Assert.Equal(7, data.AnimFrames);
			Assert.Equal(true, data.AutoPlunger);
			Assert.Equal(477f, data.Center.X);
			Assert.Equal(983.2f, data.Center.Y);
			Assert.Equal(20f, data.Height);
			Assert.Equal("alphatest_100_50_0", data.Image);
			Assert.Equal(true, data.IsLocked);
			Assert.Equal(true, data.IsMechPlunger);
			Assert.Equal(true, data.IsReflectionEnabled);
			Assert.Equal(true, data.IsTimerEnabled);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal("PlungerMat", data.Material);
			Assert.Equal(82.3f, data.MechStrength);
			Assert.Equal(1.231f, data.MomentumXfer);
			Assert.Equal(0.162f, data.ParkPosition);
			Assert.Equal(0.912f, data.RingDiam);
			Assert.Equal(4.665f, data.RingGap);
			Assert.Equal(3.223f, data.RingWidth);
			Assert.Equal(0.3554f, data.RodDiam);
			Assert.Equal(0.22f, data.ScatterVelocity);
			Assert.Equal(80.88f, data.SpeedFire);
			Assert.Equal(5.238f, data.SpeedPull);
			Assert.Equal(0.6256f, data.SpringDiam);
			Assert.Equal(2.8836f, data.SpringEndLoops);
			Assert.Equal(3.2245f, data.SpringGauge);
			Assert.Equal(7.882f, data.SpringLoops);
			Assert.Equal(78.992f, data.Stroke);
			Assert.Equal("Wall001", data.Surface);
			Assert.Equal(1332, data.TimerInterval);
			Assert.Equal("0 .34; 2 .6; 3 .64; 5 .7; 7 .84; 8 .88; 9 .9; 11 .92; 12 .91; 35 .84", data.TipShape);
			Assert.Equal(PlungerType.PlungerTypeFlat, data.Type);
			Assert.Equal(22.3378f, data.Width);
			Assert.Equal(1.223f, data.ZAdjust);
		}

		private static void ValidatePlungerData2(PlungerData data)
		{
			Assert.Equal(1, data.AnimFrames);
			Assert.Equal(false, data.AutoPlunger);
			Assert.Equal(false, data.IsLocked);
			Assert.Equal(false, data.IsMechPlunger);
			Assert.Equal(false, data.IsReflectionEnabled);
			Assert.Equal(false, data.IsTimerEnabled);
			Assert.Equal(false, data.IsVisible);
			Assert.Equal("", data.Material);
			Assert.Equal("", data.Surface);
			Assert.Equal(PlungerType.PlungerTypeModern, data.Type);
		}
	}
}
