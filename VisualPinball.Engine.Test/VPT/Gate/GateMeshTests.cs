using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Test.VPT.Gate
{
	public class GateMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public GateMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Gate);
			_obj = LoadObjFixture(ObjPath.Gate);
		}

		[Test]
		public void ShouldGenerateBracketMeshes()
		{
			string GetName(IRenderable item, Mesh mesh) => $"{item.Name}{mesh.Name}";
			AssertObjMesh(_table, _obj, _table.Gate("LongPlate"), GetName, 0.00015f);
			AssertObjMesh(_table, _obj, _table.Gate("Plate"), GetName);
			AssertObjMesh(_table, _obj, _table.Gate("WireRectangle"), GetName);
			AssertObjMesh(_table, _obj, _table.Gate("WireW"), GetName, 0.00015f);
			AssertObjMesh(_table, _obj, _table.Gate("TransformedGate"), GetName);
			AssertObjMesh(_table, _obj, _table.Gate("SurfaceGate"), GetName);
		}

		[Test]
		public void ShouldGenerateMeshWithoutBracket()
		{
			AssertObjMesh(_obj, _table.Gate("NoBracketGate").GetRenderObjects(_table).RenderObjects[0].Mesh, "NoBracketGateWire");
			AssertNoObjMesh(_obj, "NoBracketGateBracket");
		}

	}
}
