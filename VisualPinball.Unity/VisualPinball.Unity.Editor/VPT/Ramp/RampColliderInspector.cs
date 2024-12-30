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
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RampColliderComponent)), CanEditMultipleObjects]
	public class RampColliderInspector : ColliderInspector<RampData, RampComponent, RampColliderComponent>
	{
		private bool _foldoutMaterial = true;

		private SerializedProperty _hitEventProperty;
		private SerializedProperty _thresholdProperty;
		private SerializedProperty _leftWallHeightProperty;
		private SerializedProperty _rightWallHeightProperty;

		private SerializedProperty _physicsMaterialProperty;
		private SerializedProperty _overwritePhysicsProperty;
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _frictionProperty;
		private SerializedProperty _scatterProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_hitEventProperty = serializedObject.FindProperty(nameof(RampColliderComponent.HitEvent));
			_thresholdProperty = serializedObject.FindProperty(nameof(RampColliderComponent.Threshold));
			_leftWallHeightProperty = serializedObject.FindProperty(nameof(RampColliderComponent.LeftWallHeight));
			_rightWallHeightProperty = serializedObject.FindProperty(nameof(RampColliderComponent.RightWallHeight));

			_physicsMaterialProperty = serializedObject.FindProperty(nameof(ColliderComponent<RampData, RampComponent>.PhysicsMaterial));
			_overwritePhysicsProperty = serializedObject.FindProperty(nameof(RampColliderComponent.OverwritePhysics));
			_elasticityProperty = serializedObject.FindProperty(nameof(RampColliderComponent.Elasticity));
			_frictionProperty = serializedObject.FindProperty(nameof(RampColliderComponent.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(RampColliderComponent.Scatter));
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
			PropertyField(_leftWallHeightProperty, "Left Colliding Wall Height", updateColliders: true);
			PropertyField(_rightWallHeightProperty, "Right Colliding Wall Height", updateColliders: true);

			// physics material
			if (_foldoutMaterial = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMaterial, "Physics Material")) {
				EditorGUI.BeginDisabledGroup(_overwritePhysicsProperty.boolValue);
				PropertyField(_physicsMaterialProperty, "Preset");
				EditorGUI.EndDisabledGroup();

				PropertyField(_overwritePhysicsProperty);

				EditorGUI.BeginDisabledGroup(!_overwritePhysicsProperty.boolValue);
				PropertyField(_elasticityProperty);
				PropertyField(_frictionProperty);
				PropertyField(_scatterProperty, "Scatter Angle");
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
