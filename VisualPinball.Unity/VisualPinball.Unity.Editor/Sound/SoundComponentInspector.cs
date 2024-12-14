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
using System;
using System.Linq;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SoundComponent)), CanEditMultipleObjects]
	public class SoundComponentInspector : UnityEditor.Editor
	{
		private SerializedProperty _soundAssetProp;
		private SerializedProperty _triggerIdProp;
		private SerializedProperty _hasStopTriggerProp;
		private SerializedProperty _stopTriggerIdProp;
		private SerializedProperty _volumeProp;

		private void OnEnable()
		{
			_soundAssetProp = serializedObject.FindProperty("_soundAsset");
			_triggerIdProp = serializedObject.FindProperty("_triggerId");
			_hasStopTriggerProp = serializedObject.FindProperty("_hasStopTrigger");
			_stopTriggerIdProp = serializedObject.FindProperty("_stopTriggerId");
			_volumeProp = serializedObject.FindProperty("_volume");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			if (TryGetEmitterComponent(out var emitter)) {
				if (emitter.AvailableTriggers?.Length > 0) {
					if (_soundAssetProp.objectReferenceValue is SoundAsset &&
						(_soundAssetProp.objectReferenceValue as SoundAsset).Loop &&
						!_hasStopTriggerProp.boolValue) {
						EditorGUILayout.HelpBox("The selected sound asset loops and no stop trigger is set, so the sound will loop forever once started.", MessageType.Warning);
					}
					EditorGUILayout.PropertyField(_soundAssetProp);
					var triggers = GetAvailableTriggers();
					TriggerDropdown("Trigger", _triggerIdProp, triggers);
					EditorGUILayout.PropertyField(_hasStopTriggerProp);
					if (_hasStopTriggerProp.boolValue)
						TriggerDropdown("Stop Trigger", _stopTriggerIdProp, triggers);
					EditorGUILayout.PropertyField(_volumeProp);
				}
				else {
					EditorGUILayout.HelpBox("The emitter component does not specify any sound triggers.", MessageType.Info);
				}
			}
			else {
				EditorGUILayout.HelpBox("Cannot find sound emitter. This component only works with a sound emitter on the same GameObject.", MessageType.Warning);
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void TriggerDropdown(string label, SerializedProperty prop, SoundTrigger[] triggers)
		{
			var triggerIndex = triggers.ToList().FindIndex(t => t.Id == prop.stringValue);
			if (triggerIndex == -1)
				triggerIndex = 0;
			triggerIndex = EditorGUILayout.Popup(label, triggerIndex, triggers.Select(t => t.Name).ToArray());
			prop.stringValue = triggers[triggerIndex].Id;
		}

		private bool TryGetEmitterComponent(out ISoundEmitter emitter)
		{
			emitter = null;
			return target is Component && (target as Component).TryGetComponent<ISoundEmitter>(out emitter);
		}


		private SoundTrigger[] GetAvailableTriggers()
		{
			if (target is Component && (target as Component).TryGetComponent<ISoundEmitter>(out var emitter))
				return emitter.AvailableTriggers;
			return Array.Empty<SoundTrigger>();
		}
	}
}
