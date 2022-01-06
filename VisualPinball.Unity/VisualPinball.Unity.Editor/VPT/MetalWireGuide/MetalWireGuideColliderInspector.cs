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
using VisualPinball.Engine.VPT.MetalWireGuide;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(MetalWireGuideColliderComponent)), CanEditMultipleObjects]
	public class MetalWireGuideColliderInspector : ColliderInspector<MetalWireGuideData, MetalWireGuideComponent, MetalWireGuideColliderComponent>
	{
		private bool _foldoutMaterial = true;
		private SerializedProperty _hitEventProperty;
		private SerializedProperty _hitHeightProperty;
		private SerializedProperty _overwritePhysicsProperty;
		private SerializedProperty _physicsMaterialProperty;
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _elasticityFalloffProperty;
		private SerializedProperty _frictionProperty;
		private SerializedProperty _scatterProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_hitEventProperty = serializedObject.FindProperty(nameof(MetalWireGuideColliderComponent.HitEvent));
			_hitHeightProperty = serializedObject.FindProperty(nameof(MetalWireGuideColliderComponent.HitHeight));

			_overwritePhysicsProperty = serializedObject.FindProperty(nameof(MetalWireGuideColliderComponent.OverwritePhysics));
			_physicsMaterialProperty = serializedObject.FindProperty(nameof(ColliderComponent<MetalWireGuideData, MetalWireGuideComponent>.PhysicsMaterial));
			_elasticityProperty = serializedObject.FindProperty(nameof(MetalWireGuideColliderComponent.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(MetalWireGuideColliderComponent.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(MetalWireGuideColliderComponent.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(MetalWireGuideColliderComponent.Scatter));
		}

		public override void OnInspectorGUI()
		{

			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_hitEventProperty, "Has Hit Event");
			PropertyField(_hitHeightProperty, "Hit Height", updateColliders: true);

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
