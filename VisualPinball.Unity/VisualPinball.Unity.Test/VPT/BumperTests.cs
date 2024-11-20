// Visual Pinball Engine
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

using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.Test.VPT.Bumper;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class BumperTests
	{
		[Test]
		public void ShouldWriteImportedBumperData()
		{
			const string tmpFileName = "ShouldWriteBumperData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Bumper, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableComponent>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			BumperDataTests.ValidateTableData(writtenTable.Bumper("Bumper1").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldWriteUpdatedBumperData()
		{
			const string tmpFileName = "ShouldWriteUpdatedBumperData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Bumper, options: ConvertOptions.SkipNone);

			var bumper = go.transform.GetComponentsInChildren<BumperComponent>().First(c => c.gameObject.name == "Bumper2");
			var bumperAuth = bumper.GetComponent<BumperComponent>();

			bumperAuth.Position = new Vector2(128f, 255f);

			go.GetComponent<TableComponent>().TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			var writtenBumperData = writtenTable.Bumper("Bumper2").Data;

			Assert.AreEqual(128f, writtenBumperData.Center.X);
			Assert.AreEqual(255f, writtenBumperData.Center.Y);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldOnlyImportRing()
		{
			var table = new TableBuilder()
				.AddBumper("Bumper")
				.Build();

			table.Bumper("Bumper").Data.IsBaseVisible = false;
			table.Bumper("Bumper").Data.IsCapVisible = false;
			table.Bumper("Bumper").Data.IsSocketVisible = false;

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);

			var baseGo = go.transform.Find("Playfield/Bumpers/Bumper/Base");
			var capGo = go.transform.Find("Playfield/Bumpers/Bumper/Cap");
			var socketGo = go.transform.Find("Playfield/Bumpers/Bumper/Skirt");
			var ringGo = go.transform.Find("Playfield/Bumpers/Bumper/Ring");

			Assert.IsFalse(baseGo.gameObject.activeInHierarchy);
			Assert.IsFalse(capGo.gameObject.activeInHierarchy);
			Assert.IsFalse(socketGo.gameObject.activeInHierarchy);
			Assert.IsTrue(ringGo.gameObject.activeInHierarchy);

			Object.DestroyImmediate(go);
		}
	}
}
