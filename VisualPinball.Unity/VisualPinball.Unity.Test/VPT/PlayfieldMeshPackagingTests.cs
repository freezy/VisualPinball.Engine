// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using VisualPinball.Unity.Playfield;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class PlayfieldMeshPackagingTests
	{
		[Test]
		public void ShouldBePackedSoACustomMeshSurvivesExport()
		{
			Assert.That(typeof(PlayfieldMeshComponent).GetCustomAttribute<PackAsAttribute>(), Is.Not.Null);
			Assert.That(typeof(IPackable).IsAssignableFrom(typeof(PlayfieldMeshComponent)), Is.True);
		}

		[Test]
		public void AutoGenerateRoundTrips()
		{
			var gameObject = new GameObject("Playfield");
			try {
				var mesh = gameObject.AddComponent<PlayfieldMeshComponent>();
				mesh.AutoGenerate = false;
				var bytes = mesh.Pack();

				mesh.AutoGenerate = true;
				mesh.Unpack(bytes);

				Assert.That(mesh.AutoGenerate, Is.False);
			}
			finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[UnityTest]
		public IEnumerator ShouldKeepAnAuthoredPlayfieldMeshThroughAPackageRoundTrip()
		{
			var tableName = "PlayfieldMeshPackagingTest-" + Guid.NewGuid().ToString("N");
			var packagePath = Path.Combine(Path.GetTempPath(), tableName + ".vpe");
			GameObject source = null;
			GameObject imported = null;
			Mesh authored = null;
			Material material = null;
			try {
				source = new GameObject(tableName);
				source.AddComponent<TableComponent>();
				var playfieldGo = new GameObject("Playfield");
				playfieldGo.transform.SetParent(source.transform, false);
				playfieldGo.AddComponent<PlayfieldComponent>();

				// an authored playfield: a quad with a notch cut out, nothing like the generated plane
				authored = new Mesh { name = "Authored Playfield" };
				authored.vertices = new[] {
					new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, 1.1f),
					new Vector3(0.3f, 0f, 1.1f), new Vector3(0.25f, 0f, 0.9f), new Vector3(0.2f, 0f, 1.1f), new Vector3(0f, 0f, 1.1f),
				};
				authored.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 6, 5 };
				authored.RecalculateNormals();
				authored.RecalculateBounds();
				playfieldGo.AddComponent<MeshFilter>().sharedMesh = authored;
				material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = "Authored Playfield" };
				playfieldGo.AddComponent<MeshRenderer>().sharedMaterial = material;
				var playfieldMesh = playfieldGo.AddComponent<PlayfieldMeshComponent>();
				playfieldMesh.AutoGenerate = false;
				var sourceBounds = authored.bounds;
				var sourceTriangles = authored.triangles.Length / 3;

				new PackageWriter(source).WritePackageSync(packagePath);
				var importTask = new PackageReader(packagePath).ImportIntoScene(tableName);
				while (!importTask.IsCompleted) { yield return null; }
				if (importTask.IsFaulted) { throw importTask.Exception!.GetBaseException(); }
				foreach (var table in Object.FindObjectsByType<TableComponent>(FindObjectsSortMode.None)) {
					if (table.gameObject != source && table.name == tableName) { imported = table.gameObject; break; }
				}
				Assert.That(imported, Is.Not.Null);

				var restored = imported.GetComponentInChildren<PlayfieldMeshComponent>(true);
				Assert.That(restored, Is.Not.Null, "the playfield mesh component must survive the package round trip");
				Assert.That(restored.AutoGenerate, Is.False);
				var restoredFilter = restored.GetComponent<MeshFilter>();
				Assert.That(restoredFilter, Is.Not.Null, "the playfield keeps its mesh filter");
				var restoredMesh = restoredFilter.sharedMesh;
				Assert.That(restoredMesh, Is.Not.Null, "the authored playfield mesh must survive the package round trip");
				Assert.That(restoredMesh.triangles.Length / 3, Is.EqualTo(sourceTriangles), "the authored mesh, not a generated plane");
				Assert.That(restoredMesh.bounds.size.x, Is.EqualTo(sourceBounds.size.x).Within(1e-4f));
				Assert.That(restoredMesh.bounds.size.z, Is.EqualTo(sourceBounds.size.z).Within(1e-4f));
			}
			finally {
				if (source) { Object.DestroyImmediate(source); }
				if (imported) { Object.DestroyImmediate(imported); }
				if (authored) { Object.DestroyImmediate(authored); }
				if (material) { Object.DestroyImmediate(material); }
				AssetDatabase.DeleteAsset("Assets/Resources/" + tableName);
				File.Delete(packagePath);
			}
		}
	}
}
