using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.VPT
{
	public class MaterialDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Material);
			var material = table.GetMaterial("Material1");

			Assert.Equal("Material1", material.Name);
			Assert.Equal(153, material.BaseColor.Red);
			Assert.Equal(62, material.BaseColor.Green);
			Assert.Equal(255, material.BaseColor.Blue);
			Assert.Equal(50, material.ClearCoat.Red);
			Assert.Equal(51, material.ClearCoat.Green);
			Assert.Equal(52, material.ClearCoat.Blue);
			Assert.Equal(0.3874f, material.Edge);
			//Assert.Equal(0.1968504f, material.EdgeAlpha);
			Assert.InRange(material.EdgeAlpha, 0.1968503f, 0.1968505f); // comment out previous and watch what happens..
			Assert.Equal(1.1f, material.Elasticity);
			Assert.Equal(2.02f, material.ElasticityFalloff);
			Assert.Equal(2.322f, material.Friction);
			Assert.Equal(255, material.Glossiness.Red);
			Assert.Equal(158, material.Glossiness.Green);
			Assert.Equal(70, material.Glossiness.Blue);
			//Assert.Equal(0.4078431f, material.GlossyImageLerp);
			Assert.InRange(material.GlossyImageLerp, 0.4078430f, 0.4078432f);
			Assert.Equal(false, material.IsMetal);
			Assert.Equal(true, material.IsOpacityActive);
			Assert.Equal(0.8183f, material.Opacity);
			Assert.Equal(0.53628f, material.Roughness);
			Assert.Equal(7.4332f, material.ScatterAngle);
			//Assert.Equal(0.04705882f, material.Thickness);
			Assert.InRange(material.Thickness, 0.04705881f, 0.04705883f);
			Assert.Equal(0.5492f, material.WrapLighting);
		}
	}
}
