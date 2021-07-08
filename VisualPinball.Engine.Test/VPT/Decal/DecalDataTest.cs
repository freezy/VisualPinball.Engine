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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Decal
{
	public class DecalDataTest : BaseTests
	{
		[Test]
		public void ShouldReadDecalData()
		{
			var th = FileTableContainer.Load(VpxPath.Decal);
			ValidateDecal0(th.Decal(0).Data);
			ValidateDecal1(th.Decal(1).Data);
		}

		[Test]
		public void ShouldWriteDecalData()
		{
			const string tmpFileName = "ShouldWriteDecalData.vpx";
			var table = FileTableContainer.Load(VpxPath.Decal);
			table.Save(tmpFileName);
			var writtenTable = FileTableContainer.Load(tmpFileName);
			ValidateDecal0(writtenTable.Decal(0).Data);
			ValidateDecal1(writtenTable.Decal(1).Data);
		}

		private static void ValidateDecal0(DecalData data)
		{
			data.Backglass.Should().Be(false);
			data.Center.X.Should().Be(205.4f);
			data.Center.Y.Should().Be(540.68f);
			data.Color.Red.Should().Be(60);
			data.Color.Green.Should().Be(217);
			data.Color.Blue.Should().Be(142);
			data.DecalType.Should().Be(DecalType.DecalImage);
			data.Font.Name.Should().Be("Arial Black");
			data.Height.Should().Be(32.5f);
			data.Image.Should().Be("tex_transparent");
			data.Material.Should().Be("DecalMat");
			data.Rotation.Should().Be(45.98f);
			data.SizingType.Should().Be(SizingType.AutoWidth);
			data.Surface.Should().Be("");
			data.Text.Should().Be("");
			data.VerticalText.Should().Be(false);
			data.Width.Should().Be(66.1165f);
			data.EditorLayer.Should().Be(2);
			data.EditorLayerName.Should().Be("Layer_3");
			data.EditorLayerVisibility.Should().Be(true);
			data.IsLocked.Should().Be(false);
		}

		private static void ValidateDecal1(DecalData data)
		{
			data.Backglass.Should().Be(true);
			data.Center.X.Should().Be(509f);
			data.Center.Y.Should().Be(354f);
			data.Color.Red.Should().Be(216);
			data.Color.Green.Should().Be(63);
			data.Color.Blue.Should().Be(204);
			data.DecalType.Should().Be(DecalType.DecalText);
			data.Font.Name.Should().Be("Fixedsys");
			data.Height.Should().Be(100f);
			data.Image.Should().Be("");
			data.Material.Should().Be("");
			data.Rotation.Should().Be(0f);
			data.SizingType.Should().Be(SizingType.ManualSize);
			data.Surface.Should().Be("");
			data.Text.Should().Be("My Decal Text");
			data.VerticalText.Should().Be(true);
			data.Width.Should().Be(100f);
			data.EditorLayer.Should().Be(0);
			data.EditorLayerName.Should().Be("Layer_1");
			data.EditorLayerVisibility.Should().Be(true);
			data.IsLocked.Should().Be(true);
		}
	}
}
