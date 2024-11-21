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
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpinnerComponent)), CanEditMultipleObjects]
	public class SpinnerInspector : MainInspector<SpinnerData, SpinnerComponent>
	{
		private bool _foldoutPhysics = true;

		private SerializedProperty _lengthProperty;
		private SerializedProperty _dampingProperty;
		private SerializedProperty _angleMaxProperty;
		private SerializedProperty _angleMinProperty;
		private SerializedProperty _surfaceProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_lengthProperty = serializedObject.FindProperty(nameof(SpinnerComponent.Length));
			_surfaceProperty = serializedObject.FindProperty(nameof(SpinnerComponent._surface));
			_dampingProperty = serializedObject.FindProperty(nameof(SpinnerComponent.Damping));
			_angleMaxProperty = serializedObject.FindProperty(nameof(SpinnerComponent.AngleMax));
			_angleMinProperty = serializedObject.FindProperty(nameof(SpinnerComponent.AngleMin));
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
			var newPos = EditorGUILayout.Vector2Field(new GUIContent("Position", "Position of the spinner on the playfield, relative to its parent."), MainComponent.Position);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Spinner Position");
				MainComponent.Position = newPos;
			}

			EditorGUI.BeginChangeCheck();
			var newHeight = EditorGUILayout.FloatField(new GUIContent("Height", "Z-Position on the playfield, relative to its parent."), MainComponent.Height);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Spinner Height");
				MainComponent.Height = newHeight;
			}

			PropertyField(_lengthProperty, updateTransforms: true);

			EditorGUI.BeginChangeCheck();
			var newRotation = EditorGUILayout.Slider(new GUIContent("Rotation", "Z-Axis rotation of the spinner on the playfield."), MainComponent.Rotation, -180f, 180f);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Spinner Rotation");
				MainComponent.Rotation = newRotation;
			}

			PropertyField(_surfaceProperty, updateTransforms: true);

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				PropertyField(_dampingProperty);
				PropertyField(_angleMinProperty);
				PropertyField(_angleMaxProperty);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
