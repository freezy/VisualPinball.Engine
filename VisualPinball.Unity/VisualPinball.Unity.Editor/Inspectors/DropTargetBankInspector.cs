// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
	[CustomEditor(typeof(DropTargetBankComponent)), CanEditMultipleObjects]
	public class DropTargetBankInspector : ItemInspector
	{
		private bool _togglePlayfield = true;

		private SerializedProperty _bankSizeProperty;
		private SerializedProperty _dropTargetsProperty;

		protected override MonoBehaviour UndoTarget => throw new System.NotImplementedException();

		override protected void OnEnable()
		{
			base.OnEnable();

			_bankSizeProperty = serializedObject.FindProperty(nameof(DropTargetBankComponent.BankSize));
			_dropTargetsProperty = serializedObject.FindProperty(nameof(DropTargetBankComponent.DropTargets));
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			PropertyField(_bankSizeProperty);
			
			if (!Application.isPlaying)
			{
				if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Links"))
				{
					EditorGUI.indentLevel++;
					for (int index = 0; index < _bankSizeProperty.intValue; index++)
					{
						PropertyField(_dropTargetsProperty.GetArrayElementAtIndex(index), $"Drop Target {index + 1}");
					}
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();
			}

			serializedObject.ApplyModifiedProperties();

			if (Application.isPlaying) {
				EditorGUILayout.Separator();

				GUILayout.BeginHorizontal();
				GUILayout.BeginVertical();

				GUILayout.EndVertical();
				GUILayout.BeginVertical();
			}
		}

		private static void DrawCoil(string label, DeviceCoil coil)
		{
			var labelPos = EditorGUILayout.GetControlRect();
			labelPos.height = 18;
			var switchPos = new Rect((float) (labelPos.x + (double) EditorGUIUtility.labelWidth - 20.0), labelPos.y, labelPos.height, labelPos.height);
			GUI.Label(labelPos, label);
			GUI.DrawTexture(switchPos, Icons.Bolt(IconSize.Small, coil.IsEnabled ? IconColor.Orange : IconColor.Gray));
		}

		protected override void FinishEdit(string label, bool dirtyMesh = true)
		{
			base.FinishEdit(label, dirtyMesh);
		}
	}
}
