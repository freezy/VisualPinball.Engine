using JeremyAnsel.Media.WavefrontObj;
using NLog;
using VisualPinball.Engine.Test.Test;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Table
{
	public class TableMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public TableMeshTests(ITestOutputHelper output) : base(output)
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Table);
			_obj = LoadObjFixture(ObjPath.Table);
		}

		[Fact]
		public void ShouldGeneratePlayfieldCorrectly()
		{
			var tableMesh = _table.GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, tableMesh);
		}
	}
}
