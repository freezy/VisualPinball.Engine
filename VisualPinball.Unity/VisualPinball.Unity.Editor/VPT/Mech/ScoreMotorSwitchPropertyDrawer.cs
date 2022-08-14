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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(ScoreMotorSwitch))]
	public class ScoreMotorSwitchPropertyDrawer : PropertyDrawer
	{
		private const float Padding = 2f;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return (EditorGUIUtility.singleLineHeight + Padding) * 3f + 4f;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			var typeProperty = property.FindPropertyRelative(nameof(ScoreMotorSwitch.Type));
			position.y += 2f;
			position.height = EditorGUIUtility.singleLineHeight;

			// save indent level
			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			// name
			EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(ScoreMotorSwitch.Name)), new GUIContent("Name"));
			position.y += EditorGUIUtility.singleLineHeight + Padding;
			EditorGUI.PropertyField(position, typeProperty, new GUIContent("Switch Type"));

			// between
			if (typeProperty.enumValueIndex is (int)ScoreMotorSwitchType.EnableBetween) {
				position.y += EditorGUIUtility.singleLineHeight + Padding;
				var typePosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Between"));

				var width = (typePosition.width - 20) / 2f - 10f;
				var startPosRect = new Rect(typePosition.x, typePosition.y, width, typePosition.height);
				var midRect = new Rect(typePosition.x + width + 6f, typePosition.y, width, typePosition.height);
				var endPosRect = new Rect(typePosition.x + width + 40f, typePosition.y, width, typePosition.height);
			
				EditorGUI.PropertyField(startPosRect, property.FindPropertyRelative(nameof(ScoreMotorSwitch.StartPos)), GUIContent.none);
				EditorGUI.LabelField(midRect, new GUIContent("And:"));
				EditorGUI.PropertyField(endPosRect, property.FindPropertyRelative(nameof(ScoreMotorSwitch.EndPos)), GUIContent.none);
			}

			// every
			if (typeProperty.enumValueIndex is (int)ScoreMotorSwitchType.EnableEvery)
			{
				position.y += EditorGUIUtility.singleLineHeight + Padding;
				var typePosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Every"));

				var width = (typePosition.width - 20) / 2f - 10f;
				var freqRect = new Rect(typePosition.x, typePosition.y, width, typePosition.height);
				var midRect = new Rect(typePosition.x + width + 6f, typePosition.y, width, typePosition.height);
				var durationRect = new Rect(typePosition.x + width + 40f, typePosition.y, width, typePosition.height);	
			
				EditorGUI.PropertyField(freqRect, property.FindPropertyRelative(nameof(ScoreMotorSwitch.Freq)), GUIContent.none);
				EditorGUI.LabelField(midRect, new GUIContent("For:"));
				EditorGUI.PropertyField(durationRect, property.FindPropertyRelative(nameof(ScoreMotorSwitch.Duration)), GUIContent.none);
			}

			// set indent back to what it was
			EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}
	}
}
