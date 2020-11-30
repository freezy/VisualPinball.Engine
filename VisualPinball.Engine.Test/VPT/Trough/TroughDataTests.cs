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
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Engine.Test.VPT.Trough
{
	public class TroughDataTests
	{
		[Test]
		public void ShouldReadTroughData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Trough);
			ValidateTroughData(table.Trough("Trough1").Data);
		}

		[Test]
		public void ShouldWriteTroughData()
		{
			const string tmpFileName = "ShouldWriteTroughData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Trough);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTroughData(writtenTable.Trough("Trough1").Data);
		}

		private static void ValidateTroughData(TroughData data)
		{
			data.BallCount.Should().Be(3);
			data.SwitchCount.Should().Be(4);
			data.SettleTime.Should().Be(112);
			data.EntryKicker.Should().Be("BallDrain");
			data.ExitKicker.Should().Be("BallRelease");
			data.JamSwitch.Should().Be("TroughJam");
		}
	}
}
