using System;
using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.VPT.Gate
{
	public class GateMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public GateMeshTests(ITestOutputHelper output) : base(output)
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Gate);
			_obj = LoadObjFixture(ObjPath.Gate);
		}

		[Fact]
		public void ShouldGenerateBracketMeshes()
		{
			string GetName(IRenderable item, Mesh mesh) => $"{item.Name}{mesh.Name}";
			AssertObjMesh(_table, _obj, _table.Gates["LongPlate"], GetName);
			AssertObjMesh(_table, _obj, _table.Gates["Plate"], GetName);
			AssertObjMesh(_table, _obj, _table.Gates["WireRectangle"], GetName);
			AssertObjMesh(_table, _obj, _table.Gates["WireW"], GetName);
			AssertObjMesh(_table, _obj, _table.Gates["TransformedGate"], GetName);
			AssertObjMesh(_table, _obj, _table.Gates["SurfaceGate"], GetName);
		}

		[Fact]
		public void ShouldGenerateMeshWithoutBracket()
		{
			AssertObjMesh(_obj, _table.Gates["NoBracketGate"].GetRenderObjects(_table).RenderObjects[0].Mesh, "NoBracketGateWire");
			AssertNoObjMesh(_obj, "NoBracketGateBracket");
		}

	}
}
