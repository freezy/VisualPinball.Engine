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

namespace VisualPinball.Engine.Test.VPT.Bumper
{
	public class BumperMeshTests : MeshTests
	{
		private readonly TableContainer _table;
		private readonly ObjFile _obj;

		public BumperMeshTests()
		{
			_table = TableContainer.Load(VpxPath.Bumper);
			_obj = LoadObjFixture(ObjPath.Bumper);
		}

		[Test]
		public void ShouldGenerateMesh()
		{
			AssertObjMesh(_table.Table, _obj, _table.Bumper("Bumper2"), (item, mesh) => $"{item.Name}{mesh.Name}");
		}
	}
}
