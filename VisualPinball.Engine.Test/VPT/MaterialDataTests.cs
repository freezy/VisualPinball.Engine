using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT
{
	public class MaterialDataTests
	{
		[Test]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Material);
			var material = table.GetMaterial("Material1");

			material.Name.Should().Be("Material1");
			material.BaseColor.Red.Should().Be(153);
			material.BaseColor.Green.Should().Be(62);
			material.BaseColor.Blue.Should().Be(255);
			material.ClearCoat.Red.Should().Be(50);
			material.ClearCoat.Green.Should().Be(51);
			material.ClearCoat.Blue.Should().Be(52);
			material.Edge.Should().Be(0.3874f);
			//material.EdgeAlpha.Should().Be(0.1968504f);
			material.EdgeAlpha.Should().BeInRange(0.1968503f, 0.1968505f); // comment out previous and watch what happens..
			material.Elasticity.Should().Be(1.1f);
			material.ElasticityFalloff.Should().Be(2.02f);
			material.Friction.Should().Be(2.322f);
			material.Glossiness.Red.Should().Be(255);
			material.Glossiness.Green.Should().Be(158);
			material.Glossiness.Blue.Should().Be(70);
			//material.GlossyImageLerp.Should().Be(0.4078431f);
			material.GlossyImageLerp.Should().BeInRange(0.4078430f, 0.4078432f);
			material.IsMetal.Should().Be(false);
			material.IsOpacityActive.Should().Be(true);
			material.Opacity.Should().Be(0.8183f);
			material.Roughness.Should().Be(0.53628f);
			material.ScatterAngle.Should().Be(7.4332f);
			//material.Thickness.Should().Be(0.04705882f);
			material.Thickness.Should().BeInRange(0.04705881f, 0.04705883f);
			material.WrapLighting.Should().Be(0.5492f);
		}
	}
}
