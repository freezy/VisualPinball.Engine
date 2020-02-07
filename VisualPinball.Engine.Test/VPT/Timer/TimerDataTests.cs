using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Timer;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Timer
{
	public class TimerDataTests
	{
		[Fact]
		public void ShouldReadTimerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Timer);
			ValidateTimerData1(table.Timers["Timer1"].Data);
			ValidateTimerData2(table.Timers["Timer2"].Data);
		}

		[Fact]
		public void ShouldWriteTimerData()
		{
			const string tmpFileName = "ShouldWriteTimerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Timer);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTimerData1(writtenTable.Timers["Timer1"].Data);
			ValidateTimerData2(writtenTable.Timers["Timer2"].Data);
		}

		private static void ValidateTimerData1(TimerData data)
		{
			Assert.Equal(false, data.Backglass);
			Assert.Equal(471.160583f, data.Center.X);
			Assert.Equal(628.259277f, data.Center.Y);
			Assert.Equal(true, data.IsTimerEnabled);
			Assert.Equal(233, data.TimerInterval);
		}

		private static void ValidateTimerData2(TimerData data)
		{
			Assert.Equal(true, data.Backglass);
		}
	}
}
