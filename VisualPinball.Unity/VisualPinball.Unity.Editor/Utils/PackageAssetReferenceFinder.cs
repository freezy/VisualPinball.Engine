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
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class PackageAssetReferenceFinder : EditorWindow
	{
		[Serializable]
		private class Result
		{
			public string assetPath; // The project asset that references the package
			public string[] packageDeps; // The specific package assets it depends on
		}

		private string _packageInput = ""; // e.g., com.company.package or Packages/com.company.package
		private Vector2 _scroll;
		private List<Result> _results = new List<Result>();
		private string _status = "";

		[MenuItem("Pinball/Tools/Package Asset Reference Finder", false, 411)]
		public static void Open()
		{
			GetWindow<PackageAssetReferenceFinder>(true, "Package Asset Reference Finder").Show();
		}

		private void OnGUI()
		{
			GUILayout.Label("Find project assets that reference a Unity package", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"This scans all assets under your project's Assets/ folder and inspects their dependencies. If any dependency path is inside the target package, the asset is reported.",
				MessageType.Info);

			EditorGUILayout.Space();
			_packageInput = EditorGUILayout.TextField("Package name or path", _packageInput);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Scan Project", GUILayout.Height(28)))
			{
				Scan();
			}

			using (new EditorGUI.DisabledScope(_results.Count == 0))
			{
				if (GUILayout.Button("Export CSV", GUILayout.Height(28)))
				{
					ExportCsv();
				}
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
				EditorGUILayout.LabelField("Asset:", r.assetPath);
				if (GUILayout.Button("Select", GUILayout.Width(80)))
				{
					var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(r.assetPath);
					Selection.activeObject = obj;
					EditorGUIUtility.PingObject(obj);
				}

				EditorGUILayout.LabelField("Package dependencies (first 5):");
				foreach (var dep in r.packageDeps.Take(5))
					EditorGUILayout.LabelField("  • " + dep);
				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.EndScrollView();
		}

		private void Scan()
		{
			_results.Clear();
			_status = "";

			string packagePath = NormalizePackagePath(_packageInput);
			if (string.IsNullOrEmpty(packagePath))
			{
				_status =
					"Please enter a package name (e.g. com.unity.postprocessing) or path (Packages/com.unity.postprocessing).";
				Repaint();
				return;
			}

			if (!Directory.Exists(packagePath))
			{
				_status = $"Package path not found: {packagePath}";
				Repaint();
				return;
			}

			try
			{
				string[] projectAssets = AssetDatabase.GetAllAssetPaths()
					.Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
					.ToArray();

				int count = projectAssets.Length;
				int hits = 0;

				for (int i = 0; i < count; i++)
				{
					string path = projectAssets[i];
					if (i % 50 == 0)
						EditorUtility.DisplayProgressBar("Scanning Assets", path, (float)i / count);

					// Ask Unity for full dependency closure, including Packages/*
					string[] deps = AssetDatabase.GetDependencies(path, true);
					if (deps == null || deps.Length == 0)
						continue;

					// Filter to only dependencies inside the package
					var pkgDeps = deps.Where(d => d.StartsWith(packagePath + "/", StringComparison.OrdinalIgnoreCase))
						.Distinct()
						.ToArray();
					if (pkgDeps.Length > 0)
					{
						_results.Add(new Result { assetPath = path, packageDeps = pkgDeps });
						hits++;
					}
				}

				_status = hits > 0
					? $"Found {hits} referencing assets."
					: "No references from project assets to the package were found.";
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
			string save = EditorUtility.SaveFilePanel("Export CSV", Application.dataPath, "PackageReferences", "csv");
			if (string.IsNullOrEmpty(save)) return;
			try
			{
				using (var sw = new StreamWriter(save))
				{
					sw.WriteLine("Asset,PackageDependency");
					foreach (var r in _results)
					{
						foreach (var dep in r.packageDeps)
							sw.WriteLine($"\"{r.assetPath}\",\"{dep}\"");
					}
				}

				EditorUtility.RevealInFinder(save);
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
		}

		private static string NormalizePackagePath(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				return null;

			input = input.Trim();
			if (input.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
				return input.Replace("\\", "/").TrimEnd('/');

			// Assume it's a package name like com.unity.postprocessing
			return "Packages/" + input;
		}
	}

// ------------------ OPTIONAL: GUID text search (rare edge cases) ------------------
// If you prefer a brute-force GUID scan (e.g., for text-based YAML references),
// run the method below via a temporary menu item. It builds the set of GUIDs used by the package
// (from its .meta files) and grep-searches them inside your Assets/ files.

	public static class PackageGuidSearcher
	{
		[MenuItem("Pinball/Tools/Run GUID Text Scan", false, 412)]
		public static void RunGuidTextScan()
		{
			string packagePath = EditorUtility.OpenFolderPanel("Select package folder (inside Packages/)",
				Application.dataPath + "/../Packages", "");
			if (string.IsNullOrEmpty(packagePath)) return;

			var guidSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var meta in Directory.EnumerateFiles(packagePath, "*.meta", SearchOption.AllDirectories))
			{
				foreach (var line in File.ReadLines(meta))
				{
					if (line.StartsWith("guid:") || line.StartsWith("  guid:"))
					{
						var guid = line.Split(':').Last().Trim();
						if (!string.IsNullOrEmpty(guid)) guidSet.Add(guid);
					}
				}
			}

			var yamlLike = Directory.EnumerateFiles(Application.dataPath, "*.*", SearchOption.AllDirectories)
				.Where(p => p.EndsWith(".prefab") || p.EndsWith(".unity") || p.EndsWith(".asset") ||
				            p.EndsWith(".mat") || p.EndsWith(".controller") || p.EndsWith(".overrideController") ||
				            p.EndsWith(".anim") || p.EndsWith(".shadergraph") || p.EndsWith(".vfx") ||
				            p.EndsWith(".uss") || p.EndsWith(".uxml") || p.EndsWith(".shader"));

			int inspected = 0, matches = 0;
			try
			{
				foreach (var file in yamlLike)
				{
					inspected++;
					if (inspected % 50 == 0)
						EditorUtility.DisplayProgressBar("GUID Text Scan", file, 0);

					string text = File.ReadAllText(file);
					foreach (var g in guidSet)
					{
						if (text.IndexOf(g, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							Debug.Log($"GUID match → {file} references a GUID from {packagePath}");
							matches++;
							break; // log once per file
						}
					}
				}

				EditorUtility.DisplayDialog("GUID Text Scan", $"Inspected {inspected} files. Matches: {matches}.",
					"OK");
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}
	}
}
