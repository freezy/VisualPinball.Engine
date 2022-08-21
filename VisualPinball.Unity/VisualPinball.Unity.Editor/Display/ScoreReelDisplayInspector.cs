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

		private SerializedProperty _stepsPerTurnProperty;
		private SerializedProperty _timePerStepProperty;
		private SerializedProperty _delayAfterTurnProperty;

		private SerializedProperty _scoreMotorActionsListProperty;

		private List<ReorderableList> scoreMotorActionsLists = new List<ReorderableList>();
		private bool _toggleScoreMotor = true;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		private void OnEnable()
		{
			base.OnEnable();

			_idProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent._id));
			_speedProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.Speed));
			_waitProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.Wait));
			_reelObjectsProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.ReelObjects));

			_stepsPerTurnProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.StepsPerTurn));
			_timePerStepProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.TimePerStep));
			_delayAfterTurnProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.DelayAfterTurn));

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
				PropertyField(_stepsPerTurnProperty);

				UpdateScoreMotorActionsList();

				if (_stepsPerTurnProperty.intValue > 0) {
					PropertyField(_timePerStepProperty);
					PropertyField(_delayAfterTurnProperty);

					for (var index = 1; index < _stepsPerTurnProperty.intValue - 1; index++) {
						EditorGUILayout.LabelField($"Increase By {index + 1}");

						scoreMotorActionsLists[index].DoLayoutList();
					}
				}
			}

			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();

			EndEditing();
		}

		private void UpdateScoreMotorActionsList()
		{
			// Steps Per Turn Decreased

			if (_scoreMotorActionsListProperty.arraySize > _stepsPerTurnProperty.intValue) {
				while (_scoreMotorActionsListProperty.arraySize > _stepsPerTurnProperty.intValue) { 
					_scoreMotorActionsListProperty.DeleteArrayElementAtIndex(_scoreMotorActionsListProperty.arraySize - 1);
					scoreMotorActionsLists.RemoveAt(scoreMotorActionsLists.Count - 1);
				}

				RecalcuteScoreMotorActions();
			}

			// Steps Per Turn Increased

			if (_scoreMotorActionsListProperty.arraySize < _stepsPerTurnProperty.intValue) {
				while (_scoreMotorActionsListProperty.arraySize < _stepsPerTurnProperty.intValue) {
					_scoreMotorActionsListProperty.InsertArrayElementAtIndex(_scoreMotorActionsListProperty.arraySize);

					var actionsProperty = _scoreMotorActionsListProperty.GetArrayElementAtIndex(_scoreMotorActionsListProperty.arraySize - 1).FindPropertyRelative(nameof(ScoreMotorActions.Actions));
					scoreMotorActionsLists.Add(GenerateReordableList(actionsProperty));
				}

				RecalcuteScoreMotorActions();
			}
		}

		private void RecalcuteScoreMotorActions()
		{
			for (var increaseBy = 0; increaseBy < _stepsPerTurnProperty.intValue; increaseBy++) {
				var actionsProperty = _scoreMotorActionsListProperty.GetArrayElementAtIndex(increaseBy).FindPropertyRelative(nameof(ScoreMotorActions.Actions));

				// Steps Per Turn Decreased

				while (actionsProperty.arraySize > _stepsPerTurnProperty.intValue) {
					actionsProperty.DeleteArrayElementAtIndex(actionsProperty.arraySize - 1);
				}

				// Steps Per Turn Increased

				while (actionsProperty.arraySize < _stepsPerTurnProperty.intValue) {
					actionsProperty.InsertArrayElementAtIndex(actionsProperty.arraySize);
				}

				for (var index = 0; index < actionsProperty.arraySize; index++) {
					actionsProperty.GetArrayElementAtIndex(index).intValue = index <= increaseBy ? (int)ScoreMotorAction.Increase : (int)ScoreMotorAction.Wait;
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
