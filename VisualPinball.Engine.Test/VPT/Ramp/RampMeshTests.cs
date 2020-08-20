using System.Linq;
using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

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

		[Test]
		public void ShouldGenerateFlatWithoutWalls()
		{
			ShouldGenerate("FlatNone");
		}

		[Test]
		public void ShouldGenerateFlatWithBothWalls()
		{
			ShouldGenerate("Flat");
		}

		[Test]
		public void ShouldGenerate1WireRamp()
		{
			ShouldGenerate("Wire1");
		}

		[Test]
		public void ShouldGenerate2WireRamp()
		{
			ShouldGenerate("Wire2");
		}

		[Test]
		public void ShouldGenerate3WireRamp()
		{
			ShouldGenerate("Wire3L");
			ShouldGenerate("Wire3R");
		}

		[Test]
		public void ShouldGenerate4WireRamp()
		{
			ShouldGenerate("Wire4");
		}

		private void ShouldGenerate(string name)
		{
			var ramp = _table.Ramp(name);
			var rampMeshes = ramp.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh).ToArray();
#if WIN64
			const float threshold = 0.0001f;
#else
			const float threshold = 4.5f;
#endif
			AssertObjMesh(_obj, ramp.Name, rampMeshes, threshold);
		}
	}
}
