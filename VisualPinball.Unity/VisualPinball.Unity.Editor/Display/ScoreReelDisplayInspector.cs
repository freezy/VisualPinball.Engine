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
		private SerializedProperty _scoreMotorProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_idProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent._id));
			_speedProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.Speed));
			_waitProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.Wait));
			_reelObjectsProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.ReelObjects));
			_scoreMotorProperty = serializedObject.FindProperty(nameof(ScoreReelDisplayComponent.ScoreMotorComponent));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			PropertyField(_idProperty, "ID");
			PropertyField(_speedProperty);
			PropertyField(_waitProperty);
			PropertyField(_reelObjectsProperty);
			PropertyField(_scoreMotorProperty, "Score Motor");

			base.OnInspectorGUI();

			EndEditing();
		}
	}	
}
