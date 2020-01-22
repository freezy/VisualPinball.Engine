using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Test.Test;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Kicker
{
	public class KickerMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public KickerMeshTests(ITestOutputHelper output) : base(output)
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Kicker);
			_obj = LoadObjFixture(ObjPath.Kicker);
		}

		[Fact]
		public void ShouldGenerateMeshesCorrectly()
		{
			AssertObjMesh(_table, _obj, _table.Kickers["Cup"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Cup2"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Gottlieb"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Hole"]);
			AssertObjMesh(_table, _obj, _table.Kickers["HoleSimple"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Williams"], threshold: 0.001);
			AssertObjMesh(_table, _obj, _table.Kickers["Scaled"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Rotated"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Surface"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Data"]);
		}
	}
}
