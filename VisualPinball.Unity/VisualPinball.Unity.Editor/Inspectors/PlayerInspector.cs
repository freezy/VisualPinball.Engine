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

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(Player))]
	[CanEditMultipleObjects]
	public class PlayerInspector : UnityEditor.Editor
	{
		private bool _toggleDebug = true;

		private SerializedProperty _updateDuringGameplayProperty;

		private void OnEnable()
		{
			_updateDuringGameplayProperty = serializedObject.FindProperty(nameof(Player.UpdateDuringGamplay));
		}

		public override void OnInspectorGUI()
		{
			var player = (Player) target;
			if (player == null) {
				return;
			}

			if (_toggleDebug = EditorGUILayout.BeginFoldoutHeaderGroup(_toggleDebug, "Debug"))
			{
				EditorGUI.indentLevel++;

				EditorGUI.BeginChangeCheck();

				EditorGUILayout.PropertyField(_updateDuringGameplayProperty, new GUIContent("Update During Gameplay"));

				if (EditorGUI.EndChangeCheck())
				{
					serializedObject.ApplyModifiedProperties();
				}

				EditorGUI.indentLevel--;
			}

			EditorGUILayout.EndFoldoutHeaderGroup();
		}
	}
}
