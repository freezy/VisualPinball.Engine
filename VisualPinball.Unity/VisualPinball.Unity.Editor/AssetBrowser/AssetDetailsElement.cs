// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

// ReSharper disable PossibleUnintendedReferenceComparison

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// What's rendered on the right panel.
	/// </summary>
	public class AssetDetailsElement : VisualElement
	{
		private readonly Label _noSelectionElement;
		private readonly VisualElement _detailsElement;
		private readonly Label _titleElement;
		private readonly VisualElement _attributesElement;

		private List<Preset> _thumbCameraPresets;
		private Preset _thumbCameraDefaultPreset;

		private AssetBrowser AssetBrowser => panel?.visualTree?.userData as AssetBrowser;

		private static readonly string ThumbCameraPresetPrefix = "Asset Thumbcam - ";

		public AssetResult Asset {
			get => _asset;
			set {
				if (_asset == value) {
					return;
				}
				// toggle empty label
				if (value != null && _asset == null) {
					_previewEditorElement.style.height = _previewEditorElement.resolvedStyle.width;
					_noSelectionElement.AddToClassList("hidden");
					_detailsElement.RemoveFromClassList("hidden");
				}
				if (value == null && _asset != null) {
					_noSelectionElement.RemoveFromClassList("hidden");
					_detailsElement.AddToClassList("hidden");
				}
				_asset = value;
				if (value != null) {
					UpdateDetails();
				}
			}
		}

		private AssetResult _asset;
		private UnityEditor.Editor _previewEditor;
		private Object _object;
		private readonly Button _replaceSelectedButton;
		private readonly Toggle _replaceSelectedKeepName;
		private readonly Label _categoryElement;
		private readonly TextField _descriptionEditElement;
		private readonly Label _descriptionViewElement;
		private readonly Label _dateElement;
		private readonly Label _attributesTitleElement;
		private readonly Button _addAttributeButton;
		private readonly Label _descriptionTitleElement;
		private readonly Label _libraryElement;
		private readonly Image _libraryLockElement;
		private readonly Label _infoTitleElement;
		private readonly Label _infoElement;
		private readonly VisualElement _tagsElement;
		private readonly Label _tagsTitleElement;
		private readonly Button _addTagButton;
		private readonly VisualElement _linksElement;
		private readonly Label _linksTitleElement;
		private readonly Button _addLinkButton;
		private readonly IMGUIContainer _previewEditorElement;
		private readonly VisualElement _thumbCameraContainer;
		private readonly DropdownField _thumbCameraPreset;
		private readonly Label _thumbCameraTitle;
		private readonly VisualElement _assetScaleContainer;
		private readonly EnumField _assetScale;

		public new class UxmlFactory : UxmlFactory<AssetDetailsElement, UxmlTraits> { }

		public AssetDetailsElement()
		{
			RefreshCameraPresets();

			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetDetailsElement.uxml");
			var ui = visualTree.CloneTree();
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetDetailsElement.uss");
			ui.styleSheets.Add(styleSheet);
			Add(ui);

			_noSelectionElement = ui.Q<Label>("nothing-selected");
			_detailsElement = ui.Q<VisualElement>("details");
			_titleElement = ui.Q<Label>("title");
			_libraryElement = ui.Q<Label>("library-name");
			_libraryLockElement = ui.Q<Image>("library-lock");
			_categoryElement = ui.Q<Label>("category-name");
			_dateElement = ui.Q<Label>("date-value");
			_replaceSelectedButton = ui.Q<Button>("replace-selected");
			_replaceSelectedKeepName = ui.Q<Toggle>("replace-selected-keep-name");
			_descriptionTitleElement = ui.Q<Label>("description-title");
			_descriptionViewElement = ui.Q<Label>("description-view");
			_descriptionEditElement = ui.Q<TextField>("description-edit");
			_attributesElement = ui.Q<VisualElement>("attributes");
			_attributesTitleElement = ui.Q<Label>("attributes-title");
			_addAttributeButton = ui.Q<Button>("attributes-add");
			_tagsElement = ui.Q<VisualElement>("tags");
			_tagsTitleElement = ui.Q<Label>("tags-title");
			_addTagButton = ui.Q<Button>("tags-add");
			_linksElement = ui.Q<VisualElement>("links");
			_linksTitleElement = ui.Q<Label>("links-title");
			_addLinkButton = ui.Q<Button>("links-add");
			_infoTitleElement = ui.Q<Label>("info-title");
			_infoElement = ui.Q<Label>("info-view");
			_assetScaleContainer = ui.Q<VisualElement>("asset-scale-container");
			_assetScale = ui.Q<EnumField>("asset-scale");

			_libraryLockElement.image = EditorGUIUtility.IconContent("InspectorLock").image;
			_descriptionEditElement.RegisterCallback<FocusInEvent>(OnDescriptionStartEditing);
			_descriptionEditElement.RegisterCallback<FocusOutEvent>(OnDescriptionEndEditing);
			_addAttributeButton.clicked += OnAddAttribute;
			_addLinkButton.clicked += OnAddLink;
			_addTagButton.clicked += OnAddTag;
			_replaceSelectedButton.clicked += OnReplaceSelected;

			_previewEditorElement = ui.Q<IMGUIContainer>();
			_previewEditorElement.onGUIHandler = OnGUI;
			_previewEditorElement.style.height = _previewEditorElement.resolvedStyle.width;

			// active library dropdown
			_thumbCameraContainer = ui.Q<VisualElement>("thumbnail-container");
			_thumbCameraTitle = ui.Q<Label>("thumbnail-title");
			_thumbCameraPreset = new DropdownField(_thumbCameraPresets.Select(p => p.name[ThumbCameraPresetPrefix.Length..]).ToList(), 0, OnThumbCamPresetChanged) {
				tooltip = "The camera preset used to generate the thumbnail."
			};
			_thumbCameraContainer.Add(_thumbCameraPreset);

			_assetScale.RegisterValueChangedCallback(OnScaleChanged);

			ui.Q<Image>("library-icon").image = Icons.AssetLibrary(IconSize.Small);
			ui.Q<Image>("date-icon").image = Icons.Calendar(IconSize.Small);
			ui.Q<Image>("category-icon").image = EditorGUIUtility.IconContent("d_Folder Icon").image;

		}

		private void OnGUI()
		{
			if (_asset == null) {
				Object.DestroyImmediate(_previewEditor);
				_previewEditor = null;

			} else if (_previewEditor == null || _object != _previewEditor.target) {
				if (_previewEditor != null) {
					Object.DestroyImmediate(_previewEditor);
				}
				_previewEditor = UnityEditor.Editor.CreateEditor(_object);
			}

			if (_previewEditor) {
				var previewSize = _detailsElement.resolvedStyle.width - _detailsElement.resolvedStyle.paddingLeft - _detailsElement.resolvedStyle.paddingRight;
				var rect = EditorGUILayout.GetControlRect(false, previewSize, GUILayout.Width(previewSize));
				_previewEditor.OnInteractivePreviewGUI(rect, GUI.skin.box);
				_previewEditorElement.style.height = _previewEditorElement.resolvedStyle.width;
			}

			_replaceSelectedButton.SetEnabled(Selection.count > 0);

		}

		private void OnReplaceSelected()
		{
			var prefab = _asset.Asset.Object;
			var selection = Selection.gameObjects;
			var newSelection = new List<GameObject>();
			var keepName = _replaceSelectedKeepName.value;

			for (var i = selection.Length - 1; i >= 0; --i)
			{
				var selected = selection[i];
				var prefabType = PrefabUtility.GetPrefabAssetType(prefab);
				GameObject newObject;
				if (prefabType == PrefabAssetType.Regular || prefabType == PrefabAssetType.Variant) {
					newObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

				} else {
					newObject = Object.Instantiate(prefab) as GameObject;
				}
				if (newObject == null) {
					Debug.LogError("Error instantiating prefab.");
					break;
				}
				newObject.name = keepName ? selected.name : prefab.name;

				Undo.RegisterCreatedObjectUndo(newObject, "Replace With Prefab");
				newObject.transform.parent = selected.transform.parent;

				if (newObject.GetComponent(typeof(IMainRenderableComponent)) is IMainRenderableComponent comp) {
					comp.CopyFromObject(selected);

				} else {
					newObject.transform.localPosition = selected.transform.localPosition;
					newObject.transform.localRotation = selected.transform.localRotation;
					newObject.transform.localScale = selected.transform.localScale;
				}
				newObject.transform.SetSiblingIndex(selected.transform.GetSiblingIndex());
				Undo.DestroyObjectImmediate(selected);

				newSelection.Add(newObject);
			}

			Selection.objects = newSelection.Select(go => (Object)go).ToArray();
		}

		private void OnAddAttribute()
		{
			var attribute = _asset.Library.AddAttribute(_asset.Asset, "New Attribute");
			var attributeElement = new KeyValueElement(_asset, attribute, false);
			_attributesElement.Add(attributeElement);
			attributeElement.ToggleEdit();
		}

		private void OnAddLink()
		{
			var link = _asset.Library.AddLink(_asset.Asset, "New Link");
			var linkElement = new KeyValueElement(_asset, link, true);
			_linksElement.Add(linkElement);
			linkElement.ToggleEdit();
		}

		private void OnAddTag()
		{
			var tag = _asset.Library.AddTag(_asset.Asset, "New Tag");
			var tagElement = new TagElement(_asset, tag, _asset.Asset.Tags);
			_tagsElement.Add(tagElement);
			tagElement.ToggleEdit();
		}

		private void OnDescriptionStartEditing(FocusInEvent focusInEvent)
		{
			Undo.RecordObject(_asset.Library, "edit description");
		}

		private void OnDescriptionEndEditing(FocusOutEvent focusOutEvent)
		{
			_asset.Asset.Description = _descriptionEditElement.value;
			_asset.Save();
			EditorUtility.SetDirty(_asset.Library);
			AssetDatabase.SaveAssetIfDirty(_asset.Library);
		}

		private void OnScaleChanged(ChangeEvent<Enum> changeEvent)
		{
			_asset.Asset.Scale = (AssetScale)changeEvent.newValue;
			_asset.Save();
		}

		public void UpdateDetails()
		{
			if (_asset == null) {
				return;
			}

			var browser = AssetBrowser;
			_object = _asset.Asset.Object;
			_titleElement.text = _asset.Asset.Name;
			_libraryElement.text = _asset.Library.Name;
			_categoryElement.text = _asset.Asset.Category.Name;
			_dateElement.text = _asset.Asset.AddedAt.ToLongDateString();
			_descriptionViewElement.text = _asset.Asset.Description;
			_descriptionEditElement.SetValueWithoutNotify(_asset.Asset.Description);

			_attributesElement.Clear();
			if (_asset.Asset.Attributes != null) {
				SetVisibility(_attributesTitleElement, _asset.Asset.Attributes.Count > 0 || !_asset.Library.IsLocked);
				foreach (var attr in _asset.Asset.Attributes) {
					var attrElement = new KeyValueElement(_asset, attr, false);
					_attributesElement.Add(attrElement);
				}
			} else {
				SetVisibility(_attributesTitleElement, !_asset.Library.IsLocked);
			}

			_tagsElement.Clear();
			if (_asset.Asset.Tags != null) {
				SetVisibility(_tagsTitleElement, _asset.Asset.Tags.Count > 0 || !_asset.Library.IsLocked);
				foreach (var tag in _asset.Asset.Tags) {
					var tagElement = new TagElement(_asset, tag, _asset.Asset.Tags) {
						IsActive = browser!.Query.HasTag(tag)
					};
					_tagsElement.Add(tagElement);
				}
			} else {
				SetVisibility(_tagsTitleElement, !_asset.Library.IsLocked);
			}

			_linksElement.Clear();
			if (_asset.Asset.Links != null) {
				SetVisibility(_linksTitleElement, _asset.Asset.Links.Count > 0 || !_asset.Library.IsLocked);
				foreach (var link in _asset.Asset.Links) {
					var linkElement = new KeyValueElement(_asset, link, true);
					_linksElement.Add(linkElement);
				}
			} else {
				SetVisibility(_linksTitleElement, !_asset.Library.IsLocked);
			}

			SetVisibility(_libraryLockElement, _asset.Library.IsLocked);
			SetVisibility(_descriptionTitleElement, !string.IsNullOrEmpty(_asset.Asset.Description) || !_asset.Library.IsLocked);
			SetVisibility(_descriptionViewElement, !string.IsNullOrEmpty(_asset.Asset.Description) && _asset.Library.IsLocked);
			SetVisibility(_descriptionEditElement, !_asset.Library.IsLocked);
			SetVisibility(_addAttributeButton, !_asset.Library.IsLocked);
			SetVisibility(_addTagButton, !_asset.Library.IsLocked);
			SetVisibility(_addLinkButton, !_asset.Library.IsLocked);

			// info
			if (_object is GameObject go) {
				SetVisibility(_infoTitleElement, true);
				SetVisibility(_infoElement, true);
				var (meshes, subMeshes, vertices, triangles, uvs, materials) = CountVertices(go);
				const string separator = ", ";
				_infoElement.text =
					vertices + (vertices == 1 ? " vertex" : " vertices") + separator +
					triangles + " triangle" + (triangles == 1 ? "" : "s") + separator +
					uvs + " uv" + (uvs == 1 ? "" : "s") + separator +
					meshes + " mesh" + (meshes == 1 ? "" : "es") + separator +
					subMeshes + " sub mesh" + (subMeshes == 1 ? "" : "es") + separator +
					materials + " material" + (materials == 1 ? "" : "s");

			} else {
				SetVisibility(_infoTitleElement, false);
				SetVisibility(_infoElement, false);
			}

			SetVisibility(_thumbCameraTitle, !_asset.Library.IsLocked);
			SetVisibility(_thumbCameraContainer, !_asset.Library.IsLocked);
			if (!_asset.Library.IsLocked) {
				var index = _thumbCameraPresets.IndexOf(_asset.Asset.ThumbCameraPreset);
				_thumbCameraPreset.index = index >= 0 ? index : _thumbCameraPresets.IndexOf(_thumbCameraDefaultPreset);
			}

			// scale
			SetVisibility(_assetScaleContainer, !_asset.Library.IsLocked);
			if (!_asset.Library.IsLocked) {
				_assetScale.SetValueWithoutNotify(_asset.Asset.Scale);
			}
		}

		private string OnThumbCamPresetChanged(string shortName)
		{
			if (_asset == null) {
				return shortName;
			}

			var presetName = ThumbCameraPresetPrefix + shortName;
			if (_asset.Asset.ThumbCameraPreset != null && _asset.Asset.ThumbCameraPreset.name == presetName) {
				return shortName;
			}
			_asset.Asset.ThumbCameraPreset = _thumbCameraPresets
				.FirstOrDefault(ps => ps.name == presetName) ?? _thumbCameraDefaultPreset;

			_asset.Save();
			return shortName;
		}

		private void RefreshCameraPresets()
		{
			const string presetPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Presets";
			var presets = Directory.GetFiles(presetPath).Where(p => p.Contains(ThumbCameraPresetPrefix) && !p.Contains(".meta"));
			_thumbCameraPresets = presets.Select(filename => (Preset)AssetDatabase.LoadAssetAtPath(filename, typeof(Preset))).ToList();
			_thumbCameraDefaultPreset = _thumbCameraPresets.First(p => p.name.Contains("Default"));
		}

		private static (int, int, int, int, int, int) CountVertices(GameObject go)
		{
			var vertices = 0;
			var triangles = 0;
			var uvs = 0;
			var meshes = 0;
			var subMeshes = 0;
			var materials = 0;
			foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) {
				var mesh = mf.sharedMesh;
				if (mesh != null) {
					meshes++;
					vertices += mesh.vertexCount;
					triangles += mesh.triangles.Length;
					uvs += mesh.uv.Length;
					subMeshes += mesh.subMeshCount;
				}

				var mr = mf.gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					materials += mr.sharedMaterials.Length;
				}
			}
			return (meshes, subMeshes, vertices, triangles, uvs, materials);
		}

		private void SetVisibility(VisualElement element, bool isVisible)
		{
			switch (isVisible) {
				case false when !element.ClassListContains("hidden"):
					element.AddToClassList("hidden");
					break;
				case true when element.ClassListContains("hidden"):
					element.RemoveFromClassList("hidden");
					break;
			}
		}
	}
}
