using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.DispReel;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.DispReel
{
	public class DispReelDataTest : BaseTests
	{
		public DispReelDataTest(ITestOutputHelper output) : base(output) { }

		[Fact]
		public void ShouldReadDispReelData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.DispReel);
			ValidateDispReel1(table.DispReels["Reel1"].Data);
			ValidateDispReel2(table.DispReels["Reel2"].Data);
		}

		[Fact]
		public void ShouldWriteDispReelData()
		{
			const string tmpFileName = "ShouldWriteDispReelData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.DispReel);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateDispReel1(writtenTable.DispReels["Reel1"].Data);
			ValidateDispReel2(writtenTable.DispReels["Reel2"].Data);
		}

		private static void ValidateDispReel1(DispReelData data)
		{
			Assert.Equal(204, data.BackColor.Red);
			Assert.Equal(149, data.BackColor.Green);
			Assert.Equal(19, data.BackColor.Blue);
			Assert.Equal(3, data.DigitRange);
			Assert.Equal(6, data.EditorLayer);
			Assert.Equal(42, data.Height);
			Assert.Equal("tex_transparent", data.Image);
			Assert.Equal(3, data.ImagesPerGridRow);
			Assert.Equal(true, data.IsLocked);
			Assert.Equal(true, data.IsTimerEnabled);
			Assert.Equal(true, data.IsTransparent);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal(32, data.MotorSteps);
			Assert.Equal(32, data.ReelCount);
			Assert.Equal(8, data.ReelSpacing);
			Assert.Equal("", data.Sound);
			Assert.Equal(100, data.TimerInterval);
			Assert.Equal(12, data.UpdateInterval);
			Assert.Equal(true, data.UseImageGrid);
			Assert.Equal(3.2f, data.V1.X);
			Assert.Equal(151.6f, data.V1.Y);
			Assert.Equal(data.V1.X + data.BoxWidth, data.V2.X);
			Assert.Equal(data.V1.Y + data.BoxHeight, data.V2.Y);
			Assert.Equal(12, data.Width);
			Assert.Equal(true, data.IsTimerEnabled);
		}

		private static void ValidateDispReel2(DispReelData data)
		{
			Assert.Equal(0, data.BackColor.Red);
			Assert.Equal(0, data.BackColor.Green);
			Assert.Equal(255, data.BackColor.Blue);
			Assert.Equal(9, data.DigitRange);
			Assert.Equal(0, data.EditorLayer);
			Assert.Equal(40, data.Height);
			Assert.Equal("", data.Image);
			Assert.Equal(1, data.ImagesPerGridRow);
			Assert.Equal(false, data.IsLocked);
			Assert.Equal(false, data.IsTimerEnabled);
			Assert.Equal(false, data.IsTransparent);
			Assert.Equal(false, data.IsVisible);
			Assert.Equal(2, data.MotorSteps);
			Assert.Equal(5, data.ReelCount);
			Assert.Equal(4, data.ReelSpacing);
			Assert.Equal("", data.Sound);
			Assert.Equal(100, data.TimerInterval);
			Assert.Equal(50, data.UpdateInterval);
			Assert.Equal(false, data.UseImageGrid);
			Assert.Equal(445f, data.V1.X);
			Assert.Equal(341f, data.V1.Y);
			Assert.Equal(data.V1.X + data.BoxWidth, data.V2.X);
			Assert.Equal(data.V1.Y + data.BoxHeight, data.V2.Y);
			Assert.Equal(30, data.Width);
		}
	}
}
