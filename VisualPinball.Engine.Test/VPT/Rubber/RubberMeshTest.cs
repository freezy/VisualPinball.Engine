using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.Rubber
{
	public class RubberMeshTest : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public RubberMeshTest()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Rubber);
			_obj = LoadObjFixture(ObjPath.Rubber);
		}

		[Test]
		public void ShouldGenerateMesh()
		{
			var rubberMesh = _table.Rubber("Rubber2").GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, rubberMesh, threshold: 0.00015f);
		}

		[Test]
		public void ShouldGenerateThickMesh()
		{
			var rubberMesh = _table.Rubber("Rubber1").GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, rubberMesh, threshold: 0.001f);
		}
	}
}
