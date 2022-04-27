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
			_categoryElement = ui.Q<Label>("category-name");
			_attributesElement = ui.Q<VisualElement>("attributes");

			var button = ui.Q<Button>("add");
			button.clicked += OnAddAttribute;

			var editorElement = ui.Q<IMGUIContainer>();
			editorElement.onGUIHandler = OnGUI;

			var categoryIcon = ui.Q<Image>("category-icon");
			categoryIcon.image = EditorGUIUtility.IconContent("d_Folder Icon").image;
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

		private void UpdateDetails()
		{
			_object = _data.Asset.LoadAsset();
			_titleElement.text = _object.name;

			//var category = _data.Library.GetCategories()
			_categoryElement.text = _data.Asset.Category.Name;

			_attributesElement.Clear();
			foreach (var attr in _data.Asset.Attributes) {
				var categoryElement = new LibraryAttributeElement(_data, attr);
				_attributesElement.Add(categoryElement);
			}
		}
	}
}
