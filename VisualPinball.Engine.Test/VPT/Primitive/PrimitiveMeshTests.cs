using JeremyAnsel.Media.WavefrontObj;
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
	}
}
