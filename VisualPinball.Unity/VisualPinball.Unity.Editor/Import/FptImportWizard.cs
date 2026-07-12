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
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public sealed class FptImportWizard : EditorWindow
	{
		private string _path;
		private string _tableName;
		private string _libraryRoots = string.Empty;
		private Vector2 _scroll;
		private readonly FptImportOptions _options = new FptImportOptions();

		public static void Open(string path = null)
		{
			var window = GetWindow<FptImportWizard>(true, "Future Pinball Import", true);
			window.minSize = new Vector2(520f, 520f);
			if (!string.IsNullOrWhiteSpace(path)) {
				window._path = path;
				window._tableName = Path.GetFileNameWithoutExtension(path);
			}
			window.Show();
		}

		private void OnGUI()
		{
			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			EditorGUILayout.LabelField("Lossless Future Pinball Import", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("The original FPT and every media/model/script payload are preserved. Scene conversion currently creates static meshes, materials, colliders, and placeholders; it does not execute Future Pinball scripts.", MessageType.Info);

			EditorGUILayout.BeginHorizontal();
			_path = EditorGUILayout.TextField("FPT File", _path);
			if (GUILayout.Button("Browse", GUILayout.Width(80f))) {
				var selected = EditorUtility.OpenFilePanel("Future Pinball Table", Path.GetDirectoryName(_path), "fpt");
				if (!string.IsNullOrEmpty(selected)) {
					_path = selected;
					if (string.IsNullOrWhiteSpace(_tableName)) _tableName = Path.GetFileNameWithoutExtension(selected);
				}
			}
			EditorGUILayout.EndHorizontal();
			_tableName = EditorGUILayout.TextField("Table Name", _tableName);
			_options.AssetRoot = EditorGUILayout.TextField("Asset Root", _options.AssetRoot);
			_libraryRoots = EditorGUILayout.TextField("FPL Search Roots", _libraryRoots);
			EditorGUILayout.HelpBox("Separate multiple library roots with semicolons. Adjacent FPL files are searched first.", MessageType.None);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Source bundle", EditorStyles.boldLabel);
			_options.CopyOriginalTable = EditorGUILayout.Toggle("Copy Original FPT", _options.CopyOriginalTable);
			_options.OverwriteChangedSourceFiles = EditorGUILayout.Toggle("Overwrite Changed Files", _options.OverwriteChangedSourceFiles);
			_options.ReuseGeneratedAssets = EditorGUILayout.Toggle("Reuse Generated Assets", _options.ReuseGeneratedAssets);
			_options.ReplaceExistingSceneRoot = EditorGUILayout.Toggle("Replace Existing Root", _options.ReplaceExistingSceneRoot);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Recreation", EditorStyles.boldLabel);
			_options.ImportPrimaryModels = EditorGUILayout.Toggle("Import Primary Models", _options.ImportPrimaryModels);
			_options.GenerateColliders = EditorGUILayout.Toggle("Generate Colliders", _options.GenerateColliders);
			using (new EditorGUI.DisabledScope(!_options.GenerateColliders)) {
				_options.EnablePerPolygonCollision = EditorGUILayout.Toggle("Per-Polygon Collision", _options.EnablePerPolygonCollision);
				_options.GenerateRenderMeshFallbackColliders = EditorGUILayout.Toggle("Render-Mesh Fallback", _options.GenerateRenderMeshFallbackColliders);
			}

			GUILayout.FlexibleSpace();
			using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))) {
				if (GUILayout.Button("Import", GUILayout.Height(34f))) Import();
			}
			EditorGUILayout.EndScrollView();
		}

		private void Import()
		{
			_options.LibrarySearchRoots = (_libraryRoots ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(root => root.Trim()).Where(root => root.Length > 0).ToArray();
			try {
				var result = FptImportEngine.ImportIntoScene(_path, _tableName, options: _options);
				EditorUtility.DisplayDialog("Future Pinball Import",
					$"Imported {result.Report.Elements} elements.\nMeshes: {result.Report.MeshAssets}\nColliders: {result.Report.Colliders}\nPlaceholders: {result.Report.Placeholders}\n\nSource bundle and reports:\n{result.BundleAssetPath}", "OK");
				Close();
			} catch (Exception exception) {
				UnityEngine.Debug.LogException(exception);
				EditorUtility.DisplayDialog("Future Pinball Import Failed", exception.Message, "OK");
			}
		}
	}
}
