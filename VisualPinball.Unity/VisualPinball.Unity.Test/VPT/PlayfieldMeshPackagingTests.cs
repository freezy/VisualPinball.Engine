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
using Unity.Collections;
using Unity.Mathematics;
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

		[TestCase(true)]
		[TestCase(false)]
		public void MissingMeshFilterDoesNotBreakColliderExport(bool autoGenerate)
		{
			var target = new GameObject("Playfield");
			try {
				var mesh = target.AddComponent<PlayfieldMeshComponent>(); mesh.AutoGenerate = autoGenerate;
				Assert.That(target.TryGetComponent<MeshFilter>(out _), Is.False);
				Assert.That(mesh.NumColliderMeshes, Is.Zero); Assert.That(mesh.GetColliderMesh(0), Is.Null);
				Assert.DoesNotThrow(() => mesh.UnpackReferences(null, target.transform, null, null));
				Assert.DoesNotThrow(() => mesh.UnpackReferences(Array.Empty<byte>(), target.transform, null, null));
			} finally { Object.DestroyImmediate(target); }
		}

		[UnityTest]
		public IEnumerator ShouldKeepAnAuthoredPlayfieldMeshThroughAPackageRoundTrip() => RoundTrip(true, true, false);

		[UnityTest]
		public IEnumerator CollisionOnlyMeshSurvivesEditorImport() => RoundTrip(false, false, false);

		[UnityTest]
		public IEnumerator DisabledRendererMeshSurvivesEditorImport() => RoundTrip(true, false, false);

		[UnityTest]
		public IEnumerator CollisionOnlyMeshSurvivesRuntimeImport() => RoundTrip(false, false, true);

		[UnityTest]
		public IEnumerator DisabledRendererMeshSurvivesRuntimeImport() => RoundTrip(true, false, true);

		[UnityTest]
		public IEnumerator MultipleCollisionMeshesSurviveEditorImport() => RoundTrip(false, false, false, true);

		private IEnumerator RoundTrip(bool hasRenderer, bool rendererEnabled, bool runtime, bool extraCollider = false)
		{
			var tableName = "PlayfieldMeshPackagingTest-" + Guid.NewGuid().ToString("N");
			var packagePath = Path.Combine(Path.GetTempPath(), tableName + ".vpe");
			GameObject source = null;
			GameObject imported = null;
			Mesh authored = null;
			Mesh secondaryMesh = null;
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
				authored.uv = Array.ConvertAll(authored.vertices, v => new Vector2(v.x, v.z));
				authored.RecalculateNormals();
				authored.RecalculateBounds();
				playfieldGo.AddComponent<MeshFilter>().sharedMesh = authored;
				if (hasRenderer) {
					material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = "Authored Playfield" };
					var renderer = playfieldGo.AddComponent<MeshRenderer>();
					renderer.sharedMaterial = material; renderer.enabled = rendererEnabled;
				}
				playfieldGo.AddComponent<PlayfieldColliderComponent>();
				var playfieldMesh = playfieldGo.AddComponent<PlayfieldMeshComponent>();
				playfieldMesh.AutoGenerate = false;
				if (hasRenderer) playfieldGo.GetComponent<MeshRenderer>().enabled = rendererEnabled;
				if (extraCollider) {
					var secondary = new GameObject("Secondary collider"); secondary.transform.SetParent(source.transform, false);
					secondaryMesh = Object.Instantiate(authored); secondaryMesh.triangles = new[] { 0, 2, 1 };
					secondary.AddComponent<PrimitiveComponent>(); secondary.AddComponent<MeshFilter>().sharedMesh = secondaryMesh;
					secondary.AddComponent<PrimitiveColliderComponent>();
				}
				var sourceBounds = authored.bounds;
				var sourceTriangles = authored.triangles.Length / 3;

				Assert.That(playfieldMesh.NumColliderMeshes, Is.EqualTo(hasRenderer && rendererEnabled ? 0 : 1), "only hidden custom geometry uses the collider payload");
				new PackageWriter(source).WritePackageSync(packagePath);
				Assert.That(playfieldGo.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(authored), "export leaves the source mesh in place");
				Assert.That(playfieldGo.GetComponent<MeshRenderer>() != null, Is.EqualTo(hasRenderer), "export must not add a renderer to collision-only geometry");
				if (hasRenderer) Assert.That(playfieldGo.GetComponent<MeshRenderer>().enabled, Is.EqualTo(rendererEnabled));
				if (runtime) {
					var load = new RuntimePackageReader(packagePath).ImportIntoScene();
					while (!load.IsCompleted) yield return null;
					imported = load.GetAwaiter().GetResult();
				} else {
					var importTask = new PackageReader(packagePath).ImportIntoScene(tableName);
					while (!importTask.IsCompleted) { yield return null; }
					if (importTask.IsFaulted) { throw importTask.Exception!.GetBaseException(); }
					foreach (var table in Object.FindObjectsByType<TableComponent>(FindObjectsSortMode.None)) {
						if (table.gameObject != source && table.name == tableName) { imported = table.gameObject; break; }
					}
				}
				Assert.That(imported, Is.Not.Null);
				if (extraCollider) {
					var secondary = imported.GetComponentInChildren<PrimitiveComponent>(true);
					Assert.That(secondary, Is.Not.Null);
					Assert.That(secondary.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.EqualTo(1), "the second GUID resolves its distinct geometry");
				}

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
				Assert.That(restoredMesh.isReadable, Is.True, "runtime physics must be able to read the restored triangles");
				if (!hasRenderer || !rendererEnabled) Assert.That(restored.GetComponent<MeshRenderer>(), Is.Null, "collision-only geometry stays invisible");
				var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
				var colliders = new ColliderReference(ref transforms, Allocator.Temp);
				try {
					var api = new PlayfieldApi(restored.gameObject, null, null);
					((IApiColliderGenerator)api).CreateColliders(ref colliders, float4x4.identity, 0f);
					Assert.That(colliders.TriangleColliders.Length, Is.EqualTo(sourceTriangles));
					Assert.That(colliders.PlaneColliders.Length, Is.EqualTo(1), "only the glass plane remains; no solid playfield fallback");
				} finally { colliders.Dispose(); transforms.Dispose(); }
			}
			finally {
				if (source) { Object.DestroyImmediate(source); }
				if (imported) { Object.DestroyImmediate(imported); }
				if (authored) { Object.DestroyImmediate(authored); }
				if (secondaryMesh) { Object.DestroyImmediate(secondaryMesh); }
				if (material) { Object.DestroyImmediate(material); }
				AssetDatabase.DeleteAsset("Assets/Resources/" + tableName);
				File.Delete(packagePath);
			}
		}
	}
}
