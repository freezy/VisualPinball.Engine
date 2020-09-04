﻿// Visual Pinball Engine
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

namespace VisualPinball.Engine.Test.VPT.HitTarget
{
	public class HitTargetMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public HitTargetMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.HitTarget);
			_obj = LoadObjFixture(ObjPath.HitTarget);
		}

		[Test]
		public void ShouldGenerateMesh()
		{
			AssertObjMesh(_table, _obj, _table.HitTarget("DropTargetBeveled"));
			AssertObjMesh(_table, _obj, _table.HitTarget("DropTargetFlatSimple"));
			AssertObjMesh(_table, _obj, _table.HitTarget("DropTargetSimple"));
			AssertObjMesh(_table, _obj, _table.HitTarget("Data"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitFatTargetSlim"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitFatTargetSquare"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitTargetRect"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitTargetRound"));
			AssertObjMesh(_table, _obj, _table.HitTarget("HitTargetSlim"));
			AssertObjMesh(_table, _obj, _table.HitTarget("ScaledTarget"));
			AssertObjMesh(_table, _obj, _table.HitTarget("RotatedTarget"));
			AssertObjMesh(_table, _obj, _table.HitTarget("DroppedTarget"));
		}
	}
}
