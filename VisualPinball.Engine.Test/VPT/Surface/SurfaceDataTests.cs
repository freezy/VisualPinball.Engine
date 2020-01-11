using Xunit;

namespace VisualPinball.Engine.Test.VPT.Surface
{
	public class SurfaceDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\SurfaceData.vpx");
			var data = table.Surfaces["TopInvisible"].Data;

			Assert.Equal(0.6985f, data.DisableLightingBelow);
			Assert.InRange(data.DisableLightingTop, 0.129f, 0.13f);
			Assert.Equal(false, data.DisplayTexture); // editor only
			Assert.Equal(10, data.DragPoints.Length);
			Assert.Equal(0.368f, data.Elasticity);
			Assert.Equal(0.381f, data.Friction);
			Assert.Equal(2.7f, data.HeightBottom);
			Assert.Equal(49.6f, data.HeightTop);
			Assert.Equal(false, data.HitEvent);
			Assert.Equal("", data.Image);
			Assert.Equal(true, data.Inner); // ???
			Assert.Equal(true, data.IsBottomSolid); // ???
			Assert.Equal(true, data.IsCollidable);
			Assert.Equal(false, data.IsDroppable);
			Assert.Equal(false, data.IsFlipbook);
			Assert.Equal(true, data.IsReflectionEnabled);
			Assert.Equal(true, data.IsSideVisible);
			Assert.Equal(false, data.IsTopBottomVisible);
			Assert.Equal(true, data.OverwritePhysics);
			Assert.Equal("", data.PhysicsMaterial);
			Assert.Equal(0f, data.Scatter);
			Assert.Equal("test_pattern", data.SideImage);
			Assert.Equal("Playfield", data.SideMaterial);
			Assert.Equal(true, data.SlingshotAnimation);
			Assert.InRange(data.SlingshotForce, 80.22f, 80.24f);
			Assert.Equal("", data.SlingShotMaterial);
			Assert.Equal(0.029f, data.SlingshotThreshold);
			Assert.Equal(2f, data.Threshold);
			Assert.Equal("", data.TopMaterial);
		}
	}
}
