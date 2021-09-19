// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Ramp
{
	public class RampMeshTests : MeshTests
	{
		private readonly FileTableContainer _tc;
		private readonly ObjFile _obj;

		public RampMeshTests()
		{
			_tc = FileTableContainer.Load(VpxPath.Ramp);
			_obj = LoadObjFixture(ObjPath.Ramp);
		}

		[Test]
		public void ShouldGenerateFlatWithoutWalls()
		{
			ShouldGenerate("FlatNone", "Floor");
		}

		[Test]
		public void ShouldGenerateFlatWithBothWalls()
		{
			ShouldGenerate("Flat", "Floor", "RightWall", "LeftWall");
		}

		[Test]
		public void ShouldGenerate1WireRamp()
		{
			ShouldGenerate("Wire1", "Wire1");
		}

		[Test]
		public void ShouldGenerate2WireRamp()
		{
			ShouldGenerate("Wire2", "Wire1", "Wire2");
		}

		[Test]
		public void ShouldGenerate3WireRamp()
		{
			ShouldGenerate("Wire3L", "Wire2", "Wire3", "Wire4");
			ShouldGenerate("Wire3R", "Wire1", "Wire3", "Wire4");
		}

		[Test]
		public void ShouldGenerate4WireRamp()
		{
			ShouldGenerate("Wire4", "Wire1", "Wire2", "Wire3", "Wire4");
		}

		private void ShouldGenerate(string name, params string[] meshIds)
		{
			var ramp = _tc.Ramp(name);
			var rampMeshes = GetMeshes(_tc.Table, ramp, meshIds);
#if WIN64
			const float threshold = 0.0001f;
#else
			const float threshold = 4.5f;
#endif
			AssertObjMesh(_obj, ramp.Name, rampMeshes, threshold);
		}
	}
}
