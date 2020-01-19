using System.Linq;
using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Test.Test;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.HitTarget
{
	public class HitTargetMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public HitTargetMeshTests(ITestOutputHelper output) : base(output)
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.HitTarget);
			_obj = LoadObjFixture(ObjPath.HitTarget);
		}

		[Fact]
		public void ShouldGenerateMesh()
		{
			var target = _table.HitTargets["DropTargetBeveled"];
			var targetMeshes = target.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var mesh in targetMeshes) {
				AssertObjMesh(_obj, mesh);
			}
		}
	}
}
