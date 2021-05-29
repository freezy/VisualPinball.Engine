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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Surface
{
	public class SurfaceMeshTests : MeshTests
	{
		private readonly TableContainer _tc;
		private readonly ObjFile _obj;

		public SurfaceMeshTests()
		{
			_tc = TableContainer.Load(VpxPath.Surface);
			_obj = LoadObjFixture(ObjPath.Surface);
		}

		[Test]
		public void ShouldGenerateTopAndSides()
		{
			var surface = _tc.Surface("Wall");
			var surfaceMeshes = surface.GetRenderObjects(_tc.Table).RenderObjects.Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, surface.Name, surfaceMeshes);
		}

		[Test]
		public void ShouldGenerateOnlyTop()
		{
			var surface = _tc.Surface("SideInvisible");
			var surfaceMeshes = surface.GetRenderObjects(_tc.Table).RenderObjects
				.Where(ro => ro.IsVisible)
				.Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, surface.Name, surfaceMeshes, 0.001f);
		}

		[Test]
		public void ShouldGenerateOnlySide()
		{
			var surface = _tc.Surface("TopInvisible");
			var surfaceMeshes = surface.GetRenderObjects(_tc.Table).RenderObjects
				.Where(ro => ro.IsVisible)
				.Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, surface.Name, surfaceMeshes);
		}
	}
}
