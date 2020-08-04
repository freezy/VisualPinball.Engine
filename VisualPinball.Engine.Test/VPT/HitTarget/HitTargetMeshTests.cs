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
			AssertObjMesh(_table, _obj, _table.HitTarget("DropTargetBeveled"));
			AssertObjMesh(_table, _obj, _table.HitTarget("DropTargetFlatSimple"));
			AssertObjMesh(_table, _obj, _table.HitTarget("DropTargetSimple"));
			AssertObjMesh(_table, _obj, _table.HitTarget("Data"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitFatTargetSlim"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitFatTargetSquare"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitTargetRect"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitTargetRound"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitTargetSlim"));
			AssertObjMesh(_table, _obj, _table.HitTarget("ScaledTarget"));
			AssertObjMesh(_table, _obj, _table.HitTarget("RotatedTarget"));
			AssertObjMesh(_table, _obj, _table.HitTarget("DroppedTarget"));
		}
	}
}
