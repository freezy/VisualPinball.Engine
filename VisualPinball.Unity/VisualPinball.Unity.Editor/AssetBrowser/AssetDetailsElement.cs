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

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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

		public AssetData Asset {
			get => _data;
			set {
				if (_data == value) {
					return;
				}
				// toggle empty label
				if (value != null && _data == null) {
					_noSelectionElement.AddToClassList("hidden");
					_detailsElement.RemoveFromClassList("hidden");
				}
				if (value == null && _data != null) {
					_noSelectionElement.RemoveFromClassList("hidden");
					_detailsElement.AddToClassList("hidden");
				}
				_data = value;
				if (value != null) {
					UpdateDetails();
				}
			}
		}

		private AssetData _data;
		private UnityEditor.Editor _previewEditor;
		private Object _object;
		private readonly Label _categoryElement;
		private readonly TextField _descriptionEditElement;
		private readonly Label _descriptionViewElement;
		private readonly Label _dateElement;
		private readonly Label _attributesTitleElement;
		private readonly Button _addAttributeButton;
		private readonly Label _descriptionTitleElement;
		private readonly Label _libraryElement;
		private readonly Image _libraryLockElement;

		public new class UxmlFactory : UxmlFactory<AssetDetailsElement, UxmlTraits> { }

		public AssetDetailsElement()
		{
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
			_descriptionTitleElement = ui.Q<Label>("description-title");
			_descriptionViewElement = ui.Q<Label>("description-view");
			_descriptionEditElement = ui.Q<TextField>("description-edit");
			_attributesElement = ui.Q<VisualElement>("attributes");
			_attributesTitleElement = ui.Q<Label>("attributes-title");
			_addAttributeButton = ui.Q<Button>("add");

			_libraryLockElement.image = EditorGUIUtility.IconContent("InspectorLock").image;
			_descriptionEditElement.RegisterValueChangedCallback(OnDescriptionEdited);
			_addAttributeButton.clicked += OnAddAttribute;

			ui.Q<IMGUIContainer>().onGUIHandler = OnGUI;
			ui.Q<Image>("library-icon").image = Icons.AssetLibrary(IconSize.Small);
			ui.Q<Image>("date-icon").image = Icons.Calendar(IconSize.Small);
			ui.Q<Image>("category-icon").image = EditorGUIUtility.IconContent("d_Folder Icon").image;

		}

		private void OnGUI()
		{
			if (_data == null) {
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
			}
		}

		private void OnAddAttribute()
		{
			var attribute = _data.Library.AddAttribute(_data.Asset, "New Attribute");
			var attributeElement = new LibraryAttributeElement(_data, attribute);
			_attributesElement.Add(attributeElement);
			attributeElement.ToggleEdit();
		}

		private void OnDescriptionEdited(ChangeEvent<string> evt)
		{
			_data.Asset.Description = evt.newValue;
			_data.Save();
		}

		public void UpdateDetails()
		{
			if (_data == null) {
				return;
			}
			_object = _data.Asset.LoadAsset();
			_titleElement.text = _object.name;
			_libraryElement.text = _data.Library.Name;
			_categoryElement.text = _data.Asset.Category.Name;
			_dateElement.text = _data.Asset.AddedAt.ToLongDateString();
			_descriptionViewElement.text = _data.Asset.Description;
			_descriptionEditElement.SetValueWithoutNotify(_data.Asset.Description);

			_attributesElement.Clear();
			foreach (var attr in _data.Asset.Attributes) {
				var categoryElement = new LibraryAttributeElement(_data, attr);
				_attributesElement.Add(categoryElement);
			}

			SetVisibility(_libraryLockElement, _data.Library.IsLocked);
			SetVisibility(_descriptionTitleElement, !string.IsNullOrEmpty(_data.Asset.Description) || !_data.Library.IsLocked);
			SetVisibility(_descriptionViewElement, !string.IsNullOrEmpty(_data.Asset.Description) && _data.Library.IsLocked);
			SetVisibility(_descriptionEditElement, !_data.Library.IsLocked);
			SetVisibility(_attributesTitleElement, _data.Asset.Attributes.Count > 0 || !_data.Library.IsLocked);
			SetVisibility(_addAttributeButton, !_data.Library.IsLocked);
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
