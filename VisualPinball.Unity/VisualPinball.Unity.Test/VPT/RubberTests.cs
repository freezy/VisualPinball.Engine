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

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.Test.VPT.Rubber;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

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
			ta.TableContainer.Export(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			RubberDataTests.ValidateRubberData1(writtenTable.Rubber("Rubber1").Data);
			RubberDataTests.ValidateRubberData2(writtenTable.Rubber("Rubber2").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[UnityTest]
		public IEnumerator ShouldKeepGeneratedSplinesOutOfPackageRoundTrips()
		{
			var firstPackage = Path.Combine(Path.GetTempPath(),
				$"vpe-spline-package-{Guid.NewGuid():N}.vpe");
			var secondPackage = Path.Combine(Path.GetTempPath(),
				$"vpe-spline-package-{Guid.NewGuid():N}.vpe");
			GameObject source = null;
			GameObject imported = null;
			try {
				source = CreatePackageTable();
				Object.DestroyImmediate(source.GetComponentInChildren<GeneratedDragPointSplineComponent>());
				new PackageWriter(source).WritePackageSync(firstPackage);
				Assert.That(source.GetComponentInChildren<GeneratedDragPointSplineComponent>(), Is.Null);
				AssertPackageHasNoSplineNodes(firstPackage);

				var importTask = new RuntimePackageReader(firstPackage).ImportIntoScene();
				while (!importTask.IsCompleted) {
					yield return null;
				}
				if (importTask.IsFaulted) {
					throw importTask.Exception!.GetBaseException();
				}
				imported = importTask.Result;
				AssertFunctionalGeneratedSplines(imported);

				new PackageWriter(imported).WritePackageSync(secondPackage);
				AssertPackageHasNoSplineNodes(secondPackage);
				AssertFunctionalGeneratedSplines(imported);
			}
			finally {
				if (source) {
					Object.DestroyImmediate(source);
				}
				if (imported) {
					Object.DestroyImmediate(imported);
				}
				File.Delete(firstPackage);
				File.Delete(secondPackage);
			}
		}

		private static void AssertPackageHasNoSplineNodes(string path)
		{
			using var storage = PackageApi.StorageManager.OpenStorage(path);
			var sceneData = storage.GetFolder(PackageApi.TableFolder)
				.GetFile(PackageApi.SceneFile).GetData();
			var glbContents = Encoding.UTF8.GetString(sceneData);
			Assert.That(glbContents, Does.Not.Match("\\\"name\\\"\\s*:\\s*\\\"Spline\\\""));
		}

		private static GameObject CreatePackageTable()
		{
			var table = new GameObject("Table");
			table.AddComponent<TableComponent>();
			var rubberObject = new GameObject("Rubber");
			rubberObject.transform.SetParent(table.transform, false);
			var rubber = rubberObject.AddComponent<RubberComponent>();
			rubber.DragPoints = new[] {
				new DragPointData(-100f, -100f),
				new DragPointData(-100f, 100f),
				new DragPointData(100f, 100f),
				new DragPointData(100f, -100f),
			};
			return table;
		}

		private static void AssertFunctionalGeneratedSplines(GameObject table)
		{
			var rubbers = table.GetComponentsInChildren<RubberComponent>(true);
			Assert.That(rubbers, Is.Not.Empty);
			foreach (var rubber in rubbers) {
				var generated = rubber.GetComponentsInChildren<GeneratedDragPointSplineComponent>(true);
				Assert.That(generated, Has.Length.EqualTo(1), rubber.name);
				var spline = generated[0].GetComponent<DragPointSplineComponent>();
				Assert.That(spline, Is.Not.Null, rubber.name);
				Assert.That(spline!.Container, Is.Not.Null, rubber.name);
				Assert.That(spline.DragPoints, Is.Not.Empty, rubber.name);
				Assert.That(rubber.DragPointSpline, Is.SameAs(spline), rubber.name);
				Assert.That(rubber.transform.Cast<Transform>()
					.Count(child => child.name == "Spline"
						&& !child.GetComponent<GeneratedDragPointSplineComponent>()), Is.Zero, rubber.name);
			}
		}

	}
}
