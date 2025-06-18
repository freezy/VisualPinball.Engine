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

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	public class AssetMaterialVariationBasePropertyDrawer : PropertyDrawer
	{
		protected VisualElement CreatePropertyGUI(SerializedProperty property, string uxmlPath, string ussPath)
		{
			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
			visualTree.CloneTree(ui);

			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
			ui.styleSheets.Add(styleSheet);

			if (property.serializedObject.targetObject is Asset asset) {

				var objField = ui.Q<ObjectDropdownElement>("object-field");
				var slotField = ui.Q<MaterialSlotDropdownElement>("slot-field");

				// object dropdown
				objField.AddObjectsToDropdown<Renderer>(asset.Object, true);
				objField.RegisterValueChangedCallback(obj => OnObjectChanged(slotField, obj));
				var obj = property.FindPropertyRelative(nameof(AssetMaterialVariation.Object));
				if (obj != null && obj.objectReferenceValue != null) {
					objField.SetValue(obj.objectReferenceValue);
				}

				// material slot dropdown
				if (objField.HasValue) {
					slotField.PopulateChoices(objField.Value as GameObject);
				}
				var slot = property.FindPropertyRelative(nameof(AssetMaterialVariation.Slot));
				slotField.SetValue(slot.intValue);
			}

			return ui;
		}

		private static void OnObjectChanged(MaterialSlotDropdownElement slotField, Object obj)
		{
			if (slotField != null && obj is GameObject go) {
				slotField.PopulateChoices(go);
			}
		}
	}
}
