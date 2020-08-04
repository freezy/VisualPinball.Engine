using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.Trigger
{
	public class TriggerMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public TriggerMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Trigger);
			_obj = LoadObjFixture(ObjPath.Trigger);
		}

		[Test]
		public void ShouldGenerateMeshesCorrectly()
		{
			AssertObjMesh(_table, _obj, _table.Trigger("Button"));
			AssertObjMesh(_table, _obj, _table.Trigger("Star"), threshold: 0.001f);
			AssertObjMesh(_table, _obj, _table.Trigger("WireA"));
			AssertObjMesh(_table, _obj, _table.Trigger("WireB"));
			AssertObjMesh(_table, _obj, _table.Trigger("WireC"));
			AssertObjMesh(_table, _obj, _table.Trigger("WireD"));
			AssertObjMesh(_table, _obj, _table.Trigger("Surface"));

			// the last two fail because vpx ignores thickness when exporting.
			// re-enable when fixed on vp side.
			//AssertObjMesh(_table, _obj, _table.Trigger("ThickWire"));
			//AssertObjMesh(_table, _obj, _table.Trigger("Data"));
		}
	}
}
