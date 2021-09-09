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
using VisualPinball.Engine.Test.VPT.Rubber;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class RubberTests
	{
		[Test]
		public void ShouldWriteImportedRubberData()
		{
			const string tmpFileName = "ShouldWriteRubberData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Rubber, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableComponent>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			RubberDataTests.ValidateRubberData1(writtenTable.Rubber("Rubber1").Data);
			RubberDataTests.ValidateRubberData2(writtenTable.Rubber("Rubber2").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

	}
}
