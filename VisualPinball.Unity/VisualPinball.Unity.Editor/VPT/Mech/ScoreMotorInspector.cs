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
	[CustomEditor(typeof(ScoreMotorComponent))]
	public class ScoreMotorInspector : ItemInspector
	{
		private SerializedProperty _degreesProperty;
		private SerializedProperty _durationProperty;
		private SerializedProperty _switchesProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_degreesProperty = serializedObject.FindProperty(nameof(ScoreMotorComponent.Degrees));
			_durationProperty = serializedObject.FindProperty(nameof(ScoreMotorComponent.Duration));
			_switchesProperty = serializedObject.FindProperty(nameof(ScoreMotorComponent.Switches));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();


			PropertyField(_degreesProperty);
			PropertyField(_durationProperty);
			PropertyField(_switchesProperty, "Switches");

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
