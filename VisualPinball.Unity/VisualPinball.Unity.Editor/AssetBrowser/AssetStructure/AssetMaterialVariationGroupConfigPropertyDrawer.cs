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
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(AssetMaterialVariationGroupConfig))]
	public class AssetMaterialVariationGroupConfigPropertyDrawer : PropertyDrawer
	{
		// property drawers are recycled, so don't store anything in the members!

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var root = new VisualElement();

			var groupByProp = property.FindPropertyRelative(nameof(AssetMaterialVariationGroupConfig.GroupBy));
			var objectProp  = property.FindPropertyRelative(nameof(AssetMaterialVariationGroupConfig.Object));

			var groupByField = new PropertyField(groupByProp, "Group By");
			var objectField = new ObjectDropdownElement(objectProp, "Object");

			if (property.serializedObject.targetObject is Asset asset) {
				objectField.AddObjectsToDropdown<Renderer>(asset.Object, true);
			}

			root.Add(groupByField);
			root.Add(objectField);

			UpdateObjectVisibility();
			groupByField.RegisterCallback<SerializedPropertyChangeEvent>(_ => UpdateObjectVisibility());

			return root;

			void UpdateObjectVisibility()
			{
				var show = (AssetVariationGroupBy)groupByProp.enumValueIndex == AssetVariationGroupBy.Object;
				objectField.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
			}
		}
	}
}
