using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Engine.Test.VPT.HitTarget
{
	public class HitTargetDataTests
	{
		[Test]
		public void ShouldReadHitTargetData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.HitTarget);
			ValidateHitTargetData(table.HitTarget("Data").Data);
		}

		[Test]
		public void ShouldWriteHitTargetData()
		{
			const string tmpFileName = "ShouldWriteHitTargetData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.HitTarget);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateHitTargetData(writtenTable.HitTarget("Data").Data);
		}

		private static void ValidateHitTargetData(HitTargetData data)
		{
			data.DepthBias.Should().Be(0.651f);
			data.DisableLightingBelow.Should().Be(0.1932f);
			data.DisableLightingTop.Should().Be(0.2f);
			data.DropSpeed.Should().Be(0.5982f);
			data.Elasticity.Should().Be(0.9287f);
			data.ElasticityFalloff.Should().Be(0.1897f);
			data.Friction.Should().Be(1f);
			data.Image.Should().Be("");
			data.IsCollidable.Should().Be(true);
			data.IsDropped.Should().Be(false);
			data.IsDropTarget.Should().Be(false);
			data.IsLegacy.Should().Be(false);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.Material.Should().Be("Playfield");
			data.OverwritePhysics.Should().Be(true);
			data.PhysicsMaterial.Should().Be("");
			data.Position.X.Should().Be(427.12f);
			data.Position.Y.Should().Be(1079.21f);
			data.Position.Z.Should().Be(12.3221f);
			data.RaiseDelay.Should().Be(216);
			data.RotZ.Should().Be(2.124f);
			data.Scatter.Should().Be(5.12354f);
			data.Size.X.Should().Be(32.32f);
			data.Size.Y.Should().Be(32.44f);
			data.Size.Z.Should().Be(32.5055f);
			data.TargetType.Should().Be(TargetType.HitFatTargetRectangle);
			data.Threshold.Should().Be(3.2124f);
			data.UseHitEvent.Should().Be(true);
		}
	}
}
