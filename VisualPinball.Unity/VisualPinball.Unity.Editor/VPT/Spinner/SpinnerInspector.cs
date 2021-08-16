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
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpinnerAuthoring))]
	public class SpinnerInspector : ItemMainInspector<Spinner, SpinnerData, SpinnerAuthoring>
	{
		private bool _foldoutPhysics = true;

		private SerializedProperty _positionProperty;
		private SerializedProperty _heightProperty;
		private SerializedProperty _rotationProperty;
		private SerializedProperty _lengthProperty;
		private SerializedProperty _dampingProperty;
		private SerializedProperty _angleMaxProperty;
		private SerializedProperty _angleMinProperty;
		private SerializedProperty _surfaceProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(SpinnerAuthoring.Position));
			_heightProperty = serializedObject.FindProperty(nameof(SpinnerAuthoring.Height));
			_rotationProperty = serializedObject.FindProperty(nameof(SpinnerAuthoring.Rotation));
			_lengthProperty = serializedObject.FindProperty(nameof(SpinnerAuthoring.Length));
			_surfaceProperty = serializedObject.FindProperty(nameof(SpinnerAuthoring._surface));
			_dampingProperty = serializedObject.FindProperty(nameof(SpinnerAuthoring.Damping));
			_angleMaxProperty = serializedObject.FindProperty(nameof(SpinnerAuthoring.AngleMax));
			_angleMinProperty = serializedObject.FindProperty(nameof(SpinnerAuthoring.AngleMin));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_lengthProperty, updateTransforms: true);
			PropertyField(_rotationProperty, updateTransforms: true);
			PropertyField(_heightProperty, updateTransforms: true);
			PropertyField(_surfaceProperty, updateTransforms: true);

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				PropertyField(_dampingProperty);
				PropertyField(_angleMinProperty);
				PropertyField(_angleMaxProperty);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
