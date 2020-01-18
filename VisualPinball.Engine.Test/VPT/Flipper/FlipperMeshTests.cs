using System.Linq;
using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Test.Test;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Flipper
{
	public class FlipperMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public FlipperMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Flipper);
			_obj = LoadObjFixture(ObjPath.Flipper);
		}

		[Fact]
		public void ShouldGenerateFatMesh()
		{
			var flipper = _table.Flippers["FatFlipper"];
			var flipperMeshes = flipper.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var flipperMesh in flipperMeshes) {
				AssertObjMesh(_obj, flipperMesh, $"{flipper.Name}{flipperMesh.Name}");
			}
		}

		[Fact]
		public void ShouldGenerateFatRubberMesh()
		{
			var flipper = _table.Flippers["FatRubberFlipper"];
			var flipperMeshes = flipper.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var flipperMesh in flipperMeshes) {
				AssertObjMesh(_obj, flipperMesh, $"{flipper.Name}{flipperMesh.Name}");
			}
		}

		[Fact]
		public void ShouldGenerateFlipperOnSurfaceMesh()
		{
			var flipper = _table.Flippers["SurfaceFlipper"];
			var flipperMeshes = flipper.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var flipperMesh in flipperMeshes) {
				AssertObjMesh(_obj, flipperMesh, $"{flipper.Name}{flipperMesh.Name}");
			}
		}
	}
}
