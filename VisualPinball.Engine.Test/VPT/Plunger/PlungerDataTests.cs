﻿// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Plunger
{
	public class PlungerDataTests
	{
		[Test]
		public void ShouldReadPlungerData()
		{
			var table = FileTableContainer.Load(VpxPath.Plunger);
			ValidatePlungerData1(table.Plunger("Plunger1").Data);
			ValidatePlungerData2(table.Plunger("Plunger2").Data);
		}

		[Test]
		public void ShouldWritePlungerData()
		{
			const string tmpFileName = "ShouldWritePlungerData.vpx";
			var table = FileTableContainer.Load(VpxPath.Plunger);
			table.Save(tmpFileName);
			var writtenTable = FileTableContainer.Load(tmpFileName);
			ValidatePlungerData1(writtenTable.Plunger("Plunger1").Data);
			ValidatePlungerData2(writtenTable.Plunger("Plunger2").Data);
			File.Delete(tmpFileName);
		}

		public static void ValidatePlungerData1(PlungerData data, bool validateTexture = true)
		{
			data.AnimFrames.Should().Be(7);
			data.AnimFrames.Should().Be(7);
			data.AutoPlunger.Should().Be(true);
			data.Center.X.Should().Be(477f);
			data.Center.Y.Should().Be(983.2f);
			data.Height.Should().Be(20f);
			if (validateTexture) {
				data.Image.Should().Be("alphatest_100_50_0");
			}
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
			data.Type.Should().Be(PlungerType.PlungerTypeCustom); // flat plungers are converted to modern in vpe
			data.Width.Should().Be(22.3378f);
			data.ZAdjust.Should().Be(1.223f);
		}

		public static void ValidatePlungerData2(PlungerData data)
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
