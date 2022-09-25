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

			return ui;
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
