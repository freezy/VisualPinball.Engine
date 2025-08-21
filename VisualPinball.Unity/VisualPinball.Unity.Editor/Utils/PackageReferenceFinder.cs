// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement; // for Scene + GetRootGameObjects
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class PackageReferenceLocator : EditorWindow
	{
		[Serializable]
		private class DepUsage
		{
			public string depPath;                 // Package dependency path (e.g., Packages/org.visualpinball.*)
			public Object depObject;               // Cached object for UI
			public List<Object> referencerObjects = new List<Object>(); // Project assets or scene GameObjects that reference depPath
		}

		// --- Options ---
		[SerializeField] private string _packagePrefix = "Packages/org.visualpinball."; // “indicated package”
		[SerializeField] private bool _includeScenes = true;
		[SerializeField] private bool _includePrefabs = true;
		[SerializeField] private bool _includeMaterials = true;
		[SerializeField] private bool _includeScriptableObjects = true;
		[SerializeField] private bool _includeEverythingElse = true; // FBX, textures, animations, etc.
		[SerializeField] private bool _onlyOpenScenes = false;       // When true, closed scenes are skipped; open scenes yield GameObjects instead of the .unity asset

		private Vector2 _scroll;
		private readonly List<DepUsage> _results = new List<DepUsage>();
		private string _status = "";

		[MenuItem("Pinball/Tools/Package Reference Locator", false, 411)]
		public static void Open()
		{
			GetWindow<PackageReferenceLocator>(true, "Package Reference Locator").Show();
		}

		private void OnGUI()
		{
			GUILayout.Label("Package dependencies used by the Project (grouped by dependency)", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"When 'Only search open scenes' is enabled, scene references are shown as the specific GameObjects in the open scenes that reference the dependency (directly or via assets like Materials).",
				MessageType.Info);

			_packagePrefix = EditorGUILayout.TextField(
				new GUIContent("Package Prefix", "Only dependencies under this path are considered."),
				_packagePrefix);

			_onlyOpenScenes = EditorGUILayout.ToggleLeft("Only search open scenes (skip closed scenes; list GameObjects for open scenes)", _onlyOpenScenes);

			EditorGUILayout.LabelField("Project Asset Type Filters (referencers):", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			_includeScenes = EditorGUILayout.ToggleLeft("Scenes", _includeScenes);
			_includePrefabs = EditorGUILayout.ToggleLeft("Prefabs", _includePrefabs);
			_includeMaterials = EditorGUILayout.ToggleLeft("Materials", _includeMaterials);
			_includeScriptableObjects = EditorGUILayout.ToggleLeft("ScriptableObjects", _includeScriptableObjects);
			_includeEverythingElse = EditorGUILayout.ToggleLeft("Everything else", _includeEverythingElse);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			if (GUILayout.Button("Scan Project → Package Dependencies", GUILayout.Height(28)))
				ScanProjectToPackageDependencies();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Status:", _status);
			EditorGUILayout.Space();

			EditorGUILayout.LabelField($"Dependencies found: {_results.Count}");
			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			foreach (var dep in _results)
			{
				EditorGUILayout.BeginVertical("box");

				// Dependency header row: ObjectField only
				using (new EditorGUI.DisabledScope(true))
					EditorGUILayout.ObjectField("Dependency", dep.depObject, typeof(Object), false);

				// Referencers list (object fields only; no dots, no buttons)
				EditorGUILayout.LabelField($"Referenced by ({dep.referencerObjects.Count}):");
				foreach (var obj in dep.referencerObjects)
				{
					using (new EditorGUI.DisabledScope(true))
						EditorGUILayout.ObjectField(obj, obj != null ? obj.GetType() : typeof(Object), false);
				}

				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.EndScrollView();
		}

		private void ScanProjectToPackageDependencies()
		{
			_results.Clear();
			_status = "";

			if (string.IsNullOrWhiteSpace(_packagePrefix))
			{
				_status = "Please enter a package prefix (e.g., Packages/org.visualpinball.).";
				Repaint();
				return;
			}

			try
			{
				// Build a set of currently open scene paths (for fast membership checks)
				var openScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < EditorSceneManager.sceneCount; i++)
				{
					var scn = EditorSceneManager.GetSceneAt(i);
					if (scn.IsValid() && scn.isLoaded && !string.IsNullOrEmpty(scn.path))
						openScenePaths.Add(scn.path);
				}

				var allPaths = AssetDatabase.GetAllAssetPaths();

				// Project assets that act as referencers (apply type filters; for scenes, skip closed ones if toggle is on)
				var projectAssets = allPaths
					.Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
					.Where(p => PassesTypeFilter(p) && PassesSceneOpenFilter(p, openScenePaths))
					.Distinct()
					.ToArray();

				// Map: dependency (package) path -> set of project referencers (Object)
				var map = new Dictionary<string, HashSet<Object>>(StringComparer.OrdinalIgnoreCase);

				int total = projectAssets.Length;

				// Caches to avoid repeated AssetDatabase work in scene deep scans
				var depObjCache   = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
				var depsSetCache  = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase); // assetPath -> deps(set)

				for (int i = 0; i < total; i++)
				{
					string referencerPath = projectAssets[i];

					if (EditorUtility.DisplayCancelableProgressBar("Scanning project assets (dependencies)",
						    referencerPath, total == 0 ? 0 : (float)i / total))
					{
						_status = "Scan cancelled.";
						break;
					}

					bool isScene = string.Equals(Path.GetExtension(referencerPath), ".unity", StringComparison.OrdinalIgnoreCase);
					bool isOpenScene = isScene && openScenePaths.Contains(referencerPath);

					// For open scenes we want to include *transitive* deps (textures via materials, etc.)
					bool recursive = isScene && _onlyOpenScenes;

					// Get dependencies of this referencer
					string[] deps = AssetDatabase.GetDependencies(referencerPath, recursive);
					if (deps == null || deps.Length == 0) continue;

					foreach (var dep in deps)
					{
						// Only collect dependencies inside the indicated package
						if (!dep.StartsWith(_packagePrefix, StringComparison.OrdinalIgnoreCase)) continue;

						if (!map.TryGetValue(dep, out var set))
						{
							set = new HashSet<Object>();
							map[dep] = set;
						}

						// Open-scene mode: list *GameObjects* that cause/hold the reference
						if (_onlyOpenScenes && isOpenScene)
						{
							// Get/load the dependency object
							if (!depObjCache.TryGetValue(dep, out var depObj) || depObj == null)
							{
								depObj = LoadAny(dep);
								depObjCache[dep] = depObj;
							}

							var scene = EditorSceneManager.GetSceneByPath(referencerPath);
							foreach (var go in FindSceneGameObjectsReferencing(scene, dep, depObj, depsSetCache))
								set.Add(go);
						}
						else
						{
							// Normal case: store the referencer asset object (prefab, material, SO, or closed scene asset)
							var obj = LoadAny(referencerPath);
							if (obj != null) set.Add(obj);
						}
					}
				}

				EditorUtility.ClearProgressBar();

				// Build result list (no limits)
				foreach (var kv in map.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
				{
					var depPath = kv.Key;
					var usage = new DepUsage
					{
						depPath = depPath,
						depObject = LoadAny(depPath),
						referencerObjects = kv.Value
							.OrderBy(o => GetObjectSortKey(o), StringComparer.OrdinalIgnoreCase)
							.ToList()
					};
					_results.Add(usage);
				}

				if (_results.Count == 0)
					_status = "No project assets reference dependencies in the indicated package (with current filters).";
				else
					_status = $"Found {_results.Count} package dependencies referenced by project assets.";
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
				_status = ex.Message;
			}
			finally
			{
				EditorUtility.ClearProgressBar();
				Repaint();
			}
		}

		// ----- Scene deep-scan to find GameObjects that (directly or indirectly) reference a given asset -----

		private static IEnumerable<GameObject> FindSceneGameObjectsReferencing(Scene scene, string targetDepPath, Object targetDepObj,
			Dictionary<string, HashSet<string>> depsSetCache)
		{
			var results = new HashSet<GameObject>();
			if (!scene.IsValid() || !scene.isLoaded) return results;

			foreach (var root in scene.GetRootGameObjects())
			{
				foreach (var t in root.GetComponentsInChildren<Transform>(true))
				{
					var go = t.gameObject;
					var comps = go.GetComponents<Component>();
					foreach (var c in comps)
					{
						if (c == null) continue; // missing script
						var so = new SerializedObject(c);
						var it = so.GetIterator();
						bool enterChildren = true;
						while (it.NextVisible(enterChildren))
						{
							enterChildren = false;
							if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

							var refObj = it.objectReferenceValue;
							if (refObj == null) continue;

							// 1) Direct reference to the dependency object?
							if (targetDepObj != null && refObj == targetDepObj)
							{
								results.Add(go);
								break;
							}

							// 2) Indirect: does this referenced *asset* depend on the dependency path?
							var refPath = AssetDatabase.GetAssetPath(refObj);
							if (string.IsNullOrEmpty(refPath)) continue; // scene object or non-asset
							if (AssetDependsOn(refPath, targetDepPath, depsSetCache))
							{
								results.Add(go);
								break;
							}
						}
					}
				}
			}

			return results;
		}

		private static bool AssetDependsOn(string assetPath, string targetDepPath,
			Dictionary<string, HashSet<string>> depsSetCache)
		{
			if (!depsSetCache.TryGetValue(assetPath, out var set))
			{
				// Recursive to include textures/shaders/etc. pulled by materials/controllers/etc.
				var deps = AssetDatabase.GetDependencies(assetPath, true) ?? Array.Empty<string>();
				set = new HashSet<string>(deps, StringComparer.OrdinalIgnoreCase);
				depsSetCache[assetPath] = set;
			}
			return set.Contains(targetDepPath);
		}

		// ---------------- Helpers ----------------

		private static string GetObjectSortKey(Object obj)
		{
			if (obj == null) return "~";
			// For scene objects, include scene and hierarchy path; for assets, use asset path
			if (obj is GameObject go)
			{
				var sceneName = go.scene.IsValid() ? go.scene.path : "";
				return sceneName + "|" + GetHierarchyPath(go);
			}
			return AssetDatabase.GetAssetPath(obj) ?? obj.name;
		}

		private static string GetHierarchyPath(GameObject go)
		{
			if (go == null) return "<null>";
			var stack = new List<string>();
			var t = go.transform;
			while (t != null)
			{
				stack.Add(t.name);
				t = t.parent;
			}
			stack.Reverse();
			return string.Join("/", stack);
		}

		private static Object LoadAny(string path)
		{
#if UNITY_2019_1_OR_NEWER
			return AssetDatabase.LoadAssetAtPath<Object>(path);
#else
			return AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
#endif
		}

		private bool PassesTypeFilter(string assetPath)
		{
			string ext = Path.GetExtension(assetPath).ToLowerInvariant();

			// Scenes
			if (ext == ".unity") return _includeScenes;

			// Prefabs
			if (ext == ".prefab") return _includePrefabs;

			// Materials
			if (ext == ".mat") return _includeMaterials;

			// ScriptableObjects (most are .asset)
			if (ext == ".asset") return _includeScriptableObjects;

			// Everything else (textures, meshes, FBX, audio, anims, shaders, etc.)
			return _includeEverythingElse;
		}

		private bool PassesSceneOpenFilter(string assetPath, HashSet<string> openScenePaths)
		{
			if (!_onlyOpenScenes) return true; // not filtering
			if (Path.GetExtension(assetPath).ToLowerInvariant() != ".unity") return true; // only affects scenes
			// when filtering, allow only open scenes
			return openScenePaths.Contains(assetPath);
		}
	}
}
