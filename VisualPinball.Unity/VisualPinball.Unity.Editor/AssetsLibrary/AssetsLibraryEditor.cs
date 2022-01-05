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

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;
using System.Linq;
using System;

namespace VisualPinball.Unity.Editor
{
	public class AssetsLibraryEditor : BaseEditorWindow
	{
		private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private LabelsHandler _labelsHandler = new LabelsHandler();

		private List<AssetLibraryContent> _assetLibraries = new List<AssetLibraryContent>();
		private Dictionary<AssetLibraryContent, bool> _assetLibrariesSelection = new Dictionary<AssetLibraryContent, bool>();

		private List<PinballLabel> _filterLabels = new List<PinballLabel>();

		private List<AssetThumbnailElement> _thumbs = new List<AssetThumbnailElement>();
		private AssetsThumbnailView _thumbView = new AssetsThumbnailView(null) { MultiSelection = false };

		private UnityEditor.Editor _previewEditor = null;

		private List<PinballLabel> _assetLabels = new List<PinballLabel>();

		[MenuItem("Visual Pinball/Assets Library", false, 601)]
		public static void ShowWindow()
		{
			GetWindow<AssetsLibraryEditor>().titleContent = new GUIContent("Assets Library");
		}

		public AssetsLibraryEditor() : base()
		{
		}

		public override void OnEnable()
		{
			base.OnEnable();
			CheckSettinbgsAssets();
		}

		private const float DetailsWindowWidth = 400.0f;

		private bool _showDetails = true;
		private bool _showLabels = true;

