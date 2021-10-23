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
using Logger = NLog.Logger;
using NLog;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(DropTargetBankComponent)), CanEditMultipleObjects]
	public class DropTargetBankInspector : ItemInspector
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
		private SerializedProperty _dropTargetsProperty;

		protected override MonoBehaviour UndoTarget => throw new System.NotImplementedException();

		override protected void OnEnable()
		{
			base.OnEnable();

			_typeProperty = serializedObject.FindProperty(nameof(DropTargetBankComponent.Type));
			_dropTargetsProperty = serializedObject.FindProperty(nameof(DropTargetBankComponent.DropTargets));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();

			DropDownProperty("Type", _typeProperty, TypeLabels, TypeValues);

			while (_dropTargetsProperty.arraySize < _typeProperty.intValue)
			{
				_dropTargetsProperty.InsertArrayElementAtIndex(_dropTargetsProperty.arraySize);
			}

			if (!Application.isPlaying)
			{
				if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Links"))
				{
					EditorGUI.indentLevel++;

					for (var index = 0; index < _typeProperty.intValue; index++)
					{
						PropertyField(_dropTargetsProperty.GetArrayElementAtIndex(index), $"Drop Target {index + 1}");
					}

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();
			}

			base.OnInspectorGUI();

			EndEditing();

			if (Application.isPlaying)
			{
				EditorGUILayout.Separator();

				TableApi tableApi = TableComponent.GetComponent<Player>().TableApi;

				GUILayout.BeginVertical();

				if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Links"))
				{
					EditorGUI.indentLevel++;

					for (var index = 0; index < _typeProperty.intValue; index++)
					{
						GUILayout.BeginHorizontal();

						GUILayout.Label($"Drop Target {index + 1}");

						var dropTargetApi = TableComponent.GetComponent<Player>().TableApi.DropTarget((DropTargetComponent)_dropTargetsProperty.GetArrayElementAtIndex(index).objectReferenceValue);

						if (GUILayout.Button(dropTargetApi.IsDropped ? "Reset" : "Drop"))
						{
							dropTargetApi.IsDropped = !dropTargetApi.IsDropped;
						}

						GUILayout.EndHorizontal();
					}

					EditorGUI.indentLevel--;
				}

				DrawCoil("Reset Coil", tableApi.DropTargetBank(target.name).ResetCoil);

				GUILayout.EndVertical();
			}
		}

		private static void DrawCoil(string label, DeviceCoil coil)
		{
			var labelPos = EditorGUILayout.GetControlRect();
			labelPos.height = 18;
			var switchPos = new Rect((float)(labelPos.x + (double)EditorGUIUtility.labelWidth - 20.0), labelPos.y, labelPos.height, labelPos.height);
			GUI.Label(labelPos, label);
			GUI.DrawTexture(switchPos, Icons.Bolt(IconSize.Small, coil.IsEnabled ? IconColor.Orange : IconColor.Gray));
		}

		protected override void FinishEdit(string label, bool dirtyMesh = true)
		{
			base.FinishEdit(label, dirtyMesh);
		}
	}
}
