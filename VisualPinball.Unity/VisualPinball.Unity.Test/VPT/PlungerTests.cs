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
using VisualPinball.Engine.Test.VPT.Plunger;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class PlungerTests
	{
		[Test]
		public void ShouldWriteImportedPlungerData()
		{
			const string tmpFileName = "ShouldWritePlungerData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Plunger, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableAuthoring>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			PlungerDataTests.ValidatePlungerData1(writtenTable.Plunger("Plunger1").Data, false);
			PlungerDataTests.ValidatePlungerData2(writtenTable.Plunger("Plunger2").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}
	}
}
