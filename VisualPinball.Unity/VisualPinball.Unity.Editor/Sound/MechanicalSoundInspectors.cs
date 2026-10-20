// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public abstract class MechanicalSoundInspector : UnityEditor.Editor
	{
		protected void DrawChoice(string property, string[] choices) {
			var value = serializedObject.FindProperty(property);
			var index = Array.IndexOf(choices, value.stringValue);
			EditorGUI.showMixedValue = value.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var next = EditorGUILayout.Popup(ObjectNames.NicifyVariableName(property), index, choices);
			if (EditorGUI.EndChangeCheck() && next >= 0)
				value.stringValue = choices[next];
			EditorGUI.showMixedValue = false;
			if (index < 0)
				EditorGUILayout.HelpBox("Select a valid source. Current value: " + value.stringValue, MessageType.Warning);
		}

		protected void DrawSoundFields(params string[] except) {
			DrawPropertiesExcluding(serializedObject, new[] { "m_Script", "Priority", "CalloutMaxQueueTime" }.Concat(except).ToArray());
		}
	}

	[CustomEditor(typeof(PinballEventSoundComponent)), CanEditMultipleObjects]
	public class PinballEventSoundInspector : MechanicalSoundInspector
	{
		public override void OnInspectorGUI() {
			serializedObject.Update();
			var emitters = ((Component)target).GetComponents<IPinballEventEmitter>();
			DrawChoice("EventName", emitters.SelectMany(e => e.Events).Select(e => e.Name).Distinct().ToArray());
			var name = serializedObject.FindProperty("EventName").stringValue;
			if (emitters.Count(e => e.Events.Any(v => v.Name == name)) > 1)
				EditorGUILayout.HelpBox("Multiple emitters advertise this event; playback cannot bind unambiguously.", MessageType.Error);
			DrawSoundFields("EventName");
			serializedObject.ApplyModifiedProperties();
		}
	}

	[CustomEditor(typeof(InputActionSoundComponent)), CanEditMultipleObjects]
	public class InputActionSoundInspector : MechanicalSoundInspector
	{
		public override void OnInspectorGUI() {
			serializedObject.Update();
			DrawChoice("ActionName", new InputManager().GetActionNames(VisualPinball.Engine.Common.InputConstants.MapCabinetSwitches).Select(InputManager.GetCanonicalActionName).Distinct().ToArray());
			DrawSoundFields("ActionName");
			serializedObject.ApplyModifiedProperties();
		}
	}

	[CustomEditor(typeof(KickerReleaseSoundComponent)), CanEditMultipleObjects]
	public class KickerReleaseSoundInspector : MechanicalSoundInspector
	{
		public override void OnInspectorGUI() {
			serializedObject.Update();
			var kicker = ((Component)target).GetComponent<KickerComponent>();
			if (!kicker)
				EditorGUILayout.HelpBox("A KickerComponent is required on this object.", MessageType.Error);
			else
				DrawChoice("CoilName", new[] { "" }.Concat(kicker.Coils.Select(c => c.Id)).ToArray());
			DrawSoundFields("CoilName");
			serializedObject.ApplyModifiedProperties();
		}
	}

	[CustomEditor(typeof(FlipperSoundComponent)), CanEditMultipleObjects]
	public class FlipperSoundInspector : MechanicalSoundInspector
	{
		public override void OnInspectorGUI() {
			serializedObject.Update();
			if (!((Component)target).GetComponent<FlipperComponent>())
				EditorGUILayout.HelpBox("A FlipperComponent is required on this object.", MessageType.Error);
			DrawSoundFields("SoundAsset", "MultiPlayMode");
			foreach (var name in new[] { "Attack", "Up", "Reflip", "Down" }) {
				var asset = serializedObject.FindProperty(name).objectReferenceValue as SoundEffectAsset;
				if (asset && (!asset.IsValid() || asset.Loop))
					EditorGUILayout.HelpBox(name + " needs a finite sound bank with audio clips.", MessageType.Warning);
			}
			serializedObject.ApplyModifiedProperties();
		}
	}
}
