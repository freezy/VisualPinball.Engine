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
	[CustomPropertyDrawer(typeof(MechMark))]
	public class MechMarkPropertyDrawer : PropertyDrawer
	{
		private const float Padding = 2f;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var lines = property.FindPropertyRelative(nameof(MechMark.Type)).enumValueIndex switch {
				(int)MechMarkSwitchType.EnableBetween => 3f,
				(int)MechMarkSwitchType.AlwaysPulse => 4f,
				(int)MechMarkSwitchType.PulseBetween => 4f,
				_ => 3f
			};
			return (EditorGUIUtility.singleLineHeight + Padding) * lines + 4f;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			var typeProperty = property.FindPropertyRelative(nameof(MechMark.Type));
			position.y += 2f;
			position.height = EditorGUIUtility.singleLineHeight;

			// save indent level
			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			// name
			EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(MechMark.Name)), new GUIContent("Name"));
			position.y += EditorGUIUtility.singleLineHeight + Padding;
			EditorGUI.PropertyField(position, typeProperty, new GUIContent("Switch Type"));

			// step line
			if (typeProperty.enumValueIndex is (int)MechMarkSwitchType.EnableBetween or (int)MechMarkSwitchType.PulseBetween) {
				position.y += EditorGUIUtility.singleLineHeight + Padding;
				var stepPosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Between"));

				var width = (stepPosition.width - 40) / 2f - 10f;
				var fromRect = new Rect(stepPosition.x, stepPosition.y, width, stepPosition.height);
				var toRect = new Rect(stepPosition.x + width + 20f, stepPosition.y, width, stepPosition.height);
				var midRect = new Rect(stepPosition.x + width + 6f, stepPosition.y, width, stepPosition.height);
				var rightRect = new Rect(stepPosition.x + stepPosition.width - 40 + 4, stepPosition.y, 40, stepPosition.height);

				EditorGUI.PropertyField(fromRect, property.FindPropertyRelative(nameof(MechMark.StepBeginning)), GUIContent.none);
				EditorGUI.LabelField(midRect, new GUIContent("-"));
				EditorGUI.PropertyField(toRect, property.FindPropertyRelative(nameof(MechMark.StepEnd)), GUIContent.none);
				EditorGUI.LabelField(rightRect, new GUIContent("steps"));
			}

			if (typeProperty.enumValueIndex is (int)MechMarkSwitchType.PulseBetween or (int)MechMarkSwitchType.AlwaysPulse) {
				position.y += EditorGUIUtility.singleLineHeight + Padding;
				EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(MechMark.PulseFreq)), new GUIContent("Pulse Each"));
			}

			if (typeProperty.enumValueIndex is (int)MechMarkSwitchType.AlwaysPulse) {
				position.y += EditorGUIUtility.singleLineHeight + Padding;
				EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(MechMark.PulseDuration)), new GUIContent("Pulse For"));
			}

			// set indent back to what it was
			EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}
	}
}
