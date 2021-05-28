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
		private readonly TableHolder _th;
		private readonly ObjFile _obj;

		public KickerMeshTests()
		{
			_th = TableHolder.Load(VpxPath.Kicker);
			_obj = LoadObjFixture(ObjPath.Kicker);
		}

		[Test]
		public void ShouldGenerateMeshesCorrectly()
		{
			AssertObjMesh(_th.Table, _obj, _th.Kicker("Cup"));
			AssertObjMesh(_th.Table, _obj, _th.Kicker("Cup2"));
			AssertObjMesh(_th.Table, _obj, _th.Kicker("Gottlieb"), threshold: 0.00015f);
			AssertObjMesh(_th.Table, _obj, _th.Kicker("Hole"));
			AssertObjMesh(_th.Table, _obj, _th.Kicker("HoleSimple"));
			AssertObjMesh(_th.Table, _obj, _th.Kicker("Williams"), threshold: 0.001f);
			AssertObjMesh(_th.Table, _obj, _th.Kicker("Scaled"));
			AssertObjMesh(_th.Table, _obj, _th.Kicker("Rotated"), threshold: 0.00015f);
			AssertObjMesh(_th.Table, _obj, _th.Kicker("Surface"));
			AssertObjMesh(_th.Table, _obj, _th.Kicker("Data"), threshold: 0.00015f);
		}
	}
}
