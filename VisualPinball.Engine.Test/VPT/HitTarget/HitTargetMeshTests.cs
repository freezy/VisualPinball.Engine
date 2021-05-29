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
		private readonly FileTableContainer _tc;
		private readonly ObjFile _obj;

		public HitTargetMeshTests()
		{
			_tc = FileTableContainer.Load(VpxPath.HitTarget);
			_obj = LoadObjFixture(ObjPath.HitTarget);
		}

		[Test]
		public void ShouldGenerateMesh()
		{
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("DropTargetBeveled"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("DropTargetFlatSimple"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("DropTargetSimple"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("Data"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("HitFatTargetSlim"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("HitFatTargetSquare"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("HitTargetRect"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("HitTargetRound"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("HitTargetSlim"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("ScaledTarget"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("RotatedTarget"));
			AssertObjMesh(_tc.Table, _obj, _tc.HitTarget("DroppedTarget"));
		}
	}
}
