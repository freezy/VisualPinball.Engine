// Visual Pinball Engine
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
using VisualPinball.Engine.VPT.MetalWireGuide;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.MetalWireGuide
{
	public class MetalWireGuideDataTests
	{
		[Test]
		public void ShouldReadMetalWireGuideData()
		{
			var table = FileTableContainer.Load(VpxPath.MetalWireGuide);
			ValidateMetalWireGuideData1(table.MetalWireGuide("MetalWireGuide1").Data);
			ValidateMetalWireGuideData2(table.MetalWireGuide("MetalWireGuide2").Data);
		}

		[Test]
		public void ShouldWriteMetalWireGuideData()
		{
			const string tmpFileName = "ShouldWriteMetalWireGuideData.vpx";
			var table = FileTableContainer.Load(VpxPath.MetalWireGuide);
			table.Export(tmpFileName);
			var writtenTable = FileTableContainer.Load(tmpFileName);
			ValidateMetalWireGuideData1(writtenTable.MetalWireGuide("MetalWireGuide1").Data);
			ValidateMetalWireGuideData2(writtenTable.MetalWireGuide("MetalWireGuide2").Data);
			File.Delete(tmpFileName);
		}

		public static void ValidateMetalWireGuideData1(MetalWireGuideData data)
		{
			data.DragPoints.Length.Should().Be(2);
			data.Elasticity.Should().Be(0.99f);
			data.ElasticityFalloff.Should().Be(0.33f);
			data.Friction.Should().Be(0.55f);
			data.Height.Should().Be(25f);
			data.HitEvent.Should().Be(false);
			data.HitHeight.Should().Be(25);
			// i dont know where to set this...
			data.Image.Should().Be("");
			data.IsCollidable.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.Material.Should().Be("");
			data.OverwritePhysics.Should().Be(true);
			data.PhysicsMaterial.Should().Be("");
			data.RotX.Should().Be(0f);
			data.RotY.Should().Be(0f);
			data.RotZ.Should().Be(0f);
			data.Scatter.Should().Be(0f);
			data.ShowInEditor.Should().Be(true);
			data.StaticRendering.Should().Be(true);
			data.Thickness.Should().Be(3f);
			data.Points.Should().Be(true);
		}

		public static void ValidateMetalWireGuideData2(MetalWireGuideData data)
		{
			data.DragPoints.Length.Should().Be(3);
			data.Elasticity.Should().Be(0.25f);
			data.ElasticityFalloff.Should().Be(0.27f);
			data.Friction.Should().Be(0.42f);
			data.Height.Should().Be(30.2f);
			data.HitEvent.Should().Be(false);
			data.HitHeight.Should().Be(25.3f);
			data.Image.Should().Be("");
			data.IsCollidable.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsVisible.Should().Be(false);
			data.Material.Should().Be("");
			data.OverwritePhysics.Should().Be(true);
			data.PhysicsMaterial.Should().Be("");
			data.RotX.Should().Be(1.57f);
			data.RotY.Should().Be(1.73f);
			data.RotZ.Should().Be(1.61f);
			data.Scatter.Should().Be(5f);
			data.ShowInEditor.Should().Be(true);
			data.StaticRendering.Should().Be(true);
			data.Thickness.Should().Be(2.5f);
		}
	}
}
