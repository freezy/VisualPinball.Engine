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
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Test.VPT
{
	public class MaterialDataTests
	{
		[Test]
		public void ShouldReadMaterialData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Material);
			ValidateMaterial1(table.GetMaterial("Material1"));
		}

		[Test]
		public void ShouldWriteMaterialData()
		{
			const string tmpFileName = "ShouldWriteMaterialData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Material);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateMaterial1(writtenTable.GetMaterial("Material1"));
		}

		[Test]
		public void ShouldWriteUpdatedMaterialData()
		{
			const string tmpFileName = "ShouldWriteUpdatedMaterialData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Material);

			var mat = table.GetMaterial("Material1");
			mat.Name = "MaterialUpdated";
			mat.BaseColor = new Color(1, 2, 3, 1);
			mat.Edge = 0.15f;
			mat.EdgeAlpha = 0.84f;
			mat.Elasticity = 2.34f;
			mat.ElasticityFalloff = 4.02f;
			mat.Friction = 6.32f;
			mat.GlossyImageLerp = 0.23f;
			mat.IsMetal = true;
			mat.IsOpacityActive = false;
			mat.Opacity = 0.32f;
			mat.Roughness = 0.87f;
			mat.ScatterAngle = 12.2f;
			mat.Thickness = 0.74f;
			mat.WrapLighting = 0.68f;

			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			var material = writtenTable.GetMaterial("MaterialUpdated");
			material.Name.Should().Be("MaterialUpdated");
			material.BaseColor.Red.Should().Be(1);
			material.BaseColor.Green.Should().Be(2);
			material.BaseColor.Blue.Should().Be(3);
			material.Edge.Should().Be(0.15f);
			material.EdgeAlpha.Should().BeApproximately(0.84f, 0.003f);
			material.Elasticity.Should().Be(2.34f);
			material.ElasticityFalloff.Should().Be(4.02f);
			material.Friction.Should().Be(6.32f);
			material.GlossyImageLerp.Should().BeApproximately(0.23f, 0.003f);
			material.IsMetal.Should().Be(true);
			material.IsOpacityActive.Should().Be(false);
			material.Opacity.Should().Be(0.32f);
			material.Roughness.Should().Be(0.87f);
			material.ScatterAngle.Should().Be(12.2f);
			material.Thickness.Should().BeApproximately(0.74f, 0.003f);
			material.WrapLighting.Should().Be(0.68f);
		}

		private void ValidateMaterial1(Material material)
		{
			material.Name.Should().Be("Material1");
			material.BaseColor.Red.Should().Be(153);
			material.BaseColor.Green.Should().Be(62);
			material.BaseColor.Blue.Should().Be(255);
			material.ClearCoat.Red.Should().Be(50);
			material.ClearCoat.Green.Should().Be(51);
			material.ClearCoat.Blue.Should().Be(52);
			material.Edge.Should().Be(0.3874f);
			material.EdgeAlpha.Should().BeApproximately(0.1968504f, 0.00001f);
			material.Elasticity.Should().Be(1.1f);
			material.ElasticityFalloff.Should().Be(2.02f);
			material.Friction.Should().Be(2.322f);
			material.Glossiness.Red.Should().Be(255);
			material.Glossiness.Green.Should().Be(158);
			material.Glossiness.Blue.Should().Be(70);
			material.GlossyImageLerp.Should().BeApproximately(0.4078431f, 0.00001f);
			material.IsMetal.Should().Be(false);
			material.IsOpacityActive.Should().Be(true);
			material.Opacity.Should().Be(0.8183f);
			material.Roughness.Should().Be(0.53628f);
			material.ScatterAngle.Should().Be(7.4332f);
			material.Thickness.Should().Be(0.654902f);
			material.WrapLighting.Should().Be(0.5492f);
		}
	}
}
