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
	public class AssetReferenceLocator : EditorWindow
	{
		[Serializable]
		private class RefResult
		{
			public string assetPath; // Referencer (could be in Assets/ or Packages/org.visualpinball.*)
			public List<string> contexts = new List<string>(); // Optional details (where inside)
		}

		private Object _target;
		private bool _deepInspect = true;
		private bool _includeScenes = true;
		private bool _includePrefabs = true;
		private bool _includeMaterials = true;
		private bool _includeScriptableObjects = true;
		private bool _includePackages = true; // Scan Packages/org.visualpinball.*

		private static readonly string PackagePrefix = "Packages/org.visualpinball.";

		private Vector2 _scroll;
		private List<RefResult> _results = new List<RefResult>();
		private string _status = "";

		// ---------- Compatibility helpers to avoid generic type inference issues ----------
		private static T LoadAtPath<T>(string path) where T : Object
		{
#if UNITY_2019_1_OR_NEWER
			return AssetDatabase.LoadAssetAtPath<T>(path);
#else
        return (T)AssetDatabase.LoadAssetAtPath(path, typeof(T));
#endif
		}

		private static Object LoadAny(string path)
		{
#if UNITY_2019_1_OR_NEWER
			return AssetDatabase.LoadAssetAtPath<Object>(path);
#else
        return AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
#endif
		}

		[MenuItem("Pinball/Tools/Asset Reference Locator", false, 413)]
		public static void Open()
		{
			GetWindow<AssetReferenceLocator>(true, "Asset Reference Locator").Show();
		}

		private void OnGUI()
		{
			GUILayout.Label("Find where an asset is directly referenced (Assets/ + Packages/org.visualpinball.*)",
				EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Select the target asset (e.g., a Texture). Click 'Find References'. The tool lists assets that have a DIRECT reference to it. Enable 'Deep Inspect' to show the exact component/property path.",
				MessageType.Info);

			_target = EditorGUILayout.ObjectField("Target Asset", _target, typeof(Object), false);
			_deepInspect =
				EditorGUILayout.ToggleLeft("Deep Inspect (show component/property where referenced)", _deepInspect);

			EditorGUILayout.BeginHorizontal();
			_includeScenes = EditorGUILayout.ToggleLeft("Scenes", _includeScenes);
			_includePrefabs = EditorGUILayout.ToggleLeft("Prefabs", _includePrefabs);
			_includeMaterials = EditorGUILayout.ToggleLeft("Materials", _includeMaterials);
			_includeScriptableObjects = EditorGUILayout.ToggleLeft("ScriptableObjects", _includeScriptableObjects);
			EditorGUILayout.EndHorizontal();

			_includePackages = EditorGUILayout.ToggleLeft("Include Packages/org.visualpinball.*", _includePackages);

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledScope(_target == null))
			{
				if (GUILayout.Button("Find References", GUILayout.Height(28)))
					FindReferences();
			}

			using (new EditorGUI.DisabledScope(_results.Count == 0))
			{
				if (GUILayout.Button("Export CSV", GUILayout.Height(28)))
					ExportCsv();
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Status:", _status);
			EditorGUILayout.Space();

			EditorGUILayout.LabelField($"Results: {_results.Count}");
			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			foreach (var r in _results)
			{
				EditorGUILayout.BeginVertical("box");
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Asset:", r.assetPath);
				if (GUILayout.Button("Select", GUILayout.Width(80)))
				{
					var obj = LoadAny(r.assetPath);
					Selection.activeObject = obj;
					EditorGUIUtility.PingObject(obj);
				}

				EditorGUILayout.EndHorizontal();

				if (_deepInspect && r.contexts.Count > 0)
				{
					EditorGUILayout.LabelField("Where:");
					foreach (var c in r.contexts.Take(10))
						EditorGUILayout.LabelField("  • " + c);
					if (r.contexts.Count > 10)
						EditorGUILayout.LabelField($"  (+{r.contexts.Count - 10} more)");
				}

				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.EndScrollView();
		}

		private void FindReferences()
		{
			_results.Clear();
			_status = "";

			if (_target == null)
			{
				_status = "Pick a target asset first.";
				Repaint();
				return;
			}

			string targetPath = AssetDatabase.GetAssetPath(_target);
			if (string.IsNullOrEmpty(targetPath))
			{
				_status = "Target does not appear to be a project or package asset.";
				Repaint();
				return;
			}

			try
			{
				// Gather candidates from Assets/ and optionally Packages/org.visualpinball.*
				var allPaths = AssetDatabase.GetAllAssetPaths();
				IEnumerable<string> candidates =
					allPaths.Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase));
				if (_includePackages)
					candidates = candidates.Concat(allPaths.Where(p =>
						p.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase)));

				var candidateArray = candidates.Distinct().ToArray();

				int total = candidateArray.Length;
				int hits = 0;
				for (int i = 0; i < total; i++)
				{
					string path = candidateArray[i];
					if (EditorUtility.DisplayCancelableProgressBar("Scanning Assets + org.visualpinball packages", path,
						    (float)i / total))
						break; // user cancelled

					// Only list DIRECT references (non-recursive dependency lookup)
					string[] deps = AssetDatabase.GetDependencies(path, false); // direct only
					if (deps == null || deps.Length == 0) continue;
					if (!deps.Contains(targetPath)) continue;

					var result = new RefResult { assetPath = path };

					if (_deepInspect)
					{
						// Narrow deep inspection to relevant types to avoid heavy work
						string ext = Path.GetExtension(path).ToLowerInvariant();
						try
						{
							if (_includeMaterials && ext == ".mat")
							{
								var mat = LoadAtPath<Material>(path);
								FindContextsInObject(mat, result);
							}
							else if (_includePrefabs && ext == ".prefab")
							{
								FindContextsInPrefab(path, result);
							}
							else if (_includeScenes && ext == ".unity")
							{
								FindContextsInScene(path, result);
							}
							else if (_includeScriptableObjects)
							{
								FindContextsInGenericAsset(path, result);
							}
						}
						catch (Exception ex)
						{
							// Don't fail the whole scan if one asset fails deep inspect
							result.contexts.Add("[Deep Inspect Error] " + ex.Message);
						}
					}

					_results.Add(result);
					hits++;
				}

				_status = hits > 0
					? $"Found {hits} assets that DIRECTLY reference {Path.GetFileName(targetPath)} (Assets/ + {(_includePackages ? "Packages/org.visualpinball.*" : "no packages")})."
					: "No direct references found.";
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

		private void ExportCsv()
		{
			string save =
				EditorUtility.SaveFilePanel("Export CSV", Application.dataPath, "AssetReferenceResults", "csv");
			if (string.IsNullOrEmpty(save)) return;
			try
			{
				using (var sw = new StreamWriter(save))
				{
					sw.WriteLine("Referencer,Where");
					foreach (var r in _results)
					{
						if (r.contexts.Count == 0)
						{
							sw.WriteLine($"\"{r.assetPath}\",\"\"");
						}
						else
						{
							foreach (var ctx in r.contexts)
								sw.WriteLine($"\"{r.assetPath}\",\"{ctx.Replace("\"", "''")}\"");
						}
					}
				}

				EditorUtility.RevealInFinder(save);
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
		}

		// ---------------- Deep Inspect Helpers ----------------

		private void FindContextsInObject(Object obj, RefResult result)
		{
			if (obj == null) return;
			var so = new SerializedObject(obj);
			FindObjectReferenceProperties(so, _target,
				(propPath) => { result.contexts.Add($"{obj.GetType().Name}.{propPath}"); });
		}

		private void FindContextsInGenericAsset(string path, RefResult result)
		{
			// Try main asset first (type-agnostic)
			var main = LoadAny(path);
			if (main != null)
				FindContextsInObject(main, result);

			// Also scan sub-assets (e.g., Materials inside FBX, nested ScriptableObjects)
			foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
			{
				if (sub == null || ReferenceEquals(sub, main)) continue;
				FindContextsInObject(sub, result);
			}
		}

		private void FindContextsInPrefab(string prefabPath, RefResult result)
		{
			var root = PrefabUtility.LoadPrefabContents(prefabPath);
			try
			{
				foreach (var t in root.GetComponentsInChildren<Transform>(true))
				{
					var go = t.gameObject;
					var comps = go.GetComponents<Component>();
					foreach (var c in comps)
					{
						if (c == null) continue; // missing script
						var so = new SerializedObject(c);
						FindObjectReferenceProperties(so, _target,
							(propPath) =>
							{
								result.contexts.Add($"{GetHierarchyPath(go)} → {c.GetType().Name}.{propPath}");
							});
					}
				}
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		private void FindContextsInScene(string scenePath, RefResult result)
		{
			var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
			try
			{
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
							FindObjectReferenceProperties(so, _target,
								(propPath) =>
								{
									result.contexts.Add(
										$"[Scene {Path.GetFileName(scenePath)}] {GetHierarchyPath(go)} → {c.GetType().Name}.{propPath}");
								});
						}
					}
				}
			}
			finally
			{
				EditorSceneManager.CloseScene(scene, true);
			}
		}

		private static void FindObjectReferenceProperties(SerializedObject so, Object target,
			Action<string> onHit)
		{
			if (so == null) return;
			var it = so.GetIterator();
			bool enterChildren = true;
			while (it.NextVisible(enterChildren))
			{
				enterChildren = false;
				if (it.propertyType == SerializedPropertyType.ObjectReference)
				{
					if (it.objectReferenceValue == target)
					{
						onHit?.Invoke(it.propertyPath);
					}
				}
			}
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
	}
}
