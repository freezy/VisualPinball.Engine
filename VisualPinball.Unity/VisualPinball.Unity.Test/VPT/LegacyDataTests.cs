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
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.Test.VPT.Collection;
using VisualPinball.Engine.Test.VPT.Decal;
using VisualPinball.Engine.Test.VPT.DispReel;
using VisualPinball.Engine.Test.VPT.Flasher;
using VisualPinball.Engine.Test.VPT.LightSeq;
using VisualPinball.Engine.Test.VPT.TextBox;
using VisualPinball.Engine.Test.VPT.Timer;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class LegacyDataTests
	{
		[Test]
		public void ShouldWriteImportedCollectionData()
		{
			const string tmpFileName = "ShouldWriteCollectionData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Collection, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableAuthoring>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);

			var data = writtenTable.Collections.First(c => c.Name == "Flippers");
			CollectionDataTests.ValidateTableData(data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldWriteImportedDecalData()
		{
			const string tmpFileName = "ShouldWriteDecalData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Decal, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableAuthoring>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			DecalDataTests.ValidateDecal0(writtenTable.Decal(0).Data);
			DecalDataTests.ValidateDecal1(writtenTable.Decal(1).Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldWriteImportedDispReelData()
		{
			const string tmpFileName = "ShouldWriteDispReelData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.DispReel, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableAuthoring>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			DispReelDataTests.ValidateDispReel1(writtenTable.DispReel("Reel1").Data);
			DispReelDataTests.ValidateDispReel2(writtenTable.DispReel("Reel2").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldWriteImportedFlasherData()
		{
			const string tmpFileName = "ShouldWriteFlasherData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Flasher, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableAuthoring>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			FlasherDataTests.ValidateFlasher(writtenTable.Flasher("Data").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldWriteImportedLightSeqData()
		{
			const string tmpFileName = "ShouldWriteLightSeqData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.LightSeq, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableAuthoring>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			LightSeqDataTests.ValidateLightSeqData(writtenTable.LightSeq("LightSeq001").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldWriteImportedTextBoxData()
		{
			const string tmpFileName = "ShouldWriteTextBoxData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.TextBox, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableAuthoring>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			TextBoxDataTests.ValidateTextBoxData(writtenTable.TextBox("TextBox001").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldWriteImportedTimerData()
		{
			const string tmpFileName = "ShouldWriteTimerData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Timer, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableAuthoring>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			TimerDataTests.ValidateTimerData1(writtenTable.Timer("Timer1").Data);
			TimerDataTests.ValidateTimerData2(writtenTable.Timer("Timer2").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}
	}
}
