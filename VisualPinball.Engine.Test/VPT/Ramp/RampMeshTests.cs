using System.Linq;
using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Test.Test;
using Xunit;

namespace VisualPinball.Engine.Test.VPT.Ramp
{
	public class RampMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public RampMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Ramp);
			_obj = LoadObjFixture(ObjPath.Ramp);
		}

		[Fact]
		public void ShouldGenerateFlatWithoutWalls()
		{
			ShouldGenerate("FlatNone");
		}

		[Fact]
		public void ShouldGenerateFlatWithBothWalls()
		{
			ShouldGenerate("Flat");
		}

		[Fact]
		public void ShouldGenerate1WireRamp()
		{
			ShouldGenerate("Wire1");
		}

		[Fact]
		public void ShouldGenerate2WireRamp()
		{
			ShouldGenerate("Wire2");
		}

		[Fact]
		public void ShouldGenerate3WireRamp()
		{
			ShouldGenerate("Wire3L");
			ShouldGenerate("Wire3R");
		}

		[Fact]
		public void ShouldGenerate4WireRamp()
		{
			ShouldGenerate("Wire4");
		}

		private void ShouldGenerate(string name)
		{
			var ramp = _table.Ramps[name];
			var rampMeshes = ramp.GetRenderObjects(_table).Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, ramp.Name, rampMeshes);
		}
	}
}
