// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
		// property drawers are recycled, so don't store anything in the members!

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetTagPropertyDrawer.uxml");
			visualTree.CloneTree(ui);

			if (property.serializedObject.targetObject is Asset asset && asset.Library != null) {

				var tagField = ui.Q<SuggestingTextField>("tag-field");
				tagField.RegisterCallback<FocusInEvent>(evt => OnValueFocus(asset, tagField));

				ui.AddManipulator(new ContextualMenuManipulator(evt => AddAssetContextMenu(
					evt,
					ui.panel.visualTree.userData as AssetBrowser,
					tagField
				)));
			}

			return ui;
		}

		private static void AddAssetContextMenu(ContextualMenuPopulateEvent evt, AssetBrowser browser, SuggestingTextField tagField)
		{
			var destinationAssetResults = browser.NonActiveSelection.ToArray();
			if (destinationAssetResults.Length > 0) {
				var suffix = destinationAssetResults.Length == 1 ? "" : "s";

				evt.menu.AppendSeparator();

				// context menu: Add to selected
				evt.menu.AppendAction($"Add to Selected Asset{suffix}", _ => {
					var tagName = tagField.Value;
					foreach (var destAsset in destinationAssetResults.Select(r => r.Asset)) {
						destAsset.AddTag(tagName);
						destAsset.Save();
					}
					EditorUtility.DisplayDialog($"Add to Selected Asset{suffix}", $"Added tag \"{tagName}\" to the {destinationAssetResults.Length} other selected asset{suffix}.", "OK");
				});

				// context menu: Remove in selected
				evt.menu.AppendAction($"Remove in Selected Asset{suffix}", _ => {
					var tagName = tagField.Value;
					foreach (var destAsset in destinationAssetResults.Select(r => r.Asset)) {
						destAsset.RemoveTag(tagName);
						destAsset.Save();
					}
					EditorUtility.DisplayDialog($"Remove in Selected Asset{suffix}", $"Removed tag \"{tagName}\" from the {destinationAssetResults.Length} other selected asset{suffix}.", "OK");
				});
			}
		}

		private static void OnValueFocus(Asset asset, SuggestingTextField tagField)
		{
			var tags = new HashSet<string>(asset.Library.GetAllTags());
			foreach (var existingTag in asset.Tags) {
				if (!string.IsNullOrEmpty(existingTag.TagName) && tags.Contains(existingTag.TagName)) {
					tags.Remove(existingTag.TagName);
				}
			}
			tagField.SuggestOptions = tags.ToArray();
		}
	}
}
