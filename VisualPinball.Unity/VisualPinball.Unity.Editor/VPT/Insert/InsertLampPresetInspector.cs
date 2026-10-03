// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The lamp preset's inspector: its values, and the way to hand an edit on
	/// to the insert lights of the open scenes that follow it.
	/// </summary>
	[CustomEditor(typeof(InsertLampPreset))]
	public class InsertLampPresetInspector : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			var preset = (InsertLampPreset)target;
			if (string.IsNullOrWhiteSpace(preset.LampName)) {
				EditorGUILayout.HelpBox("Give the lamp a name to list it in the Insert Light's Lamp dropdown.", MessageType.Info);
				return;
			}
			var followers = Object.FindObjectsByType<InsertLightComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
				.Where(light => light.Lamp == preset.LampName)
				.ToArray();
			var stale = followers.Count(light => InsertLampPresets.Differs(light, preset));
			EditorGUILayout.Space();
			if (followers.Length == 0) {
				EditorGUILayout.HelpBox("No insert light in the open scenes follows this lamp.", MessageType.None);
				return;
			}
			EditorGUILayout.HelpBox(stale > 0
				? $"{followers.Length} insert lights follow this lamp; {stale} still have other values."
				: $"{followers.Length} insert lights follow this lamp and have its values.", MessageType.None);
			using (new EditorGUI.DisabledScope(stale == 0 || Application.isPlaying)) {
				if (GUILayout.Button(new GUIContent($"Apply to {followers.Length} Insert Lights", "Gives every insert light that follows this lamp its colour, output and fades again."))) {
					InsertLampPresets.Apply(preset, followers);
				}
			}
		}
	}
}
