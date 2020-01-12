using Xunit;

namespace VisualPinball.Engine.Test.VPT.Primitive
{
	public class PrimitiveDataTests
	{
		[Fact]
		public void ShouldLoadCorrectData()
		{
			var table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\PrimitiveData.vpx");
			var data = table.Primitives["Cube"].Data;

			Assert.Equal(false, data.BackfacesEnabled);
			Assert.Equal(0.6119f, data.CollisionReductionFactor);
			Assert.Equal(53, data.CompressedIndices);
			Assert.Equal(135, data.CompressedVertices);
			Assert.Equal(0.1665f, data.DepthBias);
			Assert.Equal(0.0012f, data.DisableLightingBelow);
			Assert.InRange(data.DisableLightingTop, 0.019f, 0.02f);
			Assert.Equal(false, data.DisplayTexture);
			Assert.Equal(false, data.DrawTexturesInside);
			Assert.Equal(0.267f, data.EdgeFactorUi);
			Assert.Equal(0.3163f, data.Elasticity);
			Assert.Equal(0.53219f, data.ElasticityFalloff);
			Assert.Equal(0.36189f, data.Friction);
			Assert.Equal(false, data.HitEvent);
			Assert.Equal("p1-beachwood", data.Image);
			Assert.Equal(true, data.IsCollidable);
			Assert.Equal(true, data.IsReflectionEnabled);
			Assert.Equal(false, data.IsToy);
			Assert.Equal(true, data.IsVisible);
			Assert.Equal("Playfield", data.Material);
			Assert.Equal("cube.obj", data.MeshFileName);
			Assert.Equal("", data.NormalMap);
			Assert.Equal(36, data.NumIndices);
			Assert.Equal(24, data.NumVertices);
			Assert.Equal(true, data.OverwritePhysics);
			Assert.Equal("", data.PhysicsMaterial);
			Assert.Equal(500.1f, data.Position.X);
			Assert.Equal(500.2f, data.Position.Y);
			Assert.Equal(0.123f, data.Position.Z);
			Assert.Equal(0.12f, data.RotAndTra[0]);
			Assert.Equal(0.98f, data.RotAndTra[1]);
			Assert.Equal(0.69f, data.RotAndTra[2]);
			Assert.Equal(0.45f, data.RotAndTra[3]);
			Assert.Equal(0.47f, data.RotAndTra[4]);
			Assert.Equal(0.24f, data.RotAndTra[5]);
			Assert.Equal(0.19f, data.RotAndTra[6]);
			Assert.Equal(0.59f, data.RotAndTra[7]);
			Assert.Equal(0.13f, data.RotAndTra[8]);
			Assert.Equal(0.9815f, data.Scatter);
			Assert.Equal(150, data.SideColor.Red);
			Assert.Equal(150, data.SideColor.Green);
			Assert.Equal(150, data.SideColor.Blue);
			Assert.Equal(4, data.Sides);
			Assert.Equal(100.11f, data.Size.X);
			Assert.Equal(100.22f, data.Size.Y);
			Assert.Equal(100.33f, data.Size.Z);
			Assert.Equal(true, data.StaticRendering);
			Assert.Equal(2f, data.Threshold);
			Assert.Equal(true, data.Use3DMesh);
		}
	}
}
