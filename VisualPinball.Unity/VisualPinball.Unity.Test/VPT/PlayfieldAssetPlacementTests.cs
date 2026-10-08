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
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class PlayfieldAssetPlacementTests
	{
		private static readonly MethodInfo TryHit = typeof(AssetBrowser).Assembly
			.GetType("VisualPinball.Unity.Editor.PlayfieldAssetPlacement")
			.GetMethod("TryHit", BindingFlags.Static | BindingFlags.NonPublic);

		[Test]
		public void MountingPointIsOnTopOfSlabRatherThanItsCenterOrBottom()
		{
			var bounds = new Bounds(new Vector3(0, -0.00635f, 0), new Vector3(0.28f, 0.0127f, 0.28f));
			AssertHit(new Ray(new Vector3(0.03f, 1, -0.02f), Vector3.down), Matrix4x4.identity, bounds, new Vector3(0.03f, 0, -0.02f));
		}

		[Test]
		public void TiltedScaledAndTranslatedPlayfieldUsesItsLocalTopPlane()
		{
			var bounds = new Bounds(Vector3.zero, new Vector3(1, 0.02f, 2));
			var matrix = Matrix4x4.TRS(new Vector3(3, 2, -4), Quaternion.Euler(12, 35, -5), new Vector3(2, 0.7f, 1.5f));
			var localPoint = new Vector3(-0.2f, bounds.max.y, 0.6f);
			var ray = new Ray(matrix.MultiplyPoint3x4(localPoint + Vector3.up), matrix.MultiplyVector(Vector3.down).normalized);
			AssertHit(ray, matrix, bounds, matrix.MultiplyPoint3x4(localPoint));
		}

		[Test]
		public void CursorOutsidePlayfieldDoesNotPlaceInEmptySpace()
		{
			AssertMiss(new Ray(new Vector3(0.51f, 1, 0), Vector3.down));
			AssertMiss(new Ray(new Vector3(0, 1, -0.51f), Vector3.down));
		}

		[Test]
		public void ParallelAndBackwardRaysDoNotProducePlacement()
		{
			AssertMiss(new Ray(Vector3.up, Vector3.right));
			AssertMiss(new Ray(Vector3.up, Vector3.up));
		}

		[Test]
		public void ProceduralPreviewDoesNotModifyPrefabOrSceneAndCancelReleasesItsMesh()
		{
			WithPrefab((prefab, playfield) => {
				var filter = prefab.GetComponent<MeshFilter>();
				Assert.That(filter.sharedMesh, Is.Null);
				var roots = SceneManager.GetActiveScene().GetRootGameObjects().Length;
				var dirty = SceneManager.GetActiveScene().isDirty;
				Invoke("Begin", new object[] { new[] { prefab } });
				var parts = (IList)Field("PreviewParts");
				Assert.That(parts.Count, Is.EqualTo(1));
				var mesh = (Mesh)parts[0].GetType().GetField("Mesh").GetValue(parts[0]);
				Assert.That(mesh.vertexCount, Is.GreaterThan(0));
				Assert.That(filter.sharedMesh, Is.Null, "The prefab must not receive a generated preview mesh.");
				Assert.That(SceneManager.GetActiveScene().GetRootGameObjects().Length, Is.EqualTo(roots));
				Assert.That(SceneManager.GetActiveScene().isDirty, Is.EqualTo(dirty));
				var previewScene = (Scene)Field("_previewScene");
				Assert.That(UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(previewScene), Is.True);
				Invoke("Clear");
				Assert.That(mesh == null, Is.True, "Cancel must destroy the owned preview mesh.");
				Assert.That(parts.Count, Is.Zero);
				Assert.That(((Scene)Field("_previewScene")).IsValid(), Is.False);
			});
		}

		[Test]
		public void DropKeepsPrefabMountingPoseAndSupportsUndoAndRedo()
		{
			WithPrefab((prefab, playfield) => {
				Invoke("Begin", new object[] { new[] { prefab } });
				var hitArgs = new object[] { new Ray(playfield.transform.position + Vector3.up, Vector3.down), null, null };
				Assert.That(Invoke("FindSurface", hitArgs), Is.True);
				SetField("_surface", hitArgs[1]);
				var position = playfield.transform.InverseTransformPoint((Vector3)hitArgs[2]) + Vector3.up * prefab.transform.localPosition.y;
				((Vector3[])Field("_positions"))[0] = position;
				Invoke("Place");
				var placed = Selection.activeGameObject;
				Assert.That(placed.name, Is.EqualTo(prefab.name));
				Assert.That(placed.transform.parent, Is.EqualTo(playfield.transform));
				Assert.That(Vector3.Distance(placed.transform.localPosition, position), Is.LessThan(0.000001f));
				Assert.That(Quaternion.Angle(placed.transform.localRotation, prefab.transform.localRotation), Is.LessThan(0.001f));
				Assert.That(placed.transform.localScale, Is.EqualTo(prefab.transform.localScale));
				Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(placed), Is.EqualTo(prefab));
				Undo.FlushUndoRecordObjects();
				Undo.PerformUndo();
				Assert.That(placed == null, Is.True);
				Undo.PerformRedo();
				placed = playfield.transform.GetChild(0).gameObject;
				Assert.That(Vector3.Distance(placed.transform.localPosition, position), Is.LessThan(0.000001f));
				Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(placed), Is.EqualTo(prefab));
				Undo.PerformUndo();
			});
		}

		[Test]
		public void StaticPrefabPreviewUsesStoredMeshWithoutCreatingAnInstance()
		{
			WithPrefab((prefab, playfield) => {
				var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
				Invoke("Begin", new object[] { new[] { prefab } });
				var parts = (IList)Field("PreviewParts");
				Assert.That(parts.Count, Is.EqualTo(1));
				Assert.That(parts[0].GetType().GetField("Mesh").GetValue(parts[0]), Is.SameAs(mesh));
				Assert.That(((Scene)Field("_previewScene")).IsValid(), Is.False);
				Invoke("Clear");
				Assert.That(mesh != null, Is.True, "Shared prefab meshes must survive cancellation.");
			}, false);
		}

		private static void WithPrefab(Action<GameObject, PlayfieldComponent> test, bool procedural = true)
		{
			var selection = Selection.objects;
			var path = $"Assets/__VpePlacementTest_{Guid.NewGuid():N}.prefab";
			GameObject source = null;
			GameObject slab = null;
			try {
				source = procedural ? new GameObject("Placement Test Prefab") : GameObject.CreatePrimitive(PrimitiveType.Cube);
				source.name = "Placement Test Prefab";
				source.SetActive(false);
				if (procedural) { source.AddComponent<InsertComponent>(); }
				source.SetActive(true);
				if (procedural) { source.GetComponent<MeshFilter>().sharedMesh = null; }
				source.GetComponent<MeshRenderer>().sharedMaterials = Array.Empty<Material>();
				source.transform.localPosition = new Vector3(0, -0.0001f, 0);
				source.transform.localRotation = Quaternion.Euler(0, 30, 0);
				source.transform.localScale = Vector3.one * 0.8f;
				var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
				Object.DestroyImmediate(source);
				slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
				slab.transform.position = new Vector3(100, 2, 100);
				slab.transform.rotation = Quaternion.Euler(10, 40, 0);
				slab.transform.localScale = new Vector3(2, 0.02f, 2);
				var playfield = slab.AddComponent<PlayfieldComponent>();
				test(prefab, playfield);
			} finally {
				Invoke("Clear");
				Selection.objects = selection;
				if (source) { Object.DestroyImmediate(source); }
				if (slab) { Object.DestroyImmediate(slab); }
				AssetDatabase.DeleteAsset(path);
			}
		}

		private static object Invoke(string name, object[] args = null) => TryHit.DeclaringType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
		private static object Field(string name) => TryHit.DeclaringType.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
		private static void SetField(string name, object value) => TryHit.DeclaringType.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);

		private static void AssertHit(Ray ray, Matrix4x4 matrix, Bounds bounds, Vector3 expected)
		{
			var args = new object[] { ray, matrix, bounds, null };
			Assert.That(TryHit.Invoke(null, args), Is.True);
			Assert.That(Vector3.Distance((Vector3)args[3], expected), Is.LessThan(0.00001f));
		}

		private static void AssertMiss(Ray ray)
		{
			var args = new object[] { ray, Matrix4x4.identity, new Bounds(Vector3.zero, Vector3.one), null };
			Assert.That(TryHit.Invoke(null, args), Is.False);
		}
	}
}
