﻿// Visual Pinball Engine
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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Gate
{
	public class GateMeshTests : MeshTests
	{
		private readonly TableContainer _tc;
		private readonly ObjFile _obj;

		public GateMeshTests()
		{
			_tc = TableContainer.Load(VpxPath.Gate);
			_obj = LoadObjFixture(ObjPath.Gate);
		}

		[Test]
		public void ShouldGenerateBracketMeshes()
		{
			string GetName(IRenderable item, Mesh mesh) => $"{item.Name}{mesh.Name}";
			AssertObjMesh(_tc.Table, _obj, _tc.Gate("LongPlate"), GetName, 0.00015f);
			AssertObjMesh(_tc.Table, _obj, _tc.Gate("Plate"), GetName);
			AssertObjMesh(_tc.Table, _obj, _tc.Gate("WireRectangle"), GetName);
			AssertObjMesh(_tc.Table, _obj, _tc.Gate("WireW"), GetName, 0.00015f);
			AssertObjMesh(_tc.Table, _obj, _tc.Gate("TransformedGate"), GetName);
			AssertObjMesh(_tc.Table, _obj, _tc.Gate("SurfaceGate"), GetName);
		}

		[Test]
		public void ShouldGenerateMeshWithoutBracket()
		{
			AssertObjMesh(_obj, _tc.Gate("NoBracketGate").GetRenderObjects(_tc.Table).RenderObjects[0].Mesh, "NoBracketGateWire");
			AssertNoObjMesh(_obj, "NoBracketGateBracket");
		}
	}
}
