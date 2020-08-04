using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Engine.Test.VPT.Ramp
{
	public class RampDataTests
	{
		[Test]
		public void ShouldReadRampData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Ramp);
			ValidateRampData(table.Ramp("FlatL").Data);
		}

		[Test]
		public void ShouldWriteRampData()
		{
			const string tmpFileName = "ShouldWriteRampData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Ramp);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateRampData(writtenTable.Ramp("FlatL").Data);
		}

		private static void ValidateRampData(RampData data)
		{
			data.DepthBias.Should().Be(0.11254f);
			data.DragPoints.Length.Should().Be(3);
			data.Elasticity.Should().Be(0.2643f);
			data.Friction.Should().Be(0.7125f);
			data.HeightBottom.Should().Be(2.1243f);
			data.HeightTop.Should().Be(54.1632f);
			data.HitEvent.Should().Be(false);
			data.Image.Should().Be("test_pattern");
			data.ImageAlignment.Should().Be(RampImageAlignment.ImageModeWrap);
			data.ImageWalls.Should().Be(true);
			data.IsCollidable.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(false);
			data.IsVisible.Should().Be(true);
			data.LeftWallHeight.Should().Be(62.2189f);
			data.LeftWallHeightVisible.Should().Be(35.2109f);
			data.Material.Should().Be("Playfield");
			data.OverwritePhysics.Should().Be(true);
			data.PhysicsMaterial.Should().Be("");
			data.RampType.Should().Be(RampType.RampTypeFlat);
			data.RightWallHeight.Should().Be(62.7891f);
			data.RightWallHeightVisible.Should().Be(0f);
			data.Scatter.Should().Be(1.2783f);
			data.Threshold.Should().Be(2.3127f);
			data.WidthBottom.Should().Be(75.289f);
			data.WidthTop.Should().Be(99.921f);
		}

		[Test]
		public void ShouldLoadWireData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Ramp);
			var data = table.Ramp("Wire3R").Data;

			data.RampType.Should().Be(RampType.RampType3WireRight);
			data.WireDiameter.Should().Be(2.982f);
			data.WireDistanceX.Should().Be(50.278f);
			data.WireDistanceY.Should().Be(88.381f);
		}
	}
}
