// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
	[CustomEditor(typeof(ScoreReelDisplayComponent))]
	public class ScoreReelDisplayInspector : ItemInspector
	{
		private SerializedProperty _idProperty;
		private SerializedProperty _speedProperty;
		private SerializedProperty _waitProperty;
		private SerializedProperty _reelObjectsProperty;

		private SerializedProperty _stepsProperty;
		private SerializedProperty _degreesProperty;
		private SerializedProperty _durationProperty;
		private SerializedProperty _blockScoringProperty;

		private SerializedProperty _scoreMotorActionsListProperty;

		private List<ReorderableList> scoreMotorActionsLists = new List<ReorderableList>();

		private bool _toggleScoreMotor = true;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_idProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent._id));
			_speedProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.Speed));
			_waitProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.Wait));
			_reelObjectsProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.ReelObjects));

			_stepsProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.Steps));
			_degreesProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.Degrees));
			_durationProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.Duration));
			_blockScoringProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.BlockScoring));

			_scoreMotorActionsListProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.ScoreMotorActionsList));

			for (var index = 0; index < _scoreMotorActionsListProperty.arraySize; index++) {
				var actionsProperty = _scoreMotorActionsListProperty.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(ScoreMotorActions.Actions));
				scoreMotorActionsLists.Add(GenerateReordableList(actionsProperty));
			}
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			PropertyField(_idProperty, "ID");
			PropertyField(_speedProperty);
			PropertyField(_waitProperty);
			PropertyField(_reelObjectsProperty);

			if (_toggleScoreMotor = EditorGUILayout.BeginFoldoutHeaderGroup(_toggleScoreMotor, "Score Motor")) {
				PropertyField(_stepsProperty);

				RecalcuteScoreMotorActions();

				PropertyField(_degreesProperty);
				PropertyField(_durationProperty);
				PropertyField(_blockScoringProperty);

				EditorGUILayout.Space();
				EditorGUILayout.LabelField($"Reel timing by increase:");

				var size = ScoreReelDisplayComponent.MAX_INCREASE;
				if (size == _stepsProperty.intValue) {
					size -= 1;
				}

				for (var index = 1; index < size; index++) {
					if (_scoreMotorActionsListProperty.GetArrayElementAtIndex(index).isExpanded =
						EditorGUILayout.Foldout(_scoreMotorActionsListProperty.GetArrayElementAtIndex(index).isExpanded, $"Increase By {index + 1}")) {
						scoreMotorActionsLists[index].DoLayoutList();
					}
				}
			}

			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();

			EndEditing();
		}

		private void RecalcuteScoreMotorActions()
		{
			for (var increase = 0; increase < _scoreMotorActionsListProperty.arraySize; increase++) {
				var change = false;

				var actionsProperty = _scoreMotorActionsListProperty.GetArrayElementAtIndex(increase).FindPropertyRelative(nameof(ScoreMotorActions.Actions));

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
							(index <= increase && increase <= ScoreReelDisplayComponent.MAX_INCREASE) ? (int)ScoreMotorAction.Increase : (int)ScoreMotorAction.Wait;
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
