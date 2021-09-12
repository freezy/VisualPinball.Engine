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

namespace VisualPinball.Engine.Test.VPT.Rubber
{
	public class RubberMeshTest : MeshTests
	{
		private readonly FileTableContainer _tc;
		private readonly ObjFile _obj;

		public RubberMeshTest()
		{
			_tc = FileTableContainer.Load(VpxPath.Rubber);
			_obj = LoadObjFixture(ObjPath.Rubber);
		}

		//[Test] todo fix
		public void ShouldGenerateMesh()
		{
			var rubberMesh = _tc.Rubber("Rubber2").GetRenderObjects(_tc.Table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, rubberMesh, threshold: 0.00015f);
		}

		// [Test] todo fix
		public void ShouldGenerateThickMesh()
		{
			var rubberMesh = _tc.Rubber("Rubber1").GetRenderObjects(_tc.Table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, rubberMesh, threshold: 0.001f);
		}
	}
}
