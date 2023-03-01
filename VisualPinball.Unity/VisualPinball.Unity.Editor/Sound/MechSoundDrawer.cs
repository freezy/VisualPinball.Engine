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

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(MechSound))]
	public class MechSoundDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 5 + 4f;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			// retrieve reference to GO and component
			var mechSoundsComponent = (MechSoundsComponent)property.serializedObject.targetObject;
			var soundEmitter = mechSoundsComponent.GetComponent<ISoundEmitter>();

			EditorGUI.BeginProperty(position, label, property);
			
			// init height
			position.height = EditorGUIUtility.singleLineHeight;

			// trigger drop-down
			var triggerIdProperty = property.FindPropertyRelative(nameof(MechSound.TriggerId));
			var triggers = soundEmitter.AvailableTriggers;
			if (triggers.Length > 0) {
				var triggerIndex = triggers.ToList().FindIndex(t => t.Id == triggerIdProperty.stringValue);
				if (triggerIndex == -1) { // pre-select first trigger in list, if none set.
					triggerIndex = 0;
				}
				EditorGUI.BeginChangeCheck();
				triggerIndex = EditorGUI.Popup(position, "Trigger on", triggerIndex, triggers.Select(t => t.Name).ToArray());
				if (EditorGUI.EndChangeCheck()) {
					triggerIdProperty.stringValue = triggers[triggerIndex].Id;
				}
			} else {
				EditorGUI.LabelField(position, "No Triggers found.");
			}
			position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
			
			// sound object picker
			var soundProperty = property.FindPropertyRelative(nameof(MechSound.Sound));
			EditorGUI.BeginChangeCheck();
			var soundValue = EditorGUI.ObjectField(position, "Sound", soundProperty.objectReferenceValue, typeof(SoundAsset), true);
			if (EditorGUI.EndChangeCheck()) {
				soundProperty.objectReferenceValue = soundValue;
			}
			position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			// volume
			var volumeProperty = property.FindPropertyRelative(nameof(MechSound.Volume));
			EditorGUI.PropertyField(position, volumeProperty);
			position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
			
			// action
			var actionProperty = property.FindPropertyRelative(nameof(MechSound.Action));
			EditorGUI.PropertyField(position, actionProperty);
			position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
			
			// fade
			var fadeProperty = property.FindPropertyRelative(nameof(MechSound.Fade));
			EditorGUI.PropertyField(position, fadeProperty);
			position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			EditorGUI.EndProperty();
		}
	}
}
