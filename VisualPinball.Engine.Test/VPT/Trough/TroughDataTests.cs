// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Engine.Test.VPT.Trough
{
	public class TroughDataTests
	{
		[Test]
		public void ShouldReadTroughData()
		{
			var table = TableContainer.Load(VpxPath.Trough);
			ValidateTroughData(table.Trough("Trough1").Data);
		}

		[Test]
		public void ShouldWriteTroughData()
		{
			const string tmpFileName = "ShouldWriteTroughData.vpx";
			var table = TableContainer.Load(VpxPath.Trough);
			table.Save(tmpFileName);
			var writtenTable = TableContainer.Load(tmpFileName);
			ValidateTroughData(writtenTable.Trough("Trough1").Data);
		}

		private static void ValidateTroughData(TroughData data)
		{
			data.Type.Should().Be(TroughType.ModernOpto);
			data.BallCount.Should().Be(3);
			data.SwitchCount.Should().Be(4);
			data.KickTime.Should().Be(112);
			data.RollTime.Should().Be(113);
			data.TransitionTime.Should().Be(114);
			data.JamSwitch.Should().Be(true);
			data.PlayfieldEntrySwitch.Should().Be("BallDrain");
			data.PlayfieldExitKicker.Should().Be("BallRelease");
		}
	}
}
