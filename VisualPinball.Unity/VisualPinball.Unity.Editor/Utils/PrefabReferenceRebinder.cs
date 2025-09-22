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

// Assets/Editor/GuidPrefabReferenceReplacer.cs
// Two-field YAML replacer for prefab references.
// Replaces occurrences of "guid: <brokenGuid>" with the new prefab's GUID and normalizes fileID/type
// so references point to the prefab root (fileID: 100100000, type: 3).
// Unity 2020+
//
// SAFETY: Use version control. Test with Dry Run first.
//
// Namespace per your request.

// Assets/Editor/MissingPrefabInstanceReplacer.cs
// Rebinds scene Prefab instances with MissingAsset status to a new prefab asset.
// Unity 2022.3+/Unity 6+
//
// Uses PrefabUtility.ReplacePrefabAssetOfPrefabInstance / ReplacePrefabAssetOfPrefabInstances
// to rebase instances, preserving overrides by matching hierarchy paths.
//
// NOTE: Works on *instances* (the red, missing ones). It does not edit raw YAML.
//
// Namespace: VisualPinball.Unity.Editor

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity.Editor
{
    public class MissingPrefabInstanceReplacer : EditorWindow
    {
        [MenuItem("Tools/Missing Prefab Instance Replacer")]
        public static void Open() => GetWindow<MissingPrefabInstanceReplacer>("Missing Prefab Replacer");

        [SerializeField] private GameObject newPrefabAsset;   // target prefab asset (root)

        private List<GameObject> _missingRoots = new List<GameObject>();
        private Vector2 _scroll;

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Finds Prefab instances in open scenes whose source asset is missing, "
              + "then replaces their Prefab Asset with the new Prefab (preserving overrides).",
                MessageType.Info);

            newPrefabAsset = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("New Prefab (asset)"),
                newPrefabAsset, typeof(GameObject), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Open Scenes")) ScanOpenScenes();
                using (new EditorGUI.DisabledScope(_missingRoots.Count == 0))
                {
                    if (GUILayout.Button($"Replace ALL ({_missingRoots.Count})"))
                        ReplaceMany(_missingRoots);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Missing instances found: {_missingRoots.Count}", EditorStyles.boldLabel);

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.MinHeight(250)))
            {
                _scroll = sv.scrollPosition;
                for (int i = 0; i < _missingRoots.Count; i++)
                {
                    var go = _missingRoots[i];
                    if (!go) continue;

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField(go.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(ScenePath(go), EditorStyles.miniLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField("Instance Root", go, typeof(GameObject), true);
                        using (new EditorGUI.DisabledScope(!newPrefabAsset))
                        {
                            if (GUILayout.Button("Replace This"))
                                ReplaceMany(new List<GameObject> { go });
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
            }
        }

        private void ScanOpenScenes()
        {
            _missingRoots.Clear();

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    // include inactive, nested
                    var all = root.GetComponentsInChildren<Transform>(true)
                                  .Select(t => t.gameObject);

                    foreach (var go in all)
                    {
                        // Only consider *outermost* prefab instance roots
                        if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;

                        // Is this instance missing its source asset?
                        // (Either API works; both included for robustness across versions)
                        var status = PrefabUtility.GetPrefabInstanceStatus(go);
                        bool missing = PrefabUtility.IsPrefabAssetMissing(go) ||
                                       status == PrefabInstanceStatus.MissingAsset;

                        if (missing) _missingRoots.Add(go);
                    }
                }
            }

            // De-dup and keep stable order
            _missingRoots = _missingRoots.Distinct().ToList();
            Repaint();
            EditorUtility.DisplayDialog("Scan complete",
                $"Found {_missingRoots.Count} missing prefab instance(s) in open scenes.", "OK");
        }

        private void ReplaceMany(List<GameObject> instanceRoots)
        {
            if (!newPrefabAsset)
            {
                EditorUtility.DisplayDialog("Assign Prefab",
                    "Please assign the 'New Prefab (asset)' first.", "OK");
                return;
            }

            // Prefer hierarchy-based matching to preserve overrides across names/paths
            var settings = new PrefabReplacingSettings
            {
                logInfo = true,
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                // Keep overrides unless you want to clear some types explicitly:
                // prefabOverridesOptions = PrefabOverridesOptions.ClearAllNonDefaultOverrides
            };

            // Batch replacement API (Unity 6+). If unavailable, fall back to per-item.
            try
            {
                PrefabUtility.ReplacePrefabAssetOfPrefabInstances(
                    instanceRoots.Where(x => x).ToArray(),
                    newPrefabAsset,
                    settings,
                    InteractionMode.AutomatedAction
                );
            }
            catch
            {
                // Fallback: do one by one
                foreach (var go in instanceRoots.Where(x => x))
                {
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(
                        go, newPrefabAsset, settings, InteractionMode.AutomatedAction);
                }
            }

            // Save modified scenes
            var touched = new HashSet<Scene>();
            foreach (var go in instanceRoots.Where(x => x))
                touched.Add(go.scene);

            foreach (var sc in touched)
            {
                if (sc.IsValid() && sc.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(sc);
                    EditorSceneManager.SaveScene(sc);
                }
            }

            // Remove the ones we just fixed from the list and refresh UI
            _missingRoots.RemoveAll(x => x == null || !PrefabUtility.IsPrefabAssetMissing(x));
            Repaint();

            EditorUtility.DisplayDialog("Done",
                "Replacement complete. The red 'Missing Prefab' instances should now be normal prefab instances linked to the new asset.",
                "OK");
        }

        private static string ScenePath(GameObject go)
        {
            // Pretty transform path for display
            var stack = new System.Collections.Generic.Stack<string>();
            var t = go.transform;
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }
    }
}
#endif
