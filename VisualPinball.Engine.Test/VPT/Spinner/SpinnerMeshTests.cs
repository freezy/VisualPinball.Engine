using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Test.VPT.Spinner
{
	public class SpinnerMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public SpinnerMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Spinner);
			_obj = LoadObjFixture(ObjPath.Spinner);
		}

		[Test]
		public void ShouldGenerateBracketMeshes()
		{
			string GetName(IRenderable item, Mesh mesh) => $"{item.Name}{mesh.Name}";
			AssertObjMesh(_table, _obj, _table.Spinner("Spinner"), GetName);
			AssertObjMesh(_table, _obj, _table.Spinner("Transformed"), GetName);
			AssertObjMesh(_table, _obj, _table.Spinner("Surface"), GetName);
			AssertObjMesh(_table, _obj, _table.Spinner("Data"), GetName, 0.001f);
		}

		[Test]
		public void ShouldGenerateMeshWithoutBracket()
		{
			AssertObjMesh(_obj, _table.Spinner("WithoutBracket").GetRenderObjects(_table).RenderObjects[0].Mesh, "WithoutBracketPlate");
			AssertNoObjMesh(_obj, "WithoutBracketBracket");
		}

	}
}
