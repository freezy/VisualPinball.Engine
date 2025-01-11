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
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PrimitiveColliderComponent)), CanEditMultipleObjects]
	public class PrimitiveColliderInspector : ColliderInspector<PrimitiveData, PrimitiveComponent, PrimitiveColliderComponent>
	{
		private bool _foldoutMaterial = true;

		private SerializedProperty _hitEventProperty;
		private SerializedProperty _thresholdProperty;
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _elasticityFalloffProperty;
		private SerializedProperty _frictionProperty;
		private SerializedProperty _scatterProperty;
		private SerializedProperty _collisionReductionFactorProperty;
		private SerializedProperty _overwritePhysicsProperty;
		private SerializedProperty _physicsMaterialProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_hitEventProperty = serializedObject.FindProperty(nameof(PrimitiveColliderComponent.HitEvent));
			_thresholdProperty = serializedObject.FindProperty(nameof(PrimitiveColliderComponent.Threshold));
			_collisionReductionFactorProperty = serializedObject.FindProperty(nameof(PrimitiveColliderComponent.CollisionReductionFactor));

			_physicsMaterialProperty = serializedObject.FindProperty(nameof(ColliderComponent<PrimitiveData, PrimitiveComponent>.PhysicsMaterial));
			_overwritePhysicsProperty = serializedObject.FindProperty(nameof(PrimitiveColliderComponent.OverwritePhysics));
			_elasticityProperty = serializedObject.FindProperty(nameof(PrimitiveColliderComponent.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(PrimitiveColliderComponent.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(PrimitiveColliderComponent.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(PrimitiveColliderComponent.Scatter));
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
			PropertyField(_collisionReductionFactorProperty, "Reduce Polygons By");

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

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
