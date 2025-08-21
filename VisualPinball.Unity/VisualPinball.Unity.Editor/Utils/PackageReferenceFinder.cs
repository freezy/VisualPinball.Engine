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
			public List<string> referencerPaths = new List<string>(); // Assets/ paths that directly reference depPath
		}

		// --- Options ---
		[SerializeField] private string _packagePrefix = "Packages/org.visualpinball."; // “indicated package”
		[SerializeField] private bool _includeScenes = true;
		[SerializeField] private bool _includePrefabs = true;
		[SerializeField] private bool _includeMaterials = true;
		[SerializeField] private bool _includeScriptableObjects = true;
		[SerializeField] private bool _includeEverythingElse = true; // FBX, textures, animations, etc.
		[SerializeField] private bool _onlyOpenScenes = false;       // When true, closed scenes are skipped

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
				"This lists every asset in the indicated package that is DIRECTLY referenced by your project. " +
				"Under each dependency you get the full list of project assets that reference it.",
				MessageType.Info);

			_packagePrefix = EditorGUILayout.TextField(
				new GUIContent("Package Prefix", "Only dependencies under this path are considered."),
				_packagePrefix);

			_onlyOpenScenes = EditorGUILayout.ToggleLeft("Only search open scenes (skip closed scenes)", _onlyOpenScenes);

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

				// Dependency header row: ObjectField only (click the target icon to reveal)
				using (new EditorGUI.DisabledScope(true))
					EditorGUILayout.ObjectField("Dependency", dep.depObject, typeof(Object), false);

				// Referencers list (no dots, no buttons)
				EditorGUILayout.LabelField($"Referenced by ({dep.referencerPaths.Count}):");
				foreach (var path in dep.referencerPaths)
				{
					var obj = LoadAny(path);
					using (new EditorGUI.DisabledScope(true))
						EditorGUILayout.ObjectField(obj, typeof(Object), false);
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

				// Map: dependency (package) path -> set of project assets that reference it
				var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

				int total = projectAssets.Length;
				for (int i = 0; i < total; i++)
				{
					string referencer = projectAssets[i];

					if (EditorUtility.DisplayCancelableProgressBar("Scanning project assets (direct dependencies)",
						    referencer, total == 0 ? 0 : (float)i / total))
					{
						_status = "Scan cancelled.";
						break;
					}

					// Direct dependencies only
					string[] deps = AssetDatabase.GetDependencies(referencer, false);
					if (deps == null || deps.Length == 0) continue;

					foreach (var dep in deps)
					{
						// Only collect dependencies inside the indicated package
						if (!dep.StartsWith(_packagePrefix, StringComparison.OrdinalIgnoreCase)) continue;

						if (!map.TryGetValue(dep, out var set))
						{
							set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
							map[dep] = set;
						}
						set.Add(referencer);
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
						referencerPaths = kv.Value.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
					};
					_results.Add(usage);
				}

				if (_results.Count == 0)
					_status = "No project assets directly reference dependencies in the indicated package.";
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

		// ---------------- Helpers ----------------

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
