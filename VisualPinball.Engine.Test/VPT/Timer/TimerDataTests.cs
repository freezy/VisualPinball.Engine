// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

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
