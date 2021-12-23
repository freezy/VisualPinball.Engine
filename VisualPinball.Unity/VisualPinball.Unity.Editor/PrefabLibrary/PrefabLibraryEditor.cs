// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using UnityEngine;
using UnityEditor;
using Logger = NLog.Logger;
using System.Linq;
using System.IO;

namespace VisualPinball.Unity.Editor
{
	internal class PrefabLibraryFolderContent
	{
		private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		public class PrefabData
		{
			public GameObject Prefab;
			public string[] Labels;
		}

		public PrefabLibrarySettingsAsset.FolderSettings FolderSetting;
		public List<PrefabData> Prefabs = new List<PrefabData>();
		private FileSystemWatcher FolderWatcher = new FileSystemWatcher();

		public Action<PrefabLibraryFolderContent> Populate;

		public PrefabLibraryFolderContent(PrefabLibrarySettingsAsset.FolderSettings folderSetting)
		{
			FolderSetting = folderSetting;
			var folderPath = AssetDatabase.GUIDToAssetPath(FolderSetting.FolderReference.Guid);
			if (folderPath != string.Empty) {
				FolderWatcher.Path = folderPath;
				FolderWatcher.NotifyFilter = NotifyFilters.Attributes
									 | NotifyFilters.CreationTime
									 | NotifyFilters.DirectoryName
									 | NotifyFilters.FileName
									 | NotifyFilters.LastAccess
									 | NotifyFilters.LastWrite
									 | NotifyFilters.Security
									 | NotifyFilters.Size;
				FolderWatcher.Created += OnChanged;
				FolderWatcher.Deleted += OnChanged;
				FolderWatcher.Renamed += OnRenamed;
				FolderWatcher.Changed += OnChanged;
				FolderWatcher.EnableRaisingEvents = true;
			}
		}

		private void OnChanged(object sender, FileSystemEventArgs e)
		{

		}

		private void OnRenamed(object sender, RenamedEventArgs e)
		{
			
		}

		public void PopulatePrefabs()
		{
			Prefabs.Clear();
			var folderpath = AssetDatabase.GUIDToAssetPath(FolderSetting.FolderReference.Guid);
			EditorUtility.DisplayProgressBar("Parsing Prefabs", folderpath, 0.0f);
			var guids = AssetDatabase.FindAssets("t: Prefab", new[] { folderpath });
			int count = 0;

			foreach (var guid in guids) {
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (!FolderSetting.Recursive && !Path.GetDirectoryName(path).Equals(folderpath, StringComparison.InvariantCultureIgnoreCase)) {
					continue;
				}
				var asset = AssetDatabase.LoadMainAssetAtPath(path.Replace("\\", "/")) as GameObject;
				if (asset != null) {
					var cachedMetadata = PinballMetadataCache.LoadMetadata(guid);
					var prefabData = new PrefabData() { Prefab = asset, Labels = AssetDatabase.GetLabels(new GUID(guid)) };
					Prefabs.Add(prefabData);
				}
				EditorUtility.DisplayProgressBar($"Parsing Prefabs {count+1}/{guids.Length}", path, (float)count++/guids.Length);
			}

			EditorUtility.ClearProgressBar();

			Populate?.Invoke(this);
		}
	}

	public class PrefabLibraryEditor : BaseEditorWindow
	{
		private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private LabelsHandler _labelsHandler = new LabelsHandler();

		private List<PrefabLibraryFolderContent> _folderContents = new List<PrefabLibraryFolderContent>();

		private List<PinballLabel> _filterLabels = new List<PinballLabel>();

		private List<PrefabThumbnailElement> _thumbs = new List<PrefabThumbnailElement>();
		private PrefabThumbnailView _thumbView = new PrefabThumbnailView(null) { MultiSelection = false };

		private UnityEditor.Editor _previewEditor = null;

		private List<PinballLabel> _prefabLabels = new List<PinballLabel>();

		[MenuItem("Visual Pinball/Prefab Library", false, 601)]
		public static void ShowWindow()
		{
			GetWindow<PrefabLibraryEditor>().titleContent = new GUIContent("Prefabs Library");
		}

		public PrefabLibraryEditor() : base()
		{
		}

		public override void OnEnable()
		{
			base.OnEnable();
			CheckPrefabSettinbgsAssets();
		}

		private const float DetailsWindowWidth = 400.0f;

		private bool _showDetails = true;
		private bool _showLabels = true;

