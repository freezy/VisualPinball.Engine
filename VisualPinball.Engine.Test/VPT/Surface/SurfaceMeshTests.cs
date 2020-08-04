using System.Linq;
using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.Surface
{
	public class SurfaceMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public SurfaceMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Surface);
			_obj = LoadObjFixture(ObjPath.Surface);
		}

		[Test]
		public void ShouldGenerateTopAndSides()
		{
			var surface = _table.Surface("Wall");
			var surfaceMeshes = surface.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, surface.Name, surfaceMeshes);
		}

		[Test]
		public void ShouldGenerateOnlyTop()
		{
			var surface = _table.Surface("SideInvisible");
			var surfaceMeshes = surface.GetRenderObjects(_table).RenderObjects
				.Where(ro => ro.IsVisible)
				.Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, surface.Name, surfaceMeshes, 0.001f);
		}

		[Test]
		public void ShouldGenerateOnlySide()
		{
			var surface = _table.Surface("TopInvisible");
			var surfaceMeshes = surface.GetRenderObjects(_table).RenderObjects
				.Where(ro => ro.IsVisible)
				.Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, surface.Name, surfaceMeshes);
		}
	}
}
