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

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(AssetMaterialVariation))]
	public class AssetMaterialVariationPropertyDrawer : PropertyDrawer
	{
		private MaterialSlotDropdownElement _slotField;

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetMaterialVariationPropertyDrawer.uxml");
			visualTree.CloneTree(ui);

			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetMaterialVariationPropertyDrawer.uss");
			ui.styleSheets.Add(styleSheet);

			if (property.serializedObject.targetObject is Asset asset) {

				// object dropdown
				var objField = ui.Q<ObjectDropdownElement>("object-field");
				objField.AddObjectsToDropdown<Renderer>(asset.Object);
				objField.RegisterValueChangedCallback(OnObjectChanged);
				var obj = property.FindPropertyRelative(nameof(AssetMaterialVariation.Object));
				if (obj != null && obj.objectReferenceValue != null) {
					objField.SetValue(obj.objectReferenceValue);
				}

				// material slot dropdown
				_slotField = ui.Q<MaterialSlotDropdownElement>("slot-field");
				if (objField.HasValue) {
					_slotField.PopulateChoices(objField.Value as GameObject);
				}
				var slot = property.FindPropertyRelative(nameof(AssetMaterialVariation.Slot));
				_slotField.SetValue(slot.intValue);

				// overrides - unity tries to be "smart" and copies over the values of the last element when adding
				//             a new element, which includes our unique Id, which only gets generated when it's not
				//             set. so we reset it manually here, which is annoying and ugly AF, but the alternative
				//             would be to re-implement the whole fucking ListView.
				var overridesList = ui.Q<ListView>("overrides");
				overridesList.itemsAdded += ints => {
					foreach (var i in ints) {
						var guid = Guid.NewGuid().ToString();
						var idProp = property.FindPropertyRelative($"Overrides.Array.data[{i}].{nameof(AssetMaterialOverride.Id)}");
						idProp.stringValue = guid;
						var nameProp = property.FindPropertyRelative($"Overrides.Array.data[{i}].{nameof(AssetMaterialOverride.Name)}");
						nameProp.stringValue = string.Empty;
						var matProp = property.FindPropertyRelative($"Overrides.Array.data[{i}].{nameof(AssetMaterialOverride.Material)}");
						matProp.objectReferenceValue = null;
						idProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
					}
				};
			}

			return ui;
		}

		private void OnObjectChanged(Object obj)
		{
			if (_slotField != null && obj is GameObject go) {
				_slotField.PopulateChoices(go);
			}
		}
	}
}
