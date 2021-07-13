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

using System.IO;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Rubber
{
	public class RubberDataTests
	{
		[Test]
		public void ShouldReadRubberData()
		{
			var table = FileTableContainer.Load(VpxPath.Rubber);
			ValidateRubberData1(table.Rubber("Rubber1").Data);
			ValidateRubberData2(table.Rubber("Rubber2").Data);
		}

		[Test]
		public void ShouldWriteRubberData()
		{
			const string tmpFileName = "ShouldWriteRubberData.vpx";
			var table = FileTableContainer.Load(VpxPath.Rubber);
			table.Save(tmpFileName);
			var writtenTable = FileTableContainer.Load(tmpFileName);
			ValidateRubberData1(writtenTable.Rubber("Rubber1").Data);
			ValidateRubberData2(writtenTable.Rubber("Rubber2").Data);
			File.Delete(tmpFileName);
		}

		public static void ValidateRubberData1(RubberData data)
		{
			data.DragPoints.Length.Should().Be(3);
			data.Elasticity.Should().Be(0.832f);
			data.ElasticityFalloff.Should().Be(0.321f);
			data.Friction.Should().Be(0.685f);
			data.Height.Should().Be(25.556f);
			data.HitEvent.Should().Be(false);
			data.HitHeight.Should().Be(25.193f);
			data.Image.Should().Be("test_pattern");
			data.IsCollidable.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.Material.Should().Be("Playfield");
			data.OverwritePhysics.Should().Be(true);
			data.PhysicsMaterial.Should().Be("");
			data.RotX.Should().Be(65.23f);
			data.RotY.Should().Be(75.273f);
			data.RotZ.Should().Be(70.962f);
			data.Scatter.Should().Be(5.225f);
			data.ShowInEditor.Should().Be(false);
			data.StaticRendering.Should().Be(true);
			data.Thickness.Should().Be(12);
			data.Points.Should().Be(true);
		}

		public static void ValidateRubberData2(RubberData data)
		{
			data.DragPoints.Length.Should().Be(3);
			data.Elasticity.Should().Be(0.8f);
			data.ElasticityFalloff.Should().Be(0.3f);
			data.Friction.Should().Be(0.6f);
			data.Height.Should().Be(25f);
			data.HitEvent.Should().Be(false);
			data.HitHeight.Should().Be(25f);
			data.Image.Should().Be("");
			data.IsCollidable.Should().Be(false);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.Material.Should().Be("");
			data.OverwritePhysics.Should().Be(true);
			data.PhysicsMaterial.Should().Be("");
			data.RotX.Should().Be(0f);
			data.RotY.Should().Be(0f);
			data.RotZ.Should().Be(0f);
			data.Scatter.Should().Be(5f);
			data.ShowInEditor.Should().Be(false);
			data.StaticRendering.Should().Be(false);
			data.Thickness.Should().Be(8);
		}
	}
}
