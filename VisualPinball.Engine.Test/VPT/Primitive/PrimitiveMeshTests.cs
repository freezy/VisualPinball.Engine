using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Primitive
{
	public class PrimitiveMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public PrimitiveMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Primitive);
			_obj = LoadObjFixture(ObjPath.Primitive);
		}

		[Fact]
		public void ShouldGenerateImportedMesh()
		{
			var bookMesh = _table.Primitives["Books"].GetRenderObjects(_table)[0].Mesh;
			AssertObjMesh(_obj, bookMesh);
		}

		[Fact]
		public void ShouldGenerateACube()
		{
			var cubeMesh = _table.Primitives["Cube"].GetRenderObjects(_table)[0].Mesh;
			AssertObjMesh(_obj, cubeMesh);
		}

		[Fact]
		public void ShouldGenerateATriangle()
		{
			var triangleMesh = _table.Primitives["Triangle"].GetRenderObjects(_table)[0].Mesh;
			AssertObjMesh(_obj, triangleMesh);
		}

		[Fact]
		public void ShouldProvideCorrectTransformationMatrices()
		{
			var ro = _table.Primitives["Primitive1"].GetRenderObjects(_table, Origin.Original, false)[0];
			Assert.Equal(100f, ro.TransformationMatrix.GetScaling().X);
			Assert.Equal(100f, ro.TransformationMatrix.GetScaling().Y);
			Assert.Equal(100f, ro.TransformationMatrix.GetScaling().Z);

			Assert.Equal(505f, ro.TransformationMatrix.GetTranslation().X);
			Assert.Equal(1305f, ro.TransformationMatrix.GetTranslation().Y);
			Assert.Equal(0f, ro.TransformationMatrix.GetTranslation().Z);
		}
	}
}
