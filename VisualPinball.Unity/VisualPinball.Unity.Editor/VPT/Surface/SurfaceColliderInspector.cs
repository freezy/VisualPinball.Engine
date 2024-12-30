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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SurfaceColliderComponent)), CanEditMultipleObjects]
	public class SurfaceColliderInspector : ColliderInspector<SurfaceData, SurfaceComponent, SurfaceColliderComponent>
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

			_hitEventProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.HitEvent));
			_thresholdProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.Threshold));
			_isBottomSolidProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.IsBottomSolid));
			_slingshotForceProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.SlingshotForce));
			_slingshotThresholdProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.SlingshotThreshold));
			_physicsMaterialProperty = serializedObject.FindProperty(nameof(ColliderComponent<SurfaceData, SurfaceComponent>.PhysicsMaterial));
			_overwritePhysicsProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.OverwritePhysics));
			_elasticityProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(SurfaceColliderComponent.Scatter));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_hitEventProperty, "Has Hit Event");
			PropertyField(_thresholdProperty, "Hit Threshold");
			PropertyField(_isBottomSolidProperty, "Is Bottom Collidable", updateColliders: true);

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

			EndEditing();
		}
	}
}
