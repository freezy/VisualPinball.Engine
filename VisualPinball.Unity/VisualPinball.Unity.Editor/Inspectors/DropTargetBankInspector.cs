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

		private static readonly string[] TypeLabels = {
			"Single",
			"2 Bank",
			"3 Bank",
			"4 Bank",
			"5 Bank"
		};

		private static readonly int[] TypeValues = {
			1,
			2,
			3,
			4,
			5
		};

		private bool _togglePlayfield = true;

		private SerializedProperty _typeProperty;
		private SerializedProperty _dropTarget1Property;
		private SerializedProperty _dropTarget2Property;
		private SerializedProperty _dropTarget3Property;
		private SerializedProperty _dropTarget4Property;
		private SerializedProperty _dropTarget5Property;

		protected override MonoBehaviour UndoTarget => throw new System.NotImplementedException();

		override protected void OnEnable()
		{
			base.OnEnable();

			_typeProperty = serializedObject.FindProperty(nameof(DropTargetBankComponent.Type));
			_dropTarget1Property = serializedObject.FindProperty(nameof(DropTargetBankComponent._dropTarget1));
			_dropTarget2Property = serializedObject.FindProperty(nameof(DropTargetBankComponent._dropTarget2));
			_dropTarget3Property = serializedObject.FindProperty(nameof(DropTargetBankComponent._dropTarget3));
			_dropTarget4Property = serializedObject.FindProperty(nameof(DropTargetBankComponent._dropTarget4));
			_dropTarget5Property = serializedObject.FindProperty(nameof(DropTargetBankComponent._dropTarget5));
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DropDownProperty("Type", _typeProperty, TypeLabels, TypeValues);

			if (!Application.isPlaying)
			{
				if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Links"))
				{
					EditorGUI.indentLevel++;

					PropertyField(_dropTarget1Property, $"Drop Target 1");

					if (_typeProperty.intValue > 1)
					{
						PropertyField(_dropTarget2Property, $"Drop Target 2");
					}

					if (_typeProperty.intValue > 2)
					{
						PropertyField(_dropTarget3Property, $"Drop Target 3");
					}

					if (_typeProperty.intValue > 3)
					{
						PropertyField(_dropTarget4Property, $"Drop Target 4");
					}

					if (_typeProperty.intValue > 4)
					{
						PropertyField(_dropTarget5Property, $"Drop Target 5");
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
