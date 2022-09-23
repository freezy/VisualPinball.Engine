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

// ReSharper disable InconsistentNaming

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(MaterialVariationOverride))]
	public class MaterialVariationOverridePropertyDrawer : PropertyDrawer
	{
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			// Create a new VisualElement to be the root of our inspector UI
			VisualElement myInspector = new VisualElement();

			// Load and clone a visual tree from UXML
			VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/MaterialVariationOverridePropertyDrawer.uxml");
			visualTree.CloneTree(myInspector);

			// Return the finished inspector UI
			return myInspector;


			// // Create a new VisualElement to be the root the property UI
			// var container = new VisualElement();
			//
			// // Create drawer UI using C#
			// var popup = new UnityEngine.UIElements.PopupWindow();
			// popup.text = "Material Override";
			// popup.Add(new PropertyField(property.FindPropertyRelative("Name"), "Name"));
			// popup.Add(new PropertyField(property.FindPropertyRelative("Material"), "Material"));
			// container.Add(popup);
			//
			// // Return the finished UI
			// return container;
		}
	}
}
