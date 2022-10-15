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
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(AssetTag))]
	public class AssetTagPropertyDrawer : PropertyDrawer
	{
		// property drawers are recycled, so store those per path.
		private readonly Dictionary<string, SuggestingTextField> _tagField = new();

		private AssetLibrary _library;

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetTagPropertyDrawer.uxml");
			visualTree.CloneTree(ui);

			if (property.serializedObject.targetObject is Asset asset && asset.Library != null) {
				_library = asset.Library;
			}

			_tagField[property.propertyPath] = ui.Q<SuggestingTextField>("tag-field");
			_tagField[property.propertyPath].RegisterCallback<FocusInEvent>(evt => OnValueFocus(property.propertyPath));

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

				evt.menu.AppendSeparator();

				// context menu: Add to selected
				evt.menu.AppendAction($"Add to Selected Asset{suffix}", _ => {
					var tagName = _tagField[property.propertyPath].Value;
					foreach (var destAsset in destinationAssetResults.Select(r => r.Asset)) {
						destAsset.AddTag(tagName);
						destAsset.Save();
					}
					EditorUtility.DisplayDialog($"Add to Selected Asset{suffix}", $"Added tag \"{tagName}\" to the {destinationAssetResults.Length} other selected asset{suffix}.", "OK");
				});

				// context menu: Remove in selected
				evt.menu.AppendAction($"Remove in Selected Asset{suffix}", _ => {
					var tagName = _tagField[property.propertyPath].Value;
					foreach (var destAsset in destinationAssetResults.Select(r => r.Asset)) {
						destAsset.RemoveTag(tagName);
						destAsset.Save();
					}
					EditorUtility.DisplayDialog($"Remove in Selected Asset{suffix}", $"Removed tag \"{tagName}\" from the {destinationAssetResults.Length} other selected asset{suffix}.", "OK");
				});
			}
		}

		private void OnValueFocus(string propertyPath)
		{
			if (_library != null) {
				var tags = new HashSet<string>(_library.GetAllTags());
				foreach (var otherField in _tagField.Values) {
					if (!string.IsNullOrEmpty(otherField.Value) && tags.Contains(otherField.Value)) {
						tags.Remove(otherField.Value);
					}
				}
				_tagField[propertyPath].SuggestOptions = tags.ToArray();
			}
		}
	}
}
