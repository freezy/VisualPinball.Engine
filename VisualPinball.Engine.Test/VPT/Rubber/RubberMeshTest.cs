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

namespace VisualPinball.Engine.Test.VPT.Rubber
{
	public class RubberMeshTest : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public RubberMeshTest()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Rubber);
			_obj = LoadObjFixture(ObjPath.Rubber);
		}

		[Test]
		public void ShouldGenerateMesh()
		{
			var rubberMesh = _table.Rubber("Rubber2").GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, rubberMesh, threshold: 0.00015f);
		}

		[Test]
		public void ShouldGenerateThickMesh()
		{
			var rubberMesh = _table.Rubber("Rubber1").GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, rubberMesh, threshold: 0.001f);
		}
	}
}
