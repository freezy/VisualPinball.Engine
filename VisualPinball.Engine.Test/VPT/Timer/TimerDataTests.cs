using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Timer;

namespace VisualPinball.Engine.Test.VPT.Timer
{
	public class TimerDataTests
	{
		[Test]
		public void ShouldReadTimerData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Timer);
			ValidateTimerData1(table.Timer("Timer1").Data);
			ValidateTimerData2(table.Timer("Timer2").Data);
		}

		[Test]
		public void ShouldWriteTimerData()
		{
			const string tmpFileName = "ShouldWriteTimerData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Timer);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTimerData1(writtenTable.Timer("Timer1").Data);
			ValidateTimerData2(writtenTable.Timer("Timer2").Data);
		}

		private static void ValidateTimerData1(TimerData data)
		{
			data.Backglass.Should().Be(false);
			data.Center.X.Should().Be(471.160583f);
			data.Center.Y.Should().Be(628.259277f);
			data.IsTimerEnabled.Should().Be(true);
			data.TimerInterval.Should().Be(233);
		}

		private static void ValidateTimerData2(TimerData data)
		{
			data.Backglass.Should().Be(true);
		}
	}
}
