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
	[CustomEditor(typeof(PhysicsEngine))]
	public class PhysicsEngineInspector : UnityEditor.Editor
	{
		private SerializedProperty _gravityProperty;
		private SerializedProperty _keyboardNudgeModeProperty;
		private SerializedProperty _keyboardNudgeStrengthProperty;
		private SerializedProperty _keyboardCabinetDampingProperty;
		private SerializedProperty _visualNudgeStrengthProperty;

		private void OnEnable()
		{
			_gravityProperty = serializedObject.FindProperty(nameof(PhysicsEngine.GravityStrength));
			_keyboardNudgeModeProperty = serializedObject.FindProperty(nameof(PhysicsEngine.KeyboardNudgeMode));
			_keyboardNudgeStrengthProperty = serializedObject.FindProperty(nameof(PhysicsEngine.KeyboardNudgeStrength));
			_keyboardCabinetDampingProperty = serializedObject.FindProperty(nameof(PhysicsEngine.KeyboardCabinetDamping));
			_visualNudgeStrengthProperty = serializedObject.FindProperty(nameof(PhysicsEngine.VisualNudgeStrength));
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(_gravityProperty, new GUIContent("Gravity Constant"));
			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Keyboard Nudge", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_keyboardNudgeModeProperty, new GUIContent("Mode"));
			EditorGUILayout.PropertyField(_keyboardNudgeStrengthProperty, new GUIContent("Strength"));
			EditorGUILayout.PropertyField(_keyboardCabinetDampingProperty, new GUIContent("Cabinet Damping"));
			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Visual Nudge", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_visualNudgeStrengthProperty, new GUIContent("Strength"));

			if (EditorGUI.EndChangeCheck()) {
				serializedObject.ApplyModifiedProperties();
				foreach (var obj in targets) {
					if (obj is PhysicsEngine physicsEngine) {
						physicsEngine.ConfigureKeyboardNudge(physicsEngine.KeyboardNudgeMode,
							physicsEngine.KeyboardNudgeStrength, physicsEngine.KeyboardCabinetDamping);
						physicsEngine.ConfigureVisualNudge(physicsEngine.VisualNudgeStrength);
					}
				}
			}
		}
	}
}
