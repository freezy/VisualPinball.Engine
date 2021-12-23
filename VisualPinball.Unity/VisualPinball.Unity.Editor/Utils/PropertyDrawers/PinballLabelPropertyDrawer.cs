// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{

	[CustomPropertyDrawer(typeof(PinballLabel))]

	public class PinballLabelPropertyDrawer : PropertyDrawer
	{
		private GUIStyle labelStyle = null;
		private GUIStyle labelButtonStyle = null;

		protected void OnSelectCallback(PopupListElement element)
		{
			element.selected = !element.selected;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (labelStyle == null) {
				labelStyle = GUI.skin.FindStyle("AssetLabel");
			}

			if (labelButtonStyle == null) {
				labelButtonStyle = GUI.skin.FindStyle("AssetLabel Icon");
			}

			var fullLabel = property.FindPropertyRelative("_fullLabel");

			if (fullLabel != null) {
				Rect labelRect = position;

				var labelsList = new PopupLabelList.InputData() { m_AllowCustom = true, m_CloseOnSelection = false, m_EnableAutoCompletion = true, m_SortAlphabetically = true, m_OnSelectCallback = OnSelectCallback };
				labelsList.m_ListElements.Add(new PopupListElement("Manufacturers.Bally"));
				labelsList.m_ListElements.Add(new PopupListElement("Manufacturers.Stern"));
				labelsList.m_ListElements.Add(new PopupListElement("Manufacturers.Williams"));
				labelsList.m_ListElements.Add(new PopupListElement("Manufacturers.Sega"));
				labelsList.m_ListElements.Add(new PopupListElement("Decades.1980"));
				labelsList.m_ListElements.Add(new PopupListElement("Decades.1990"));
				labelsList.m_ListElements.Add(new PopupListElement("Decades.2000"));
				labelsList.m_ListElements.Add(new PopupListElement("AFM"));

				//if (string.IsNullOrEmpty(fullLabel.stringValue)) {
				//	labelRect.width = labelButtonStyle.margin.left + labelButtonStyle.fixedWidth + labelButtonStyle.padding.right;
				//	if (EditorGUI.DropdownButton(labelRect, GUIContent.none, FocusType.Passive, labelButtonStyle)) {
				//		PopupWindow.Show(labelRect, new PopupLabelList(labelsList));
				//	}
				//} else {
					labelRect.width = labelStyle.CalcSize(new GUIContent(fullLabel.stringValue)).x;
					fullLabel.stringValue = GUI.TextField(labelRect, fullLabel.stringValue, labelStyle);
				//}

			}
		}
	}
}
