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
using VisualPinball.Engine.VPT.DropTargetBank;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(DropTargetBankComponent)), CanEditMultipleObjects]
	public class DropTargetBankInspector : MainInspector<DropTargetBankData, DropTargetBankComponent>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static readonly string[] BankSizeLabels = {
			"Single",
			"2 Bank",
			"3 Bank",
			"4 Bank",
			"5 Bank"
		};

		private static readonly int[] BankSizeValues = {
			1,
			2,
			3,
			4,
			5
		};

		private bool _togglePlayfield = true;

		private SerializedProperty _bankSizeProperty;
		private SerializedProperty _dropTargetsProperty;

		override protected void OnEnable()
		{
			base.OnEnable();

			_bankSizeProperty = serializedObject.FindProperty(nameof(DropTargetBankComponent.BankSize));
			_dropTargetsProperty = serializedObject.FindProperty(nameof(DropTargetBankComponent.DropTargets));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();

			DropDownProperty("Banks", _bankSizeProperty, BankSizeLabels, BankSizeValues);

			while (_dropTargetsProperty.arraySize < _bankSizeProperty.intValue)
			{
				_dropTargetsProperty.InsertArrayElementAtIndex(_dropTargetsProperty.arraySize);
			}

			if (!Application.isPlaying)
			{
				if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Links"))
				{
					EditorGUI.indentLevel++;

					for (var index = 0; index < _bankSizeProperty.intValue; index++)
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
				DropTargetBankApi dropTargetBankApi = tableApi.DropTargetBank(target.name);

			   GUILayout.BeginVertical();

				if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Links"))
				{
					EditorGUI.indentLevel++;

					for (var index = 0; index < _bankSizeProperty.intValue; index++)
					{
						GUILayout.BeginHorizontal();

						var dropTargetComponent = (DropTargetComponent)_dropTargetsProperty.GetArrayElementAtIndex(index).objectReferenceValue;
						var dropTargetApi = tableApi.DropTarget(dropTargetComponent);

						DrawSwitch($"Drop Target {index + 1}", dropTargetApi.IsDropped);

						if (GUILayout.Button(dropTargetApi.IsDropped ? "Reset" : "Drop"))
						{
							dropTargetApi.IsDropped = !dropTargetApi.IsDropped;
						}

						
						GUILayout.EndHorizontal();
					}
			
					EditorGUI.indentLevel--;
				}

				EditorGUILayout.Separator();

				GUILayout.BeginHorizontal();

				if (GUILayout.Button("Drop All"))
				{
					for (var index = 0; index < _bankSizeProperty.intValue; index++)
					{
						tableApi.DropTarget((DropTargetComponent)_dropTargetsProperty.GetArrayElementAtIndex(index).objectReferenceValue).IsDropped = true;
					}
				}

				if (GUILayout.Button("Reset All"))
				{
					for (var index = 0; index < _bankSizeProperty.intValue; index++)
					{
						tableApi.DropTarget((DropTargetComponent)_dropTargetsProperty.GetArrayElementAtIndex(index).objectReferenceValue).IsDropped = false;
					}
				}

				GUILayout.EndHorizontal();

				EditorGUILayout.Separator();

				DrawCoil("Reset Coil", dropTargetBankApi.ResetCoil);

				GUILayout.EndVertical();
			}
		}

		private static void DrawSwitch(string label, bool sw)
		{
			var labelPos = EditorGUILayout.GetControlRect();
			labelPos.height = 18;
			var switchPos = new Rect((float)(labelPos.x + (double)EditorGUIUtility.labelWidth + 2.0), labelPos.y, labelPos.height, labelPos.height);
			GUI.Label(labelPos, label);
			GUI.DrawTexture(switchPos, Icons.Switch(sw, IconSize.Small, sw ? IconColor.Orange : IconColor.Gray));
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
