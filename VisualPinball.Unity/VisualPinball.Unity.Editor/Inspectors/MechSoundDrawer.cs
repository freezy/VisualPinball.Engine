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

using Unity.Entities;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using static VisualPinball.Unity.MechSoundsComponent;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(MechSound))]
	public class MechSoundDrawer : PropertyDrawer
	{
		private SoundTrigger _selectedTrigger;
		private const float _buttonWidth = 150;

		private SoundTrigger[] _availiableTriggers;
		private VolumeEmitter[] _availableEmitters;
		private const string _volEmitter = "fixed";
		private const string _volEmitterName = "Fixed";

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			MechSoundsComponent ob = (MechSoundsComponent)property.serializedObject.targetObject;
			GameObject go = ob.gameObject;

			EditorGUI.BeginProperty(position, label, property);

			EditorGUI.LabelField(position, label);

			var _triggerProperty = property.FindPropertyRelative("Trigger");
			var _soundProperty = property.FindPropertyRelative("Sound");
			var _volumeSelectionProperty = property.FindPropertyRelative("Volume");
			var _volumeProperty = property.FindPropertyRelative("VolumeValue");
			var _actionSelectionProperty = property.FindPropertyRelative("Action");
			var _fadeProperty = property.FindPropertyRelative("Fade");

			_availiableTriggers = go.GetComponent<FlipperComponent>().AvailableTriggers;
			_triggerProperty.intValue = EditorGUILayout.Popup("Trigger", _triggerProperty.intValue, GetTriggerOptions(_availiableTriggers));
			
			_selectedTrigger = GetSelectedTrigger(_triggerProperty.intValue);
			_availableEmitters = go.GetComponent<FlipperComponent>().GetVolumeEmitters(_selectedTrigger);
			
			EditorGUILayout.Space(5);
			_soundProperty.objectReferenceValue = EditorGUILayout.ObjectField("Sound", _soundProperty.objectReferenceValue, typeof(SoundAsset), true);

			EditorGUILayout.Space(5);
			_volumeSelectionProperty.intValue = EditorGUILayout.Popup(new GUIContent("Volume", "Depends on trigger selected: \n 'Fixed'-Not dependent on any playfield action. \n 'Ball Velocity'- Gameplay-related (collision)."), _volumeSelectionProperty.intValue, GetEmitterOptions(_availableEmitters));

			EditorGUILayout.Space(5);
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("", GUILayout.Width(EditorGUIUtility.labelWidth));
			_volumeProperty.floatValue = EditorGUILayout.Slider("", _volumeProperty.floatValue, 0.1f, 2);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(5);
			EditorGUILayout.PropertyField(_actionSelectionProperty);

			EditorGUILayout.Space(5);
			EditorGUILayout.BeginHorizontal();
			_fadeProperty.floatValue = EditorGUILayout.Slider("Fade", _fadeProperty.floatValue, 0, 300);
			GUILayout.Label("ms", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
			EditorGUILayout.EndHorizontal();

			EditorGUI.EndProperty();

			
		}


		#region Set methods

		private string[] GetTriggerOptions(SoundTrigger[] triggers)
		{
			int index = triggers.Length;
			string[] options = new string[index];

			for (int i = 0; i < index; i++)
			{
				options[i] = triggers[i].Name;
			}

			return options;
		}


		private SoundTrigger GetSelectedTrigger(int index)
		{
			SoundTrigger sTrigger = new SoundTrigger();
			sTrigger.Id = _availiableTriggers[index].Id;
			sTrigger.Name = _availiableTriggers[index].Name;

			return sTrigger;
		}

		private string[] GetEmitterOptions(VolumeEmitter[] volEmitters)
		{
			string[] options;

			if (volEmitters == null)
			{
				options = new string[] {_volEmitterName}; //'fixed' by default
			}
			else 
			{

				int index = volEmitters.Length;
				options = new string[index];

				for (int i = 0; i < index; i++)
				{
					options[i] = volEmitters[i].Name;
				}

			}
			

			return options;
		}
		#endregion

	}
}
