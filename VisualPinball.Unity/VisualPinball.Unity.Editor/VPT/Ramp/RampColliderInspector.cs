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
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RampColliderAuthoring))]
	public class RampColliderInspector : ItemColliderInspector<Ramp, RampData, RampAuthoring, RampColliderAuthoring>
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

			_hitEventProperty = serializedObject.FindProperty(nameof(SurfaceColliderAuthoring.HitEvent));
			_thresholdProperty = serializedObject.FindProperty(nameof(RampColliderAuthoring.Threshold));
			_leftWallHeightProperty = serializedObject.FindProperty(nameof(RampColliderAuthoring.LeftWallHeight));
			_rightWallHeightProperty = serializedObject.FindProperty(nameof(RampColliderAuthoring.RightWallHeight));

			_physicsMaterialProperty = serializedObject.FindProperty(nameof(ItemColliderAuthoring<Ramp, RampData, RampAuthoring>.PhysicsMaterial));
			_overwritePhysicsProperty = serializedObject.FindProperty(nameof(RampColliderAuthoring.OverwritePhysics));
			_elasticityProperty = serializedObject.FindProperty(nameof(RampColliderAuthoring.Elasticity));
			_frictionProperty = serializedObject.FindProperty(nameof(RampColliderAuthoring.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(RampColliderAuthoring.Scatter));
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
			PropertyField(_leftWallHeightProperty, "Left Colliding Wall Height");
			PropertyField(_rightWallHeightProperty, "Right Colliding Wall Height");

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

			serializedObject.ApplyModifiedProperties();
		}
	}
}
