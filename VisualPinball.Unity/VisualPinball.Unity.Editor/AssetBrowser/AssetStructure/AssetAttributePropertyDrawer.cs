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

using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(AssetAttribute))]
	public class AssetAttributePropertyDrawer : PropertyDrawer
	{
		// property drawers are recycled, so don't store anything in the members!

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetAttributePropertyDrawer.uxml");
			visualTree.CloneTree(ui);

			var keyField = ui.Q<SuggestingTextField>("key-field");
			if (property.serializedObject.targetObject is Asset asset && asset.Library != null) {
				keyField.SuggestOptions = asset.Library.GetAttributeKeys().ToArray();

				var valueField = ui.Q<SuggestingTextField>("value-field");
				valueField.RegisterCallback<FocusInEvent>(evt => OnValueFocus(asset.Library, keyField, valueField));

				ui.AddManipulator(new ContextualMenuManipulator(evt => AddAssetContextMenu(
					evt,
					ui.panel.visualTree.userData as AssetBrowser,
					keyField,
					valueField
				)));
			}
			return ui;
		}

		private void AddAssetContextMenu(ContextualMenuPopulateEvent evt, AssetBrowser browser, SuggestingTextField keyField, SuggestingTextField valueField)
		{
			var destinationAssetResults = browser.NonActiveSelection.ToArray();
			if (destinationAssetResults.Length > 0) {
				var suffix = destinationAssetResults.Length == 1 ? "" : "s";

				evt.menu.AppendSeparator();

				// context menu: Add to selected
				evt.menu.AppendAction($"Add to Selected Asset{suffix}", _ => {
					var attrKey = keyField.Value;
					var attrValue = valueField.Value;
					foreach (var destAsset in destinationAssetResults.Select(r => r.Asset)) {
						destAsset.AddAttribute(attrKey, attrValue);
						destAsset.Save();
					}
					EditorUtility.DisplayDialog($"Add to Selected Asset{suffix}", $"Added values of attribute \"{attrKey}\" to the {destinationAssetResults.Length} other selected asset{suffix}.", "OK");
				});

				// context menu: Replace in selected
				evt.menu.AppendAction($"Replace in Selected Asset{suffix}", _ => {
					var attrKey = keyField.Value;
					var attrValue = valueField.Value;
					foreach (var destAsset in destinationAssetResults.Select(r => r.Asset)) {
						destAsset.ReplaceAttribute(attrKey, attrValue);
						destAsset.Save();
					}
					EditorUtility.DisplayDialog($"Replace in Selected Asset{suffix}", $"Replaced values of attribute \"{attrKey}\" in the {destinationAssetResults.Length} other selected asset{suffix}.", "OK");
				});
			}
		}

		private static void OnValueFocus(AssetLibrary library, SuggestingTextField keyField, SuggestingTextField valueField)
		{
			if (!string.IsNullOrEmpty(keyField.Value) && library != null) {
				valueField.SuggestOptions = library.GetAttributeValues(keyField.Value).ToArray();
			}
		}
	}
}
