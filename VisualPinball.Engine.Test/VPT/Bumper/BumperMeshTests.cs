using System.Linq;
using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Test.Test;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Bumper
{
	public class BumperMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public BumperMeshTests(ITestOutputHelper output) : base(output)
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Bumper);
			_obj = LoadObjFixture(ObjPath.Bumper);
		}

		[Fact]
		public void ShouldGenerateMesh()
		{
			var bumper = _table.Bumpers["Bumper2"];
			var bumperMeshes = bumper.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var bumperMesh in bumperMeshes) {
				AssertObjMesh(_obj, bumperMesh, $"{bumper.Name}{bumperMesh.Name}");
			}
		}
	}
}
