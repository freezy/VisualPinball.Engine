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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(StepRotatorMark))]
	public class StepRotatorMarkPropertyDrawer : PropertyDrawer
	{
		private const float Padding = 2f;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => (EditorGUIUtility.singleLineHeight + Padding) * 3f;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			position.height = EditorGUIUtility.singleLineHeight;

			// save indent level
			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			// description and switch id
			EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(StepRotatorMark.Description)), new GUIContent("Title"));
			position.y += EditorGUIUtility.singleLineHeight + Padding;
			EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(StepRotatorMark.SwitchId)));

			// step line
			position.y += EditorGUIUtility.singleLineHeight + Padding;
			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Between"));

			var width = (position.width - 40) / 2f - 10f;
			var fromRect = new Rect(position.x, position.y, width, position.height);
			var toRect = new Rect(position.x + width + 20f, position.y, width, position.height);
			var midRect = new Rect(position.x + width + 6f, position.y, width, position.height);
			var rightRect = new Rect(position.x + position.width - 40 + 4, position.y, 40, position.height);

			EditorGUI.PropertyField(fromRect, property.FindPropertyRelative(nameof(StepRotatorMark.StepBeginning)), GUIContent.none);
			EditorGUI.LabelField(midRect, new GUIContent("-"));
			EditorGUI.PropertyField(toRect, property.FindPropertyRelative(nameof(StepRotatorMark.StepEnd)), GUIContent.none);
			EditorGUI.LabelField(rightRect, new GUIContent("steps"));

			// set indent back to what it was
			EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}
	}
}
