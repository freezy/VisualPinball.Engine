// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

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
