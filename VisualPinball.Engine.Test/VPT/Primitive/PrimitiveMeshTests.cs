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
			_table = Engine.VPT.Table.Table.Load(@"..\..\Fixtures\PrimitiveData.vpx");
			_obj = LoadObjFixture(@"..\..\Fixtures\PrimitiveData.obj");
		}

		[Fact]
		public void ShouldGenerateImportedMesh()
		{
			var bookMesh = _table.Primitives["Books"].GetRenderObjects(_table)[0].Mesh;
			AssertObjMesh(_obj, bookMesh);
		}
	}
}
