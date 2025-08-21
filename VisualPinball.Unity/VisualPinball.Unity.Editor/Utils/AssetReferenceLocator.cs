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

		private UnityEngine.Object _target; // Object-based search (takes precedence if both set to avoid ambiguity)
		private string _guidInput = ""; // GUID-based search

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
		private static T LoadAtPath<T>(string path) where T : UnityEngine.Object
		{
#if UNITY_2019_1_OR_NEWER
			return AssetDatabase.LoadAssetAtPath<T>(path);
#else
        return (T)AssetDatabase.LoadAssetAtPath(path, typeof(T));
#endif
		}

		private static UnityEngine.Object LoadAny(string path)
		{
#if UNITY_2019_1_OR_NEWER
			return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
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
				"Search by Asset (object field) OR by GUID. If both are set, the asset search is used. Results list only DIRECT references.",
				MessageType.Info);

			_target = EditorGUILayout.ObjectField("Target Asset (optional)", _target, typeof(UnityEngine.Object),
				false);
			_guidInput = EditorGUILayout.TextField(
				new GUIContent("Target GUID (optional)",
					"32 hex characters; resolves to an asset if present, otherwise falls back to YAML text scan."),
				_guidInput);

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
			using (new EditorGUI.DisabledScope(_target == null && string.IsNullOrWhiteSpace(_guidInput)))
			{
				if (GUILayout.Button("Find References", GUILayout.Height(28)))
					FindReferences();
			}

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

			string guid = SanitizeGuid(_guidInput);
			bool guidMode = !string.IsNullOrEmpty(guid);

			if (_target == null && !guidMode)
			{
				_status = "Pick a target asset or enter a GUID.";
				Repaint();
				return;
			}

			// Try to resolve GUID to an asset path if provided
			string targetPath = null;
			UnityEngine.Object searchTarget = _target;
			if (guidMode)
			{
				targetPath = AssetDatabase.GUIDToAssetPath(guid);
				if (!string.IsNullOrEmpty(targetPath))
				{
					// Populate the object field with the resolved asset
					searchTarget = LoadAny(targetPath);
					_target = searchTarget; // reflect in the UI as requested
				}
			}
			else
			{
				targetPath = AssetDatabase.GetAssetPath(_target);
			}

			var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				// If GUID resolved, include the asset itself in the results first
				if (!string.IsNullOrEmpty(targetPath))
				{
					var rr = new RefResult { assetPath = targetPath };
					rr.contexts.Add("[Target] Resolved from input" + (guidMode ? " GUID" : " asset"));
					_results.Add(rr);
					addedPaths.Add(targetPath);
				}

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

					bool isHit = false;

					if (!string.IsNullOrEmpty(targetPath))
					{
						// Only list DIRECT references (non-recursive dependency lookup)
						string[] deps = AssetDatabase.GetDependencies(path, false); // direct only
						if (deps != null && deps.Length > 0 && deps.Contains(targetPath))
							isHit = true;
					}
					else if (guidMode)
					{
						// Fallback: GUID text search for direct mentions in YAML-like files
						if (IsYamlLike(path))
						{
							if (FileContainsGuid(path, guid))
								isHit = true;
						}
					}

					if (!isHit) continue;
					if (addedPaths.Contains(path)) continue; // avoid duplicates (e.g., target itself)

					var result = new RefResult { assetPath = path };

					if (_deepInspect && searchTarget != null)
					{
						// Narrow deep inspection to relevant types to avoid heavy work
						string ext = Path.GetExtension(path).ToLowerInvariant();
						try
						{
							if (_includeMaterials && ext == ".mat")
							{
								var mat = LoadAtPath<Material>(path);
								FindContextsInObject(mat, searchTarget, result);
							}
							else if (_includePrefabs && ext == ".prefab")
							{
								FindContextsInPrefab(path, searchTarget, result);
							}
							else if (_includeScenes && ext == ".unity")
							{
								FindContextsInScene(path, searchTarget, result);
							}
							else if (_includeScriptableObjects)
							{
								FindContextsInGenericAsset(path, searchTarget, result);
							}
						}
						catch (Exception ex)
						{
							// Don't fail the whole scan if one asset fails deep inspect
							result.contexts.Add("[Deep Inspect Error] " + ex.Message);
						}
					}
					else if (_deepInspect && guidMode && string.IsNullOrEmpty(targetPath))
					{
						// For GUID-only text search, provide line number hints
						var lines = FindGuidLines(path, guid, maxLines: 5);
						foreach (var ln in lines)
							result.contexts.Add("[YAML] " + ln);
					}

					_results.Add(result);
					addedPaths.Add(path);
					hits++;
				}

				if (guidMode && string.IsNullOrEmpty(targetPath) &&
				    EditorSettings.serializationMode != SerializationMode.ForceText)
				{
					_status =
						$"Found {hits} direct references via GUID text search. Note: project serialization is {EditorSettings.serializationMode}; ForceText is recommended for reliable GUID scanning.";
				}
				else
				{
					_status = $"Found {_results.Count} item(s). Direct references listed.";
				}
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

		// ---------------- Deep Inspect Helpers ----------------

		private void FindContextsInObject(UnityEngine.Object obj, UnityEngine.Object searchTarget, RefResult result)
		{
			if (obj == null || searchTarget == null) return;
			var so = new SerializedObject(obj);
			FindObjectReferenceProperties(so, searchTarget,
				(propPath) => { result.contexts.Add($"{obj.GetType().Name}.{propPath}"); });
		}

		private void FindContextsInGenericAsset(string path, UnityEngine.Object searchTarget, RefResult result)
		{
			// Try main asset first (type-agnostic)
			var main = LoadAny(path);
			if (main != null)
				FindContextsInObject(main, searchTarget, result);

			// Also scan sub-assets (e.g., Materials inside FBX, nested ScriptableObjects)
			foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
			{
				if (sub == null || ReferenceEquals(sub, main)) continue;
				FindContextsInObject(sub, searchTarget, result);
			}
		}

		private void FindContextsInPrefab(string prefabPath, UnityEngine.Object searchTarget, RefResult result)
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
						FindObjectReferenceProperties(so, searchTarget,
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

		private void FindContextsInScene(string scenePath, UnityEngine.Object searchTarget, RefResult result)
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
							FindObjectReferenceProperties(so, searchTarget,
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

		private static void FindObjectReferenceProperties(SerializedObject so, UnityEngine.Object target,
			Action<string> onHit)
		{
			if (so == null || target == null) return;
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

		// ---------------- GUID Search Helpers ----------------

		private static string SanitizeGuid(string input)
		{
			if (string.IsNullOrWhiteSpace(input)) return null;
			var hex = new System.Text.StringBuilder(32);
			foreach (char c in input)
			{
				if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
					hex.Append(char.ToLowerInvariant(c));
			}

			if (hex.Length != 32) return null; // Unity GUIDs are 32 hex chars
			return hex.ToString();
		}

		private static bool IsYamlLike(string assetPath)
		{
			string ext = Path.GetExtension(assetPath).ToLowerInvariant();
			return ext == ".prefab" || ext == ".unity" || ext == ".asset" || ext == ".mat" ||
			       ext == ".controller" || ext == ".overridecontroller" || ext == ".anim" ||
			       ext == ".shadergraph" || ext == ".vfx" || ext == ".uss" || ext == ".uxml" ||
			       ext == ".shader";
		}

		private static string ToAbsolutePath(string assetDbPath)
		{
			// assetDbPath is like "Assets/..." or "Packages/..."
			var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
			return Path.GetFullPath(Path.Combine(projectRoot, assetDbPath));
		}

		private static bool FileContainsGuid(string assetDbPath, string guid)
		{
			try
			{
				string p = ToAbsolutePath(assetDbPath);
				if (!File.Exists(p)) return false;
				// Lightweight scan first
				string text = File.ReadAllText(p);
				if (text.IndexOf("guid:" + guid, StringComparison.OrdinalIgnoreCase) >= 0) return true;
				if (text.IndexOf("guid: " + guid, StringComparison.OrdinalIgnoreCase) >= 0) return true;
				return false;
			}
			catch
			{
				return false;
			}
		}

		private static IEnumerable<string> FindGuidLines(string assetDbPath, string guid, int maxLines = 5)
		{
			var list = new List<string>();
			try
			{
				string p = ToAbsolutePath(assetDbPath);
				if (!File.Exists(p)) return list;
				int n = 0;
				int lineNo = 0;
				foreach (var line in File.ReadLines(p))
				{
					lineNo++;
					if (line.IndexOf(guid, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						list.Add($"line {lineNo}: {TrimForContext(line)}");
						n++;
						if (n >= maxLines) break;
					}
				}
			}
			catch
			{
			}

			return list;
		}

		private static string TrimForContext(string s)
		{
			if (string.IsNullOrEmpty(s)) return s;
			s = s.Trim();
			if (s.Length > 120) s = s.Substring(0, 117) + "...";
			return s;
		}
	}
}
