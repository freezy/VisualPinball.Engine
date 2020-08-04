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
			AssertObjMesh(_table, _obj, _table.Kicker("Cup"));
			AssertObjMesh(_table, _obj, _table.Kicker("Cup2"));
			AssertObjMesh(_table, _obj, _table.Kicker("Gottlieb"), threshold: 0.00015f);
			AssertObjMesh(_table, _obj, _table.Kicker("Hole"));
			AssertObjMesh(_table, _obj, _table.Kicker("HoleSimple"));
			AssertObjMesh(_table, _obj, _table.Kicker("Williams"), threshold: 0.001f);
			AssertObjMesh(_table, _obj, _table.Kicker("Scaled"));
			AssertObjMesh(_table, _obj, _table.Kicker("Rotated"), threshold: 0.00015f);
			AssertObjMesh(_table, _obj, _table.Kicker("Surface"));
			AssertObjMesh(_table, _obj, _table.Kicker("Data"), threshold: 0.00015f);
		}
	}
}