		private void DetailsGUI()
		{

			if (_thumbView.SelectedAsset == null) {
				DestroyImmediate(_previewEditor);
				_previewEditor = null;
			} else if (_previewEditor == null || _thumbView.SelectedAsset != _previewEditor.target) {
				if (_previewEditor != null) {
					DestroyImmediate(_previewEditor);
				}

				_previewEditor = UnityEditor.Editor.CreateEditor(_thumbView.SelectedAsset);
				_assetLabels.Clear();
				if (_thumbView.SelectedAsset != null) {
					var labels = AssetDatabase.GetLabels(_thumbView.SelectedAsset);
					_assetLabels.AddRange(labels.Select(L => new PinballLabel(L)).ToList());
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
					EditorGUILayout.LabelField($"Name : {_thumbView.SelectedAsset.name}", style);
					var prefabType = PrefabUtility.GetPrefabAssetType(_thumbView.SelectedAsset);
					if (prefabType != PrefabAssetType.NotAPrefab) {
						EditorGUILayout.LabelField($"Type : Prefab ({prefabType})", style);
					} else {
						EditorGUILayout.LabelField($"Type : {_thumbView.SelectedAsset.GetType().Name}", style);
					}
					EditorGUILayout.LabelField($"Path : {AssetDatabase.GetAssetPath(_thumbView.SelectedAsset)}", style);
					EditorGUILayout.Separator();
					if (_thumbView.SelectedAsset is GameObject gameObj) {
						var meshRenderer = gameObj.GetComponentInChildren<MeshRenderer>();
						var meshFilter = gameObj.GetComponentInChildren<MeshFilter>();
						if (meshRenderer && meshFilter) {
							EditorGUILayout.LabelField($"Mesh : {meshRenderer.name}", style);
							EditorGUI.indentLevel++;
							if (meshFilter.sharedMesh != null) {
								EditorGUILayout.LabelField($"{meshFilter.sharedMesh.subMeshCount} submesh{(meshFilter.sharedMesh.subMeshCount > 1 ? "es" : "")}", style);
								EditorGUILayout.LabelField($"{meshFilter.sharedMesh.vertices.Length} vertices", style);
								EditorGUILayout.LabelField($"{meshFilter.sharedMesh.triangles.Length} triangles", style);
							}
							if (meshRenderer.sharedMaterials != null) {
								EditorGUILayout.LabelField($"{meshRenderer.sharedMaterials.Length} material{(meshRenderer.sharedMaterials.Length > 1 ? "s" : "")} : {(meshRenderer.sharedMaterials.Length > 0 ? string.Join(',', meshRenderer.sharedMaterials.Select(M => M ? M.name : "null")) : "")}", style);
							}
							EditorGUI.indentLevel--;
						}
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
				EditorGUILayout.LabelField(new GUIContent("Select an asset"));
			}
		}

		public void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();

			var thumbRect = new Rect(position);
			thumbRect.y += GUI.skin.button.lineHeight;
			thumbRect.width -= DetailsWindowWidth;
			thumbRect.height -= GUI.skin.button.lineHeight;

			EditorGUILayout.BeginVertical(GUILayout.Width(thumbRect.width));
			//Library selector
			if (EditorGUILayout.DropdownButton(new GUIContent("Libraries"), FocusType.Passive, GUILayout.Width(150))) {
				DoLibrariesPopup();
			}

			_thumbView.OnGUI(thumbRect);

			EditorGUILayout.EndVertical();

			EditorGUILayout.BeginVertical();
			DetailsGUI();
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		private void DoLibrariesPopup()
		{
			var inputdata = new GenericPopupList.InputData() {
				m_AllowCustom = false,
				m_CloseOnSelection = false,
				m_EnableAutoCompletion = false,
				m_SortAlphabetically = true,
				m_OnSelectCallback = OnLibrarySelection
			};

			inputdata.m_ListElements.AddRange(_assetLibrariesSelection.Select(L => new PopupListElement(L.Key.Settings.Name, L.Value)));

			var popupList = new GenericPopupList(inputdata);
			Rect r = GUILayoutUtility.GetRect(GUI.skin.button.fixedWidth, GUI.skin.button.fixedWidth, GUI.skin.button.fixedHeight + 5.0f, GUI.skin.button.fixedHeight + 5.0f);
			PopupWindow.Show(r, popupList);
		}

		private void OnLibrarySelection(PopupListElement element)
		{
			element.selected = !element.selected;
			_assetLibrariesSelection[_assetLibrariesSelection.FirstOrDefault(L => L.Key.Settings.Name.Equals(element.m_Content.text, StringComparison.InvariantCultureIgnoreCase)).Key] = element.selected;
			CreateThumbnailElements();
		}

		private void CheckSettinbgsAssets()
		{
			_labelsHandler.Init();

			var guids = AssetDatabase.FindAssets("t:AssetsLibrarySettingsAsset");
			foreach(var guid in guids) {
				var asset = AssetDatabase.LoadAssetAtPath<AssetsLibrarySettingsAsset>(AssetDatabase.GUIDToAssetPath(guid));
				if (!_assetLibraries.Any(L=>L.Settings == asset)) {
					var library = new AssetLibraryContent(asset);
					library.FolderPopulate += OnFolderPopulate;
					library.PopulateAssets();
					_assetLibraries.Add(library);
					_assetLibrariesSelection.Add(library, true);
				}
			}

			CreateThumbnailElements();
		}

		private void OnFolderPopulate(AssetLibraryContent.AssetLibraryFolderContent folder)
		{
			foreach(var asset in folder.Assets) {
				_labelsHandler.AddLabels(asset.Labels);
			}
		}

		private void CreateThumbnailElements()
		{
			_thumbView.SetData(null);
			_thumbs.Clear();
			foreach(var tuple in _assetLibrariesSelection) {
				if (tuple.Value) {
					foreach(var folder in tuple.Key.FolderContents) {
						foreach(var asset in folder.Assets) {
							if (!_thumbs.Any(T=>T.Asset == asset.Asset)) {
								_thumbs.Add(new AssetThumbnailElement(asset.Asset));
							}
						}
					}
				}
			}
			_thumbView.SetData(_thumbs);
			AssetPreview.SetPreviewTextureCacheSize(_thumbs.Count + 1);
			Repaint();
		}

	}
}
