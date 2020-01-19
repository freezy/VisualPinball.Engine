using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Primitive
{
	public class PrimitiveMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public PrimitiveMeshTests(ITestOutputHelper output) : base(output)
		{
			// _table = Engine.VPT.Table.Table.Load(VpxPath.Primitive);
			// _obj = LoadObjFixture(ObjPath.Primitive);
		}

		[Fact]
		public void ShouldGenerateImportedMesh()
		{
			var bookMesh = _table.Primitives["Books"].GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, bookMesh);
		}

		[Fact]
		public void ShouldGenerateACube()
		{
			var cubeMesh = _table.Primitives["Cube"].GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, cubeMesh);
		}

		[Fact]
		public void ShouldGenerateATriangle()
		{
			var triangleMesh = _table.Primitives["Triangle"].GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, triangleMesh);
		}

		[Fact]
		public void ShouldProvideCorrectTransformationMatrices()
		{
			var rog = _table.Primitives["Primitive1"].GetRenderObjects(_table, Origin.Original, false);
			Assert.Equal(100f, rog.TransformationMatrix.GetScaling().X);
			Assert.Equal(100f, rog.TransformationMatrix.GetScaling().Y);
			Assert.Equal(100f, rog.TransformationMatrix.GetScaling().Z);

			Assert.Equal(505f, rog.TransformationMatrix.GetTranslation().X);
			Assert.Equal(1305f, rog.TransformationMatrix.GetTranslation().Y);
			Assert.Equal(0f, rog.TransformationMatrix.GetTranslation().Z);
		}

		[Fact]
		public void ShouldNotCrash()
		{
			Logger.Info("log test");
			Engine.VPT.Table.Table.Load(@"..\..\VPT\Primitive\PrimitiveLzwError2.vpx");
		}
	}
}
