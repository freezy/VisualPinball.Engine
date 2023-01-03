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

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(ScoreMotorComponent))]
	public class ScoreMotorInspector : ItemInspector
	{
		private SerializedProperty _stepsProperty;
		private SerializedProperty _degreesProperty;
		private SerializedProperty _durationProperty;
		private SerializedProperty _blockScoringProperty;
		private SerializedProperty _scoreMotorTimingListProperty;

		private List<ReorderableList> scoreMotorTimingReorderableList = new List<ReorderableList>();

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_stepsProperty = serializedObject.FindProperty(nameof(ScoreMotorComponent.Steps));
			_durationProperty = serializedObject.FindProperty(nameof(ScoreMotorComponent.Duration));
			_blockScoringProperty = serializedObject.FindProperty(nameof(ScoreMotorComponent.BlockScoring));
			_scoreMotorTimingListProperty = serializedObject.FindProperty(nameof(ScoreMotorComponent.ScoreMotorTimingList));

			for (var index = 0; index < _scoreMotorTimingListProperty.arraySize; index++) {
				var actionsProperty = _scoreMotorTimingListProperty.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(ScoreMotorTiming.Actions));
				scoreMotorTimingReorderableList.Add(GenerateReordableList(actionsProperty));
			}
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			PropertyField(_stepsProperty);

			RecalcuteScoreMotorTimingActions();

			PropertyField(_durationProperty);
			PropertyField(_blockScoringProperty);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"Reel timing by increase:");

			var size = ScoreMotorComponent.MaxIncrease;
			if (size == _stepsProperty.intValue) {
				size -= 1;
			}

			for (var index = 1; index < size; index++) {
				if (_scoreMotorTimingListProperty.GetArrayElementAtIndex(index).isExpanded =
					EditorGUILayout.Foldout(_scoreMotorTimingListProperty.GetArrayElementAtIndex(index).isExpanded, $"Increase By {index + 1}")) {
					scoreMotorTimingReorderableList[index].DoLayoutList();
				}
			}

			base.OnInspectorGUI();

			EndEditing();
		}

		private void RecalcuteScoreMotorTimingActions()
		{
			for (var increase = 0; increase < _scoreMotorTimingListProperty.arraySize; increase++) {
				var change = false;

				var actionsProperty = _scoreMotorTimingListProperty.GetArrayElementAtIndex(increase).FindPropertyRelative(nameof(ScoreMotorTiming.Actions));

				// Steps Decreased

				while (actionsProperty.arraySize > _stepsProperty.intValue) {
					actionsProperty.DeleteArrayElementAtIndex(actionsProperty.arraySize - 1);

					change = true;
				}

				// Steps Increased

				while (actionsProperty.arraySize < _stepsProperty.intValue) {
					actionsProperty.InsertArrayElementAtIndex(actionsProperty.arraySize);

					change = true;
				}

				if (change) {
					for (var index = 0; index < actionsProperty.arraySize; index++) {
						actionsProperty.GetArrayElementAtIndex(actionsProperty.arraySize - (index + 1)).intValue =
							(index <= increase && increase <= ScoreMotorComponent.MaxIncrease) ? (int)ScoreMotorAction.Increase : (int)ScoreMotorAction.Wait;
					}
				}
			}
		}

		private ReorderableList GenerateReordableList(SerializedProperty property)
		{
			var list = new ReorderableList(property.serializedObject, property, true, false, false, false);
			list.footerHeight = 0;
			list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => DrawReordableListItem(list.serializedProperty.GetArrayElementAtIndex(index), rect);

			return list;
		}

		private void DrawReordableListItem(SerializedProperty property, Rect rect)
		{
			EditorGUI.LabelField(new Rect(rect.x, rect.y, 100, EditorGUIUtility.singleLineHeight), property.enumDisplayNames[property.enumValueIndex]);
		}
	}	
}
