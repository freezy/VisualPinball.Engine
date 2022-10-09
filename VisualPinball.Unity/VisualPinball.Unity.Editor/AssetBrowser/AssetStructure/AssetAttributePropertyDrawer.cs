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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(AssetAttribute))]
	public class AssetAttributePropertyDrawer : PropertyDrawer
	{
		// property drawers are recycled, so store those per path.
		private readonly Dictionary<string, SuggestingTextField> _keyField = new();
		private readonly Dictionary<string, SuggestingTextField> _valueField = new();

		private AssetLibrary _library;

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetAttributePropertyDrawer.uxml");
			visualTree.CloneTree(ui);

			_keyField[property.propertyPath] = ui.Q<SuggestingTextField>("key-field");
			if (property.serializedObject.targetObject is Asset asset && asset.Library != null) {
				_library = asset.Library;
				_keyField[property.propertyPath].SuggestOptions = asset.Library.GetAttributeKeys().ToArray();
			}

			_valueField[property.propertyPath] = ui.Q<SuggestingTextField>("value-field");
			_valueField[property.propertyPath].RegisterCallback<FocusInEvent>(evt => OnValueFocus(property.propertyPath));

			ui.AddManipulator(new ContextualMenuManipulator(evt => AddAssetContextMenu(
				evt,
				property,
				ui.panel.visualTree.userData as AssetBrowser
			)));

			return ui;
		}

		private void AddAssetContextMenu(ContextualMenuPopulateEvent evt, SerializedProperty property, AssetBrowser browser)
		{
			var destinationAssetResults = browser.NonActiveSelection.ToArray();
			if (destinationAssetResults.Length > 0) {
				var suffix = destinationAssetResults.Length == 1 ? "" : "s";

				// context menu: Add to selected
				evt.menu.AppendAction($"Add to selected asset{suffix}", _ => {
					var attrKey = _keyField[property.propertyPath].Value;
					var attrValue = _valueField[property.propertyPath].Value;
					foreach (var destAsset in destinationAssetResults.Select(r => r.Asset)) {
						destAsset.AddAttribute(attrKey, attrValue);
						destAsset.Save();
					}
					EditorUtility.DisplayDialog($"Add to selected asset{suffix}", $"Added values of attribute \"{attrKey}\" to the {destinationAssetResults.Length} other selected asset{suffix}.", "OK");
				});

				// context menu: Replace in selected
				evt.menu.AppendAction($"Replace in selected asset{suffix}", _ => {
					var attrKey = _keyField[property.propertyPath].Value;
					var attrValue = _valueField[property.propertyPath].Value;
					foreach (var destAsset in destinationAssetResults.Select(r => r.Asset)) {
						destAsset.ReplaceAttribute(attrKey, attrValue);
						destAsset.Save();
					}
					EditorUtility.DisplayDialog($"Replace in selected asset{suffix}", $"Replaced values of attribute \"{attrKey}\" in the {destinationAssetResults.Length} other selected asset{suffix}.", "OK");
				});
			}
		}

		private void OnValueFocus(string propertyPath)
		{
			if (!string.IsNullOrEmpty(_keyField[propertyPath].Value) && _library != null) {
				_valueField[propertyPath].SuggestOptions = _library.GetAttributeValues(_keyField[propertyPath].Value).ToArray();
			}
		}
	}
}
