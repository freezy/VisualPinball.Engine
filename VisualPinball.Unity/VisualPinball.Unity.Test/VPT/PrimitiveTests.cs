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

using System.IO;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.Test.VPT.Primitive;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;
using Assert = Unity.Assertions.Assert;

namespace VisualPinball.Unity.Test
{
	public class PrimitiveTests
	{
		[Test]
		public void ShouldWriteImportedPrimitiveData()
		{
			const string tmpFileName = "ShouldWritePrimitiveData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Primitive, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableComponent>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			PrimitiveDataTests.ValidatePrimitiveData(writtenTable.Primitive("Cube").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldWriteImportedMesh()
		{
			const string primitiveName = "Books";
			const string tmpFileName = "ShouldWriteImportedMesh.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Primitive, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableComponent>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			var writtenMesh = writtenTable.Primitive(primitiveName).GetMesh();

			var table = FileTableContainer.Load(VpxPath.Primitive);
			var originalMesh = table.Primitive(primitiveName).GetMesh();

			Assert.AreEqual(originalMesh, writtenMesh);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

	}
}
