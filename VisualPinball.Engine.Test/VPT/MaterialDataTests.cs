using FluentAssertions;
using NUnit.Framework;
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
