// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertMeshSaveTests
	{
		private const string GeneratedName = "Insert (generated)";
		private const string PlaceholderName = "Insert (placeholder)";

		private string _scenePath;
		private string _prefabPath;
		private Scene _scene;

		[SetUp]
		public void SetUp()
		{
			var id = Guid.NewGuid().ToString("N");
			_scenePath = $"Assets/InsertMeshSaveTest-{id}.unity";
			_prefabPath = $"Assets/InsertMeshSaveTest-{id}.prefab";
			// the test runner restores its own scenes after the run
			_scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
		}

		[TearDown]
		public void TearDown()
		{
			EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			AssetDatabase.DeleteAsset(_scenePath);
			AssetDatabase.DeleteAsset(_prefabPath);
		}

		[Test]
		public void ShouldNotSaveTheGeneratedMeshAndRebuildItAfterReload()
		{
			var enabledInsert = CreateInsert("Enabled Insert");
			var disabledInsert = CreateInsert("Disabled Insert");
			disabledInsert.enabled = false;

			Save();
			Assert.That(File.ReadAllText(_scenePath), Does.Not.Contain(GeneratedName));

			Reload();
			AssertHasMesh("Enabled Insert");
			// a disabled insert's renderer still draws, so it needs its mesh too
			AssertHasMesh("Disabled Insert");
		}

		[Test]
		public void ShouldNotSaveALegacyMeshOfAnInactiveInsert()
		{
			var insert = CreateInsert("Inactive Insert");
			insert.GetComponent<MeshFilter>().sharedMesh = LegacyMesh();
			insert.gameObject.SetActive(false);

			Save();
			Assert.That(File.ReadAllText(_scenePath), Does.Not.Contain(GeneratedName));
		}

		[Test]
		public void ShouldNotSaveALegacyMeshAPrefabOverridePointsAt()
		{
			var template = CreateInsert("Prefab Insert");
			template.GetComponent<MeshFilter>().sharedMesh = null;
			var prefab = PrefabUtility.SaveAsPrefabAsset(template.gameObject, _prefabPath);
			Object.DestroyImmediate(template.gameObject);

			var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
			var filter = instance.GetComponent<MeshFilter>();
			// An older version saved the mesh into the scene as the instance's override.
			// The guard would keep a mesh under the generated name out of this first save,
			// so it is written under another and renamed in the file.
			filter.sharedMesh = LegacyMesh(PlaceholderName);
			PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
			Save();
			File.WriteAllText(_scenePath, File.ReadAllText(_scenePath).Replace(PlaceholderName, GeneratedName));
			AssetDatabase.ImportAsset(_scenePath, ImportAssetOptions.ForceUpdate);

			// On load the insert builds its own mesh, and the override still points at the old one.
			Reload();
			instance = _scene.GetRootGameObjects().Single(root => root.GetComponent<InsertComponent>() != null);
			Assume.That(instance.GetComponent<MeshFilter>().sharedMesh.name, Does.Not.Contain("1234567890"));
			Assume.That(PrefabUtility.GetPropertyModifications(instance)
				.Any(m => m.propertyPath == "m_Mesh" && m.objectReference is Mesh mesh && mesh.name.StartsWith(GeneratedName)));

			Save();
			Assert.That(File.ReadAllText(_scenePath), Does.Not.Contain(GeneratedName));
		}

		private InsertComponent CreateInsert(string name)
		{
			var go = new GameObject(name);
			SceneManager.MoveGameObjectToScene(go, _scene);
			var insert = go.AddComponent<InsertComponent>();
			insert.RebuildMesh();
			Assume.That(go.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
			return insert;
		}

		private static Mesh LegacyMesh(string name = GeneratedName)
		{
			// what an older version left in the scene: a generated mesh under a stale entity id, saveable
			var mesh = new Mesh { name = $"{name} 1234567890" };
			mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
			mesh.triangles = new[] { 0, 1, 2 };
			return mesh;
		}

		private void Save()
		{
			Assert.That(EditorSceneManager.SaveScene(_scene, _scenePath), Is.True);
		}

		private void Reload()
		{
			_scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Single);
		}

		private void AssertHasMesh(string name)
		{
			var go = _scene.GetRootGameObjects().Single(root => root.name == name);
			var mesh = go.GetComponent<MeshFilter>().sharedMesh;
			Assert.That(mesh, Is.Not.Null, name);
			Assert.That(mesh.vertexCount, Is.GreaterThan(0), name);
		}
	}
}
