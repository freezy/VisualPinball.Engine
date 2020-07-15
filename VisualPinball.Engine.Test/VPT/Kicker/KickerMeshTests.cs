using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.Kicker
{
	public class KickerMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public KickerMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Kicker);
			_obj = LoadObjFixture(ObjPath.Kicker);
		}

		[Test]
		public void ShouldGenerateMeshesCorrectly()
		{
			AssertObjMesh(_table, _obj, _table.Kickers["Cup"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Cup2"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Gottlieb"], threshold: 0.00015f);
			AssertObjMesh(_table, _obj, _table.Kickers["Hole"]);
			AssertObjMesh(_table, _obj, _table.Kickers["HoleSimple"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Williams"], threshold: 0.001f);
			AssertObjMesh(_table, _obj, _table.Kickers["Scaled"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Rotated"], threshold: 0.00015f);
			AssertObjMesh(_table, _obj, _table.Kickers["Surface"]);
			AssertObjMesh(_table, _obj, _table.Kickers["Data"], threshold: 0.00015f);
		}
	}
}
