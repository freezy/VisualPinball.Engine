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

using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.Kicker
{
	public class KickerMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public KickerMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Kicker);
			_obj = LoadObjFixture(ObjPath.Kicker);
		}

		[Test]
		public void ShouldGenerateMeshesCorrectly()
		{
			AssertObjMesh(_table, _obj, _table.Kicker("Cup"));
			AssertObjMesh(_table, _obj, _table.Kicker("Cup2"));
			AssertObjMesh(_table, _obj, _table.Kicker("Gottlieb"), threshold: 0.00015f);
			AssertObjMesh(_table, _obj, _table.Kicker("Hole"));
			AssertObjMesh(_table, _obj, _table.Kicker("HoleSimple"));
			AssertObjMesh(_table, _obj, _table.Kicker("Williams"), threshold: 0.001f);
			AssertObjMesh(_table, _obj, _table.Kicker("Scaled"));
			AssertObjMesh(_table, _obj, _table.Kicker("Rotated"), threshold: 0.00015f);
			AssertObjMesh(_table, _obj, _table.Kicker("Surface"));
			AssertObjMesh(_table, _obj, _table.Kicker("Data"), threshold: 0.00015f);
		}
	}
}
