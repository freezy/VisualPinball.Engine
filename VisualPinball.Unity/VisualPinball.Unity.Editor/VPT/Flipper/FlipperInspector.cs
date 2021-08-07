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
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(FlipperAuthoring))]
	public class FlipperInspector : ItemMainInspector<Flipper, FlipperData, FlipperAuthoring>
	{
		private bool _foldoutBaseGeometry = true;
		private bool _foldoutRubberGeometry = true;

		private SerializedProperty _positionProperty;
		private SerializedProperty _startAngleProperty;
		private SerializedProperty _endAngleProperty;
		private SerializedProperty _surfaceProperty;
		private SerializedProperty _isEnabledProperty;
		private SerializedProperty _isDualWoundProperty;
		private SerializedProperty _heightProperty;
		private SerializedProperty _baseRadiusProperty;
		private SerializedProperty _endRadiusProperty;
		private SerializedProperty _flipperRadiusProperty;
		private SerializedProperty _rubberThicknessProperty;
		private SerializedProperty _rubberHeightProperty;
		private SerializedProperty _rubberWidthProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.Position));
			_startAngleProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.StartAngle));
			_endAngleProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.EndAngle));
			_surfaceProperty = serializedObject.FindProperty(nameof(FlipperAuthoring._surface));
			_isEnabledProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.IsEnabled));
			_isDualWoundProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.IsDualWound));
			_heightProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.Height));
			_baseRadiusProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.BaseRadius));
			_endRadiusProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.EndRadius));
			_flipperRadiusProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.FlipperRadius));
			_rubberThicknessProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.RubberThickness));
			_rubberHeightProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.RubberHeight));
			_rubberWidthProperty = serializedObject.FindProperty(nameof(FlipperAuthoring.RubberWidth));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_startAngleProperty, updateTransforms: true);
			PropertyField(_endAngleProperty);
			PropertyField(_surfaceProperty);
			PropertyField(_isEnabledProperty);
			PropertyField(_isDualWoundProperty);

			if (_foldoutBaseGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutBaseGeometry, "Base Geometry")) {
				PropertyField(_heightProperty, rebuildMesh: true);
				PropertyField(_baseRadiusProperty, rebuildMesh: true);
				PropertyField(_endRadiusProperty, rebuildMesh: true);
				PropertyField(_flipperRadiusProperty, "Flipper Length", true);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutRubberGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutRubberGeometry, "Rubber Geometry")) {
				PropertyField(_rubberThicknessProperty, rebuildMesh: true, onChanged: OnRubberSizeUpdated);
				PropertyField(_rubberHeightProperty, rebuildMesh: true);
				PropertyField(_rubberWidthProperty, rebuildMesh: true, onChanged: OnRubberSizeUpdated);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}


		private void OnRubberSizeUpdated()
		{
			var rubberMesh = ItemAuthoring.GetComponentInChildren<FlipperRubberMeshAuthoring>(true);
			rubberMesh.gameObject.SetActive(_rubberWidthProperty.floatValue > 0f && _rubberThicknessProperty.floatValue > 0f);
		}
	}
}
