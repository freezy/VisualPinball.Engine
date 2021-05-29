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

namespace VisualPinball.Engine.Test.VPT.Kicker
{
	public class KickerMeshTests : MeshTests
	{
		private readonly TableContainer _tc;
		private readonly ObjFile _obj;

		public KickerMeshTests()
		{
			_tc = TableContainer.Load(VpxPath.Kicker);
			_obj = LoadObjFixture(ObjPath.Kicker);
		}

		[Test]
		public void ShouldGenerateMeshesCorrectly()
		{
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("Cup"));
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("Cup2"));
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("Gottlieb"), threshold: 0.00015f);
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("Hole"));
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("HoleSimple"));
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("Williams"), threshold: 0.001f);
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("Scaled"));
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("Rotated"), threshold: 0.00015f);
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("Surface"));
			AssertObjMesh(_tc.Table, _obj, _tc.Kicker("Data"), threshold: 0.00015f);
		}
	}
}
