using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Ramp
{
	public class RampDataTests
	{
		[Fact]
		public void ShouldReadRampData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Ramp);
			ValidateRampData(table.Ramps["FlatL"].Data);
		}

		[Fact]
		public void ShouldWriteRampData()
		{
			const string tmpFileName = "ShouldWriteRampData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Ramp);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateRampData(writtenTable.Ramps["FlatL"].Data);
		}

		private static void ValidateRampData(RampData data)
		{
			Assert.Equal(0.11254f, data.DepthBias);
			Assert.Equal(3, data.DragPoints.Length);
			Assert.Equal(0.2643f, data.Elasticity);
			Assert.Equal(0.7125f, data.Friction);
			Assert.Equal(2.1243f, data.HeightBottom);
			Assert.Equal(54.1632f, data.HeightTop);
			Assert.Equal(false, data.HitEvent);
			Assert.Equal("test_pattern", data.Image);
			Assert.Equal(RampImageAlignment.ImageModeWrap, data.ImageAlignment);
			Assert.Equal(true, data.ImageWalls);
			Assert.Equal(true, data.IsCollidable);
			Assert.Equal(false, data.IsReflectionEnabled);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal(62.2189f, data.LeftWallHeight);
			Assert.Equal(35.2109f, data.LeftWallHeightVisible);
			Assert.Equal("Playfield", data.Material);
			Assert.Equal(true, data.OverwritePhysics);
			Assert.Equal("", data.PhysicsMaterial);
			Assert.Equal(RampType.RampTypeFlat, data.RampType);
			Assert.Equal(62.7891f, data.RightWallHeight);
			Assert.Equal(0f, data.RightWallHeightVisible);
			Assert.Equal(1.2783f, data.Scatter);
			Assert.Equal(2.3127f, data.Threshold);
			Assert.Equal(75.289f, data.WidthBottom);
			Assert.Equal(99.921f, data.WidthTop);
		}

		[Fact]
		public void ShouldLoadWireData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Ramp);
			var data = table.Ramps["Wire3R"].Data;

			Assert.Equal(RampType.RampType3WireRight, data.RampType);
			Assert.Equal(2.982f, data.WireDiameter);
			Assert.Equal(50.278f, data.WireDistanceX);
			Assert.Equal(88.381f, data.WireDistanceY);
		}
	}
}
