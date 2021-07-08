﻿// Visual Pinball Engine
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
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.DispReel
{
	public class DispReelDataTest : BaseTests
	{
		[Test]
		public void ShouldReadDispReelData()
		{
			var table = TableHolder.Load(VpxPath.DispReel);
			ValidateDispReel1(table.DispReel("Reel1").Data);
			ValidateDispReel2(table.DispReel("Reel2").Data);
		}

		[Test]
		public void ShouldWriteDispReelData()
		{
			const string tmpFileName = "ShouldWriteDispReelData.vpx";
			var table = TableHolder.Load(VpxPath.DispReel);
			table.Save(tmpFileName);
			var writtenTable = TableHolder.Load(tmpFileName);
			ValidateDispReel1(writtenTable.DispReel("Reel1").Data);
			ValidateDispReel2(writtenTable.DispReel("Reel2").Data);
		}

		private static void ValidateDispReel1(DispReelData data)
		{
			data.BackColor.Red.Should().Be(204);
			data.BackColor.Green.Should().Be(149);
			data.BackColor.Blue.Should().Be(19);
			data.DigitRange.Should().Be(3);
			data.EditorLayer.Should().Be(6);
			data.EditorLayerName.Should().Be("Layer_7");
			data.EditorLayerVisibility.Should().Be(true);
			data.Height.Should().Be(42);
			data.Image.Should().Be("tex_transparent");
			data.ImagesPerGridRow.Should().Be(3);
			data.IsLocked.Should().Be(true);
			data.IsTimerEnabled.Should().Be(true);
			data.IsTransparent.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.MotorSteps.Should().Be(32);
			data.ReelCount.Should().Be(32);
			data.ReelSpacing.Should().Be(8);
			data.Sound.Should().Be("");
			data.TimerInterval.Should().Be(100);
			data.UpdateInterval.Should().Be(12);
			data.UseImageGrid.Should().Be(true);
			data.V1.X.Should().Be(3.2f);
			data.V1.Y.Should().Be(151.6f);
			data.V2.X.Should().Be(data.V1.X + data.BoxWidth);
			data.V2.Y.Should().Be(data.V1.Y + data.BoxHeight);
			data.Width.Should().Be(12);
			data.IsTimerEnabled.Should().Be(true);
		}

		private static void ValidateDispReel2(DispReelData data)
		{
			data.BackColor.Red.Should().Be(0);
			data.BackColor.Green.Should().Be(0);
			data.BackColor.Blue.Should().Be(255);
			data.DigitRange.Should().Be(9);
			data.EditorLayer.Should().Be(0);
			data.EditorLayerName.Should().Be("Layer_1");
			data.EditorLayerVisibility.Should().Be(true);
			data.Height.Should().Be(40);
			data.Image.Should().Be("");
			data.ImagesPerGridRow.Should().Be(1);
			data.IsLocked.Should().Be(false);
			data.IsTimerEnabled.Should().Be(false);
			data.IsTransparent.Should().Be(false);
			data.IsVisible.Should().Be(false);
			data.MotorSteps.Should().Be(2);
			data.ReelCount.Should().Be(5);
			data.ReelSpacing.Should().Be(4);
			data.Sound.Should().Be("");
			data.TimerInterval.Should().Be(100);
			data.UpdateInterval.Should().Be(50);
			data.UseImageGrid.Should().Be(false);
			data.V1.X.Should().Be(445f);
			data.V1.Y.Should().Be(341f);
			data.V2.X.Should().Be(data.V1.X + data.BoxWidth);
			data.V2.Y.Should().Be(data.V1.Y + data.BoxHeight);
			data.Width.Should().Be(30);
		}
	}
}
