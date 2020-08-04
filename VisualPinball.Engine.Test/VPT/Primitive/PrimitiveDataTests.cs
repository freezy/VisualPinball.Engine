﻿using FluentAssertions;
 using NUnit.Framework;
 using VisualPinball.Engine.Test.Test;
 using VisualPinball.Engine.VPT.Primitive;
 using VisualPinball.Engine.VPT.Table;

 namespace VisualPinball.Engine.Test.VPT.Primitive
{
	public class PrimitiveDataTests
	{
		[Test]
		public void ShouldReadPrimitiveData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Primitive);
			ValidatePrimitiveData(table.Primitive("Cube").Data);
		}

		[Test]
		public void ShouldWritePrimitiveData()
		{
			const string tmpFileName = "ShouldWritePrimitiveData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Primitive);
			new TableWriter(table).WriteTable(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidatePrimitiveData(writtenTable.Primitive("Cube").Data);
		}

		private static void ValidatePrimitiveData(PrimitiveData data)
		{
			data.BackfacesEnabled.Should().Be(false);
			data.CollisionReductionFactor.Should().Be(0.6119f);
			data.CompressedIndices.Should().Be(53);
			data.CompressedVertices.Should().Be(135);
			data.DepthBias.Should().Be(0.1665f);
			data.DisableLightingBelow.Should().Be(0.0012f);
			data.DisableLightingTop.Should().BeInRange(0.019f, 0.02f);
			data.DisplayTexture.Should().Be(false);
			data.DrawTexturesInside.Should().Be(false);
			data.EdgeFactorUi.Should().Be(0.267f);
			data.Elasticity.Should().Be(0.3163f);
			data.ElasticityFalloff.Should().Be(0.53219f);
			data.Friction.Should().Be(0.36189f);
			data.HitEvent.Should().Be(false);
			data.Image.Should().Be("p1-beachwood");
			data.IsCollidable.Should().Be(true);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsToy.Should().Be(false);
			data.IsVisible.Should().Be(true);
			data.Material.Should().Be("Playfield");
			data.MeshFileName.Should().Be("cube.obj");
			data.NormalMap.Should().Be("");
			data.NumIndices.Should().Be(36);
			data.NumVertices.Should().Be(24);
			data.OverwritePhysics.Should().Be(true);
			data.PhysicsMaterial.Should().Be("");
			data.Position.X.Should().Be(500.1f);
			data.Position.Y.Should().Be(500.2f);
			data.Position.Z.Should().Be(0.123f);
			data.RotAndTra[0].Should().Be(0.12f);
			data.RotAndTra[1].Should().Be(0.98f);
			data.RotAndTra[2].Should().Be(0.69f);
			data.RotAndTra[3].Should().Be(0.45f);
			data.RotAndTra[4].Should().Be(0.47f);
			data.RotAndTra[5].Should().Be(0.24f);
			data.RotAndTra[6].Should().Be(0.19f);
			data.RotAndTra[7].Should().Be(0.59f);
			data.RotAndTra[8].Should().Be(0.13f);
			data.Scatter.Should().Be(0.9815f);
			data.SideColor.Red.Should().Be(150);
			data.SideColor.Green.Should().Be(150);
			data.SideColor.Blue.Should().Be(150);
			data.Sides.Should().Be(4);
			data.Size.X.Should().Be(100.11f);
			data.Size.Y.Should().Be(100.22f);
			data.Size.Z.Should().Be(100.33f);
			data.StaticRendering.Should().Be(true);
			data.Threshold.Should().Be(2f);
			data.Use3DMesh.Should().Be(true);

			data.Mesh.Vertices[0].X.Should().Be(1f);
			data.Mesh.Vertices[0].Y.Should().Be(1f);
			data.Mesh.Vertices[0].Z.Should().Be(-1f);
			data.Mesh.Vertices[0].Nx.Should().Be(0f);
			data.Mesh.Vertices[0].Ny.Should().Be(1f);
			data.Mesh.Vertices[0].Nz.Should().Be(0f);
			data.Mesh.Vertices[0].Tu.Should().Be(0.375f);
			data.Mesh.Vertices[0].Tv.Should().Be(0f);
		}
	}
}
