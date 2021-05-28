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

namespace VisualPinball.Engine.Test.VPT.HitTarget
{
	public class HitTargetMeshTests : MeshTests
	{
		private readonly TableHolder _th;
		private readonly ObjFile _obj;

		public HitTargetMeshTests()
		{
			_th = TableHolder.Load(VpxPath.HitTarget);
			_obj = LoadObjFixture(ObjPath.HitTarget);
		}

		[Test]
		public void ShouldGenerateMesh()
		{
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("DropTargetBeveled"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("DropTargetFlatSimple"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("DropTargetSimple"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("Data"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("HitFatTargetSlim"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("HitFatTargetSquare"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("HitTargetRect"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("HitTargetRound"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("HitTargetSlim"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("ScaledTarget"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("RotatedTarget"));
			AssertObjMesh(_th.Table, _obj, _th.HitTarget("DroppedTarget"));
		}
	}
}
