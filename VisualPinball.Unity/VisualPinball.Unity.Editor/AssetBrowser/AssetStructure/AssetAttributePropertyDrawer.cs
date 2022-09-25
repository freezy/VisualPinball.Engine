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
		private SuggestingTextField _keyField;
		private SuggestingTextField _valueField;

		private AssetLibrary _library;

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetAttributePropertyDrawer.uxml");
			visualTree.CloneTree(ui);

			_keyField = ui.Q<SuggestingTextField>("key-field");
			if (property.serializedObject.targetObject is Asset asset && asset.Library != null) {
				_library = asset.Library;
				_keyField.SuggestOptions = asset.Library.GetAttributeKeys().ToArray();
			}

			_valueField = ui.Q<SuggestingTextField>("value-field");
			_valueField.RegisterCallback<FocusInEvent>(OnValueFocus);

			return ui;
		}

		private void OnValueFocus(FocusInEvent evt)
		{
			if (!string.IsNullOrEmpty(_keyField.Value) && _library != null) {
				_valueField.SuggestOptions = _library.GetAttributeValues(_keyField.Value).ToArray();
			}
		}
	}
}
