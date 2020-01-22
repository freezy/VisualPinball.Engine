using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Spinner
{
	public class SpinnerMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public SpinnerMeshTests(ITestOutputHelper output) : base(output)
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Spinner);
			_obj = LoadObjFixture(ObjPath.Spinner);
		}

		[Fact]
		public void ShouldGenerateBracketMeshes()
		{
			string GetName(IRenderable item, Mesh mesh) => $"{item.Name}{mesh.Name}";
			AssertObjMesh(_table, _obj, _table.Spinners["Spinner"], GetName);
			AssertObjMesh(_table, _obj, _table.Spinners["Transformed"], GetName);
			AssertObjMesh(_table, _obj, _table.Spinners["Surface"], GetName);
			AssertObjMesh(_table, _obj, _table.Spinners["Data"], GetName, 0.001);
		}

		[Fact]
		public void ShouldGenerateMeshWithoutBracket()
		{
			AssertObjMesh(_obj, _table.Spinners["WithoutBracket"].GetRenderObjects(_table).RenderObjects[0].Mesh, "WithoutBracketPlate");
			AssertNoObjMesh(_obj, "WithoutBracketBracket");
		}

	}
}
