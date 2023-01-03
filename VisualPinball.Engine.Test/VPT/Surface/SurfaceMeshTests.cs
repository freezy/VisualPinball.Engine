﻿// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Surface
{
	public class SurfaceMeshTests : MeshTests
	{
		private readonly FileTableContainer _tc;
		private readonly ObjFile _obj;

		public SurfaceMeshTests()
		{
			_tc = FileTableContainer.Load(VpxPath.Surface);
			_obj = LoadObjFixture(ObjPath.Surface);
		}

		[Test]
		public void ShouldGenerateTopAndSides()
		{
			var surface = _tc.Surface("Wall");
			var surfaceMeshes = GetMeshes(_tc.Table, surface, SurfaceMeshGenerator.Side, SurfaceMeshGenerator.Top);

			AssertObjMesh(_obj, surface.Name, surfaceMeshes);
		}
	}
}
