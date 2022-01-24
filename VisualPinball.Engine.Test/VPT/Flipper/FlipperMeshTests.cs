﻿// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Flipper
{
	public class FlipperMeshTests : MeshTests
	{
		private readonly FileTableContainer _tc;
		private readonly ObjFile _obj;

		public FlipperMeshTests()
		{
			_tc = FileTableContainer.Load(VpxPath.Flipper);
			_obj = LoadObjFixture(ObjPath.Flipper);
		}

		[Test]
		public void ShouldGenerateFatMesh()
		{
			var flipper = _tc.Flipper("FatFlipper");
			var flipperMeshes = GetMeshes(_tc.Table, flipper, FlipperMeshGenerator.Base, FlipperMeshGenerator.Rubber);
			foreach (var flipperMesh in flipperMeshes) {
				AssertObjMesh(_obj, flipperMesh, $"{flipper.Name}{flipperMesh.Name}", 0.00013f);
			}
		}

		[Test]
		public void ShouldGenerateFatRubberMesh()
		{
			var flipper = _tc.Flipper("FatRubberFlipper");
			var flipperMeshes = GetMeshes(_tc.Table, flipper, FlipperMeshGenerator.Base, FlipperMeshGenerator.Rubber);
			foreach (var flipperMesh in flipperMeshes) {
				AssertObjMesh(_obj, flipperMesh, $"{flipper.Name}{flipperMesh.Name}", threshold: 0.00015f);
			}
		}

		[Test]
		public void ShouldGenerateFlipperOnSurfaceMesh()
		{
			var flipper = _tc.Flipper("SurfaceFlipper");
			var flipperMeshes = GetMeshes(_tc.Table, flipper, FlipperMeshGenerator.Base, FlipperMeshGenerator.Rubber);
			foreach (var flipperMesh in flipperMeshes) {
				AssertObjMesh(_obj, flipperMesh, $"{flipper.Name}{flipperMesh.Name}");
			}
		}
	}
}
