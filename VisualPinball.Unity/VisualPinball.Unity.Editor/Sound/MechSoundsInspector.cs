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

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(MechSoundsComponent)), CanEditMultipleObjects]
	public class MechanicalSoundInspector : UnityEditor.Editor
	{
		private SerializedProperty _audioMixerProperty;
		private SerializedProperty _soundsProperty;

		private void OnEnable()
		{
			_audioMixerProperty = serializedObject.FindProperty(nameof(MechSoundsComponent.AudioMixer));
			_soundsProperty = serializedObject.FindProperty(nameof(MechSoundsComponent.Sounds));
			
			var comp = target as MechSoundsComponent;
		}

		public override void OnInspectorGUI()
		{
			var comp = target as MechSoundsComponent;
			
			var soundEmitter = comp!.GetComponent<ISoundEmitter>();
			if (soundEmitter == null) {
				EditorGUILayout.HelpBox("Cannot find sound emitter. This component only works with a sound emitter on the same GameObject.", MessageType.Error);
				return;
			}

			serializedObject.Update();
			
			EditorGUILayout.PropertyField(_soundsProperty);

			EditorGUILayout.PropertyField(_audioMixerProperty);

			serializedObject.ApplyModifiedProperties();
		}
	}
}
