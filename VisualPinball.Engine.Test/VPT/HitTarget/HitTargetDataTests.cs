using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.HitTarget
{
	public class HitTargetDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.HitTarget);
			var data = table.HitTargets["Data"].Data;

			Assert.Equal(0.651f, data.DepthBias);
			Assert.Equal(0.1932f, data.DisableLightingBelow);
			Assert.Equal(0.2f, data.DisableLightingTop);
			Assert.Equal(0.5982f, data.DropSpeed);
			Assert.Equal(0.9287f, data.Elasticity);
			Assert.Equal(0.1897f, data.ElasticityFalloff);
			Assert.Equal(1f, data.Friction);
			Assert.Equal("", data.Image);
			Assert.Equal(true, data.IsCollidable);
			Assert.Equal(false, data.IsDropped);
			Assert.Equal(false, data.IsDropTarget);
			Assert.Equal(false, data.IsLegacy);
			Assert.Equal(true, data.IsReflectionEnabled);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal("", data.Material);
			Assert.Equal(true, data.OverwritePhysics);
			Assert.Equal("", data.PhysicsMaterial);
			Assert.Equal(427.12f, data.Position.X);
			Assert.Equal(1079.21f, data.Position.Y);
			Assert.Equal(12.3221f, data.Position.Z);
			Assert.Equal(216, data.RaiseDelay);
			Assert.Equal(2.124f, data.RotZ);
			Assert.Equal(5.12354f, data.Scatter);
			Assert.Equal(32.32f, data.Size.X);
			Assert.Equal(32.44f, data.Size.Y);
			Assert.Equal(32.5055f, data.Size.Z);
			Assert.Equal(TargetType.HitFatTargetRectangle, data.TargetType);
			Assert.Equal(3.2124f, data.Threshold);
			Assert.Equal(true, data.UseHitEvent);
		}
	}
}
