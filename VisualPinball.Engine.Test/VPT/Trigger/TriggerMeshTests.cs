using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Test.Test;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Trigger
{
	public class TriggerMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public TriggerMeshTests(ITestOutputHelper output) : base(output)
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Trigger);
			_obj = LoadObjFixture(ObjPath.Trigger);
		}

		[Fact]
		public void ShouldGenerateMeshesCorrectly()
		{
			AssertObjMesh(_table, _obj, _table.Triggers["Button"]);
			AssertObjMesh(_table, _obj, _table.Triggers["Star"], threshold: 0.001);
			AssertObjMesh(_table, _obj, _table.Triggers["WireA"]);
			AssertObjMesh(_table, _obj, _table.Triggers["WireB"]);
			AssertObjMesh(_table, _obj, _table.Triggers["WireC"]);
			AssertObjMesh(_table, _obj, _table.Triggers["WireD"]);
			AssertObjMesh(_table, _obj, _table.Triggers["Surface"]);

			// the last two fail because vpx ignores thickness when exporting.
			// re-enable when fixed on vp side.
			//AssertObjMesh(_table, _obj, _table.Triggers["ThickWire"]);
			//AssertObjMesh(_table, _obj, _table.Triggers["Data"]);
		}
	}
}