		private void DetailsGUI()
		{

			if (_thumbView.SelectedPrefab == null) {
				DestroyImmediate(_previewEditor);
				_previewEditor = null;
			} else if (_previewEditor == null || _thumbView.SelectedPrefab != _previewEditor.target) {
				if (_previewEditor != null) {
					DestroyImmediate(_previewEditor);
				}

				_previewEditor = UnityEditor.Editor.CreateEditor(_thumbView.SelectedPrefab);
				_prefabLabels.Clear();
				if (_thumbView.SelectedPrefab != null) {
					var labels = AssetDatabase.GetLabels(_thumbView.SelectedPrefab);
					_prefabLabels.AddRange(labels.Select(L => new PinballLabel(L)).ToList());
				}
			}

			if (_previewEditor) {
				var previewSize = DetailsWindowWidth - GUI.skin.box.lineHeight - GUI.skin.label.padding.horizontal;
				var rect = EditorGUILayout.GetControlRect(false, previewSize, GUILayout.Width(previewSize));
				EditorGUI.PrefixLabel(rect, new GUIContent("Preview"));
				rect.yMin += GUI.skin.label.lineHeight + GUI.skin.label.padding.vertical;
				_previewEditor.OnInteractivePreviewGUI(rect, GUI.skin.box);

				if (_showDetails = EditorGUILayout.BeginFoldoutHeaderGroup(_showDetails, new GUIContent("Details"))) {
					EditorGUI.indentLevel++;
					var style = EditorStyles.label;
					style.wordWrap = true;
					EditorGUILayout.LabelField($"Name : {_thumbView.SelectedPrefab.name}", style);
					EditorGUILayout.LabelField($"Path : {AssetDatabase.GetAssetPath(_thumbView.SelectedPrefab)}", style);
					EditorGUILayout.Separator();
					var meshRenderer = _thumbView.SelectedPrefab.GetComponentInChildren<MeshRenderer>();
					var meshFilter = _thumbView.SelectedPrefab.GetComponentInChildren<MeshFilter>();
					if (meshRenderer && meshFilter){
						EditorGUILayout.LabelField($"Mesh : {meshRenderer.name}", style);
						EditorGUI.indentLevel++;
						if (meshFilter.sharedMesh != null) {
							EditorGUILayout.LabelField($"{meshFilter.sharedMesh.subMeshCount} submesh{(meshFilter.sharedMesh.subMeshCount>1?"es":"")}", style);
							EditorGUILayout.LabelField($"{meshFilter.sharedMesh.vertices.Length} vertices", style);
							EditorGUILayout.LabelField($"{meshFilter.sharedMesh.triangles.Length} triangles", style);
						}
						if (meshRenderer.sharedMaterials != null) {
							EditorGUILayout.LabelField($"{meshRenderer.sharedMaterials.Length} material{(meshRenderer.sharedMaterials.Length>1?"s":"")} : {(meshRenderer.sharedMaterials.Length > 0 ? string.Join(',', meshRenderer.sharedMaterials.Select(M => M ? M.name : "null")) : "")}", style);
						}
						EditorGUI.indentLevel--;
					}
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();

				if (_showLabels = EditorGUILayout.BeginFoldoutHeaderGroup(_showLabels, new GUIContent("Labels"))) {
					EditorGUI.indentLevel++;
					EditorGUILayout.LabelField(new GUIContent("Labels"));
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();

			} else {
				EditorGUILayout.LabelField(new GUIContent("Select a prefab"));
			}
		}

		public void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();

			var thumbRect = new Rect(position);
			thumbRect.width -= DetailsWindowWidth;
			_thumbView.OnGUI(thumbRect);

			EditorGUILayout.BeginVertical();
			DetailsGUI();
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		private void CheckPrefabSettinbgsAssets()
		{
			_labelsHandler.Init();

			var guids = AssetDatabase.FindAssets("t: PrefabLibrarySettingsAsset");
			foreach(var guid in guids) {
				var asset = AssetDatabase.LoadAssetAtPath<PrefabLibrarySettingsAsset>(AssetDatabase.GUIDToAssetPath(guid));
				foreach (var folder in asset.Folders) {
					if (!_folderContents.Any(FC=>FC.FolderSetting == folder)) {
						var newFolder = new PrefabLibraryFolderContent(folder);
						newFolder.Populate += OnFolderPopulate;
						newFolder.PopulatePrefabs();
						_folderContents.Add(newFolder);
					}
				}
			}

			_thumbView.SetData(_thumbs);
			AssetPreview.SetPreviewTextureCacheSize(_thumbs.Count + 10);
		}

		private void OnFolderPopulate(PrefabLibraryFolderContent folder)
		{
			foreach (var prefab in folder.Prefabs) {
				_thumbs.Add(new PrefabThumbnailElement(prefab.Prefab));
				_labelsHandler.AddLabels(prefab.Labels);
			}
		}
	}
}
