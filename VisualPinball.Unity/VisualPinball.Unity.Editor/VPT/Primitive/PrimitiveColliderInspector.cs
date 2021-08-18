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
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PrimitiveColliderAuthoring)), CanEditMultipleObjects]
	public class PrimitiveColliderInspector : ItemColliderInspector<Primitive, PrimitiveData, PrimitiveAuthoring, PrimitiveColliderAuthoring>
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

			_hitEventProperty = serializedObject.FindProperty(nameof(PrimitiveColliderAuthoring.HitEvent));
			_thresholdProperty = serializedObject.FindProperty(nameof(PrimitiveColliderAuthoring.Threshold));
			_collisionReductionFactorProperty = serializedObject.FindProperty(nameof(PrimitiveColliderAuthoring.CollisionReductionFactor));

			_physicsMaterialProperty = serializedObject.FindProperty(nameof(ItemColliderAuthoring<Primitive, PrimitiveData, PrimitiveAuthoring>.PhysicsMaterial));
			_overwritePhysicsProperty = serializedObject.FindProperty(nameof(PrimitiveColliderAuthoring.OverwritePhysics));
			_elasticityProperty = serializedObject.FindProperty(nameof(PrimitiveColliderAuthoring.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(PrimitiveColliderAuthoring.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(PrimitiveColliderAuthoring.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(PrimitiveColliderAuthoring.Scatter));
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

			serializedObject.ApplyModifiedProperties();
		}
	}
}
