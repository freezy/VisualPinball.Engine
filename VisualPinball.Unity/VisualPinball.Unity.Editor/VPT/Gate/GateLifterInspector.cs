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
	[CustomEditor(typeof(GateLifterComponent)), CanEditMultipleObjects]
	public class GateLifterInspector : UnityEditor.Editor
	{
		private SerializedProperty _angleProperty;
		private SerializedProperty _speedProperty;

		private void OnEnable()
		{
			_angleProperty = serializedObject.FindProperty(nameof(GateLifterComponent.LiftedAngleDeg));
			_speedProperty = serializedObject.FindProperty(nameof(GateLifterComponent.AnimationSpeed));
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(_angleProperty, new GUIContent("Lifted Angle"));
			EditorGUILayout.PropertyField(_speedProperty);

			serializedObject.ApplyModifiedProperties();
		}
	}
}
