using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.HitTarget
{
	public class HitTargetMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public HitTargetMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.HitTarget);
			_obj = LoadObjFixture(ObjPath.HitTarget);
		}

		[Test]
		public void ShouldGenerateMesh()
		{
			AssertObjMesh(_table, _obj, _table.HitTargets["DropTargetBeveled"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["DropTargetFlatSimple"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["DropTargetSimple"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["Data"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["HitFatTargetSlim"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["HitFatTargetSquare"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["HitTargetRect"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["HitTargetRound"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["HitTargetSlim"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["ScaledTarget"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["RotatedTarget"]);
			AssertObjMesh(_table, _obj, _table.HitTargets["DroppedTarget"]);
		}
	}
}
