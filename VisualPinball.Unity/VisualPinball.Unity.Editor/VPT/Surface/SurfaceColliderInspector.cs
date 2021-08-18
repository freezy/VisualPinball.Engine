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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SurfaceColliderAuthoring)), CanEditMultipleObjects]
	public class SurfaceColliderInspector : ItemColliderInspector<Surface, SurfaceData, SurfaceAuthoring, SurfaceColliderAuthoring>
	{
		private bool _foldoutMaterial = true;
		private bool _foldoutSlingshot;

		private SerializedProperty _hitEventProperty;
		private SerializedProperty _thresholdProperty;
		private SerializedProperty _isBottomSolidProperty;
		private SerializedProperty _slingshotForceProperty;
		private SerializedProperty _slingshotThresholdProperty;
		private SerializedProperty _physicsMaterialProperty;
		private SerializedProperty _overwritePhysicsProperty;
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _elasticityFalloffProperty;
		private SerializedProperty _frictionProperty;
		private SerializedProperty _scatterProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_hitEventProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.HitEvent));
			_thresholdProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.Threshold));
			_isBottomSolidProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.IsBottomSolid));
			_slingshotForceProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.SlingshotForce));
			_slingshotThresholdProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.SlingshotThreshold));
			_physicsMaterialProperty = serializedObject.FindProperty(nameof(ItemColliderAuthoring<Surface, SurfaceData, SurfaceAuthoring>.PhysicsMaterial));
			_overwritePhysicsProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.OverwritePhysics));
			_elasticityProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.Scatter));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_hitEventProperty, "Has Hit Event");
			PropertyField(_thresholdProperty, "Hit Threshold");
			PropertyField(_isBottomSolidProperty, "Is Bottom Collidable");

			// physics material
			if (_foldoutMaterial = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMaterial, "Physics Material")) {
				EditorGUI.BeginDisabledGroup(_overwritePhysicsProperty.boolValue);
				PropertyField(_physicsMaterialProperty, "Preset");
				EditorGUI.EndDisabledGroup();

				PropertyField(_overwritePhysicsProperty);

				EditorGUI.BeginDisabledGroup(!_overwritePhysicsProperty.boolValue);
				PropertyField(_elasticityProperty);
				PropertyField(_elasticityFalloffProperty);
				PropertyField(_frictionProperty);
				PropertyField(_scatterProperty, "Scatter Angle");
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			// slingshot props
			if (_foldoutSlingshot = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSlingshot, "Slingshot")) {
				PropertyField(_slingshotForceProperty);
				PropertyField(_slingshotThresholdProperty);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
