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
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(BumperComponent)), CanEditMultipleObjects]
	public class BumperInspector : MainInspector<BumperData, BumperComponent>
	{
		private SerializedProperty _positionProperty;
		private SerializedProperty _radiusProperty;
		private SerializedProperty _heightScaleProperty;
		private SerializedProperty _orientationProperty;
		private SerializedProperty _surfaceProperty;
		private SerializedProperty _isHardwiredProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(BumperComponent.Position));
			_radiusProperty = serializedObject.FindProperty(nameof(BumperComponent.Radius));
			_heightScaleProperty = serializedObject.FindProperty(nameof(BumperComponent.HeightScale));
			_orientationProperty = serializedObject.FindProperty(nameof(BumperComponent.Orientation));
			_surfaceProperty = serializedObject.FindProperty(nameof(BumperComponent._surface));
			_isHardwiredProperty = serializedObject.FindProperty(nameof(BumperComponent.IsHardwired));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			// position
			EditorGUI.BeginChangeCheck();
			var newPos = EditorGUILayout.Vector2Field(new GUIContent("Position", "Position of the bumper on the playfield, relative to its parent."), MainComponent.Position);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Bumper Position");
				MainComponent.Position = newPos;
			}

			PropertyField(_radiusProperty, updateTransforms: true);
			PropertyField(_heightScaleProperty, updateTransforms: true);

			// orientation
			EditorGUI.BeginChangeCheck();
			var newAngle = EditorGUILayout.Slider(new GUIContent("Orientation", "Orientation angle. Updates z rotation"), MainComponent.Orientation, -180f, 180f);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Bumper Orientation");
				MainComponent.Orientation = newAngle;
			}

			PropertyField(_surfaceProperty, updateTransforms: true);
			PropertyField(_isHardwiredProperty, updateTransforms: false);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
