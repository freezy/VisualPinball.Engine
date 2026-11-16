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
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.Test.VPT.Plunger;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class PlungerTests
	{
		[Test]
		public void ShouldWriteImportedPlungerData()
		{
			const string tmpFileName = "ShouldWritePlungerData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Plunger, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableComponent>();
			ta.TableContainer.Export(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			PlungerDataTests.ValidatePlungerData1(writtenTable.Plunger("Plunger1").Data, false);
			PlungerDataTests.ValidatePlungerData2(writtenTable.Plunger("Plunger2").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[UnityTest]
		public IEnumerator PackageRestoresRodAndSpringDeformation()
		{
			var name = "PlungerPackagingTest-" + Guid.NewGuid().ToString("N");
			var path = Path.Combine(Path.GetTempPath(), name + ".vpe");
			var source = new GameObject("Table");
			var material = new Material(Shader.Find("Standard"));
			var baked = new Mesh();
			Mesh rodMesh = null;
			Mesh springMesh = null;
			GameObject editor = null;
			GameObject runtime = null;
			try {
				var table = source.AddComponent<TableComponent>();
				var plunger = new GameObject("Plunger");
				plunger.transform.SetParent(source.transform, false);
				var settings = plunger.AddComponent<PlungerComponent>();
				var collider = plunger.AddComponent<PlungerColliderComponent>();
				collider.Stroke = 60f;
				collider.ParkPosition = 0.3f;
				var rod = new GameObject("Rod");
				rod.transform.SetParent(plunger.transform, false);
				rod.AddComponent<PlungerRodMeshComponent>().TipShape = "0 .34; 2 .6; 5 .7";
				var spring = new GameObject("Spring");
				spring.transform.SetParent(plunger.transform, false);
				spring.AddComponent<PlungerSpringMeshComponent>().SpringLoops = 5f;
				var data = settings.CopyDataTo(settings.InstantiateData(), null, null, false);
				var generator = new PlungerMeshGenerator(data);
				rodMesh = AddPlungerMesh(rod, generator, PlungerMeshGenerator.Rod, material);
				springMesh = AddPlungerMesh(spring, generator, PlungerMeshGenerator.Spring, material);
				new PackageWriter(source).WritePackageSync(path);

				var editorImport = new PackageReader(path).ImportIntoScene(name);
				while (!editorImport.IsCompleted) yield return null;
				editorImport.GetAwaiter().GetResult();
				editor = Object.FindObjectsByType<TableComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
					.Single(t => t != table).gameObject;

				var runtimeImport = new RuntimePackageReader(path).ImportIntoScene();
				while (!runtimeImport.IsCompleted) yield return null;
				runtime = runtimeImport.GetAwaiter().GetResult();

				foreach (var imported in new[] { editor, runtime }) {
					var importedPlunger = imported.GetComponentInChildren<PlungerComponent>();
					AssertDeforms(importedPlunger, importedPlunger.GetComponentInChildren<PlungerRodMeshComponent>(), rodMesh, baked);
					importedPlunger.UpdateParkPosition(collider.ParkPosition);
					AssertDeforms(importedPlunger, importedPlunger.GetComponentInChildren<PlungerSpringMeshComponent>(), springMesh, baked);
				}
			} finally {
				if (editor) Object.DestroyImmediate(editor);
				if (runtime) Object.DestroyImmediate(runtime);
				Object.DestroyImmediate(source);
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(baked);
				if (rodMesh) Object.DestroyImmediate(rodMesh);
				if (springMesh) Object.DestroyImmediate(springMesh);
				AssetDatabase.DeleteAsset("Assets/Resources/" + name);
				File.Delete(path);
			}
		}

		private static Mesh AddPlungerMesh(GameObject part, PlungerMeshGenerator generator, string id, Material material)
		{
			var mesh = generator.GetMesh(9f, id).ToUnityMesh(id);
			part.AddComponent<MeshFilter>().sharedMesh = mesh;
			var renderer = part.AddComponent<SkinnedMeshRenderer>();
			renderer.sharedMesh = mesh;
			renderer.sharedMaterial = material;
			return mesh;
		}

		private static void AssertDeforms(PlungerComponent plunger, PlungerMeshComponent part, Mesh source, Mesh baked)
		{
			var renderer = part.GetComponent<SkinnedMeshRenderer>();
			Assert.That(renderer, Is.Not.Null);
			Assert.That(part.GetComponent<MeshRenderer>(), Is.Null);
			Assert.That(renderer.sharedMesh.vertexCount, Is.EqualTo(source.vertexCount));
			Assert.That(renderer.sharedMesh.bounds.center.y, Is.EqualTo(source.bounds.center.y).Within(1e-6));
			Assert.That(renderer.sharedMesh.blendShapeCount, Is.EqualTo(1));
			Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(0.3f).Within(1e-6));
			var expectedDeltas = new Vector3[source.vertexCount];
			source.GetBlendShapeFrameVertices(0, source.GetBlendShapeFrameCount(0) - 1, expectedDeltas, null, null);
			Assert.That(expectedDeltas[0].z, Is.GreaterThan(0f));
			plunger.UpdateParkPosition(0f);
			renderer.BakeMesh(baked);
			var atRest = baked.vertices[0];
			plunger.UpdateParkPosition(1f);
			renderer.BakeMesh(baked);
			Assert.That(baked.vertices[0].z - atRest.z, Is.EqualTo(expectedDeltas[0].z).Within(1e-5));
		}
	}
}
