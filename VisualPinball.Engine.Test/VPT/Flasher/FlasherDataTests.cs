﻿// Visual Pinball Engine
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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flasher;

namespace VisualPinball.Engine.Test.VPT.Flasher
{
	public class FlasherDataTests
	{
		[Test]
		public void ShouldReadFlasherData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flasher);
			ValidateFlasher(table.Flasher("Data").Data);
		}

		[Test]
		public void ShouldWriteFlasherData()
		{
			const string tmpFileName = "ShouldWriteFlasherData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flasher);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateFlasher(writtenTable.Flasher("Data").Data);
		}

		private static void ValidateFlasher(FlasherData data)
		{
			data.AddBlend.Should().Be(false);
			data.Alpha.Should().Be(69);
			data.Center.X.Should().Be(383f);
			data.Center.Y.Should().Be(785f);
			data.Color.Red.Should().Be(64);
			data.Color.Green.Should().Be(153);
			data.Color.Blue.Should().Be(225);
			data.DepthBias.Should().Be(0.282f);
			data.DisplayTexture.Should().Be(false);
			data.DragPoints.Length.Should().Be(4);
			data.DragPoints[0].Center.X.Should().Be(333f);
			data.DragPoints[0].Center.Y.Should().Be(735f);
			data.DragPoints[0].Center.Z.Should().Be(0f);
			data.Filter.Should().Be(Filters.Filter_Overlay);
			data.FilterAmount.Should().Be(100);
			data.Height.Should().Be(50.22f);
			data.ImageA.Should().Be("");
			data.ImageAlignment.Should().Be(ImageAlignment.ImageAlignTopLeft);
			data.ImageB.Should().Be("");
			data.IsDmd.Should().Be(false);
			data.IsVisible.Should().Be(true);
			data.ModulateVsAdd.Should().Be(0.921f);
			data.RotX.Should().Be(15.651f);
			data.RotY.Should().Be(32.918f);
			data.RotZ.Should().Be(14.32f);

			data.TimerInterval.Should().Be(123);
			data.IsTimerEnabled.Should().Be(false);

			data.EditorLayer.Should().Be(10);
			data.EditorLayerName.Should().Be(string.Empty);
			data.EditorLayerVisibility.Should().Be(true);
			data.IsLocked.Should().Be(true);
		}
	}
}
