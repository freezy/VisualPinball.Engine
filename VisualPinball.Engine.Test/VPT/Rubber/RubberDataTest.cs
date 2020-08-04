using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Engine.Test.VPT.Rubber
{
	public class RubberDataTest
	{
		[Test]
		public void ShouldReadRubberData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Rubber);
			ValidateRubberData1(table.Rubber("Rubber1").Data);
			ValidateRubberData2(table.Rubber("Rubber2").Data);
		}

		[Test]
		public void ShouldWriteRubberData()
		{
			const string tmpFileName = "ShouldWriteRubberData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Rubber);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateRubberData1(writtenTable.Rubber("Rubber1").Data);
			ValidateRubberData2(writtenTable.Rubber("Rubber2").Data);
		}

		private static void ValidateRubberData1(RubberData data)
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

		private static void ValidateRubberData2(RubberData data)
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
