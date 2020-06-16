﻿using JeremyAnsel.Media.WavefrontObj;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;

namespace VisualPinball.Engine.Test.VPT.Table
{
	public class TableMeshTests : MeshTests
	{
		private readonly Engine.VPT.Table.Table _table;
		private readonly ObjFile _obj;

		public TableMeshTests()
		{
			_table = Engine.VPT.Table.Table.Load(VpxPath.Table);
			_obj = LoadObjFixture(ObjPath.Table);
		}

		[Test]
		public void ShouldGeneratePlayfieldCorrectly()
		{
			var tableMesh = _table.GetRenderObjects(_table).RenderObjects[0].Mesh;
			AssertObjMesh(_obj, tableMesh);
		}
	}
}
