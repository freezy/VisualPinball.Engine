﻿// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using System.IO;
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
		#if !WRITE_VP106 && !WRITE_VP107

		[Test]
		public void ShouldReadTroughData()
		{
			var table = FileTableContainer.Load(VpxPath.Trough);
			ValidateTroughData(table.Trough("Trough1").Data);
		}

		[Test]
		public void ShouldWriteTroughData()
		{
			const string tmpFileName = "ShouldWriteTroughData.vpx";
			var table = FileTableContainer.Load(VpxPath.Trough);
			table.Save(tmpFileName);
			var writtenTable = FileTableContainer.Load(tmpFileName);
			ValidateTroughData(writtenTable.Trough("Trough1").Data);
			File.Delete(tmpFileName);
		}

		public static void ValidateTroughData(TroughData data)
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

		#endif
	}
}
