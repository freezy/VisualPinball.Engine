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

namespace VisualPinball.Engine.Test.VPT.Surface
{
	public class SurfaceMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public SurfaceMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Surface);
			_obj = LoadObjFixture(ObjPath.Surface);
		}

		[Test]
		public void ShouldGenerateTopAndSides()
		{
			var surface = _table.Surface("Wall");
			var surfaceMeshes = surface.GetRenderObjects(_table).RenderObjects.Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, surface.Name, surfaceMeshes);
		}

		[Test]
		public void ShouldGenerateOnlyTop()
		{
			var surface = _table.Surface("SideInvisible");
			var surfaceMeshes = surface.GetRenderObjects(_table).RenderObjects
				.Where(ro => ro.IsVisible)
				.Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, surface.Name, surfaceMeshes, 0.001f);
		}

		[Test]
		public void ShouldGenerateOnlySide()
		{
			var surface = _table.Surface("TopInvisible");
			var surfaceMeshes = surface.GetRenderObjects(_table).RenderObjects
				.Where(ro => ro.IsVisible)
				.Select(ro => ro.Mesh).ToArray();
			AssertObjMesh(_obj, surface.Name, surfaceMeshes);
		}
	}
}
