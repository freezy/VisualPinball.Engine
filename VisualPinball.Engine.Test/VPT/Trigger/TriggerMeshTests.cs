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

using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Trigger
{
	public class TriggerMeshTests : MeshTests
	{
		private readonly FileTableContainer _tc;
		private readonly ObjFile _obj;

		public TriggerMeshTests()
		{
			_tc = FileTableContainer.Load(VpxPath.Trigger);
			_obj = LoadObjFixture(ObjPath.Trigger);
		}

		[Test]
		public void ShouldGenerateMeshesCorrectly()
		{
			AssertObjMesh(_tc.Table, _obj, _tc.Trigger("Button"));
			AssertObjMesh(_tc.Table, _obj, _tc.Trigger("Star"), threshold: 0.001f);
			AssertObjMesh(_tc.Table, _obj, _tc.Trigger("WireA"));
			AssertObjMesh(_tc.Table, _obj, _tc.Trigger("WireB"));
			AssertObjMesh(_tc.Table, _obj, _tc.Trigger("WireC"));
			AssertObjMesh(_tc.Table, _obj, _tc.Trigger("WireD"));
			AssertObjMesh(_tc.Table, _obj, _tc.Trigger("Surface"));

			// the last two fail because vpx ignores thickness when exporting.
			// re-enable when fixed on vp side.
			//AssertObjMesh(_table, _obj, _table.Trigger("ThickWire"));
			//AssertObjMesh(_table, _obj, _table.Trigger("Data"));
		}
	}
}
