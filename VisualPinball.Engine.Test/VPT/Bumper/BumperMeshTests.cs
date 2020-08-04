using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.Bumper
{
	public class BumperMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public BumperMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Bumper);
			_obj = LoadObjFixture(ObjPath.Bumper);
		}

		[Test]
		public void ShouldGenerateMesh()
		{
			AssertObjMesh(_table, _obj, _table.Bumper("Bumper2"), (item, mesh) => $"{item.Name}{mesh.Name}");
		}
	}
}
