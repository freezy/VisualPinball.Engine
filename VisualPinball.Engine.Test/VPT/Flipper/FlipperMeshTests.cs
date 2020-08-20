using System.Linq;
using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

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

		[Test]
		public void ShouldGenerateFatMesh()
		{
			var flipper = _table.Flipper("FatFlipper");
			var flipperMeshes = flipper.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var flipperMesh in flipperMeshes) {
				AssertObjMesh(_obj, flipperMesh, $"{flipper.Name}{flipperMesh.Name}", 0.00013f);
			}
		}

		[Test]
		public void ShouldGenerateFatRubberMesh()
		{
			var flipper = _table.Flipper("FatRubberFlipper");
			var flipperMeshes = flipper.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var flipperMesh in flipperMeshes) {
				AssertObjMesh(_obj, flipperMesh, $"{flipper.Name}{flipperMesh.Name}", threshold: 0.00015f);
			}
		}

		[Test]
		public void ShouldGenerateFlipperOnSurfaceMesh()
		{
			var flipper = _table.Flipper("SurfaceFlipper");
			var flipperMeshes = flipper.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var flipperMesh in flipperMeshes) {
				AssertObjMesh(_obj, flipperMesh, $"{flipper.Name}{flipperMesh.Name}");
			}
		}
	}
}
