using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Flipper
{
	public class FlipperDataTests
	{
		[Fact]
		public void ShouldReadFlipperData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);
			ValidateFlipper(table.Flippers["FatFlipper"].Data);
		}

		[Fact]
		public void ShouldWriteFlipperData()
		{
			const string tmpFileName = "ShouldWriteFlipperData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateFlipper(writtenTable.Flippers["FatFlipper"].Data);
		}

		private static void ValidateFlipper(FlipperData data)
		{
			Assert.Equal(30.0303f, data.BaseRadius);
			Assert.Equal(269.287f, data.Center.X);
			Assert.Equal(1301.21f, data.Center.Y);
			Assert.Equal(0.823f, data.Elasticity);
			Assert.Equal(0.4321f, data.ElasticityFalloff);
			Assert.Equal(70.701f, data.EndAngle);
			Assert.Equal(20.1762f, data.EndRadius);
			Assert.Equal(150.987f, data.FlipperRadius);
			Assert.Equal(150.987f, data.FlipperRadiusMax);
			Assert.Equal(0.332f, data.FlipperRadiusMin);
			Assert.Equal(0.6187f, data.Friction);
			Assert.Equal(70.1627f, data.Height);
			Assert.Equal("ldr", data.Image);
			Assert.Equal(true, data.IsEnabled);
			Assert.Equal(true, data.IsReflectionEnabled);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal(1.1992f, data.Mass);
			Assert.Equal("Playfield", data.Material);
			Assert.Equal(0, data.OverridePhysics);
			Assert.Equal(3.109f, data.RampUp);
			Assert.Equal(0.05813f, data.Return);
			Assert.Equal(19.912f, data.RubberHeight);
			Assert.Equal("", data.RubberMaterial);
			Assert.Equal(24.1872f, data.RubberWidth);
			Assert.Equal(0.192f, data.Scatter);
			Assert.Equal(121.163f, data.StartAngle);
			Assert.Equal(2200.1832f, data.Strength);
			Assert.Equal("", data.Surface);
			Assert.Equal(0.7532f, data.TorqueDamping);
			Assert.Equal(6.209f, data.TorqueDampingAngle);
		}
	}
}
