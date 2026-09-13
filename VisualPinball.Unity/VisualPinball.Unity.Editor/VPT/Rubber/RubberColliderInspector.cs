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
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberColliderComponent)), CanEditMultipleObjects]
	public class RubberColliderInspector : ColliderInspector<RubberData, RubberComponent, RubberColliderComponent>
	{
		private bool _foldoutMaterial = true;
		private SerializedProperty _hitEventProperty;
		private SerializedProperty _zOffset;
		private SerializedProperty _overwritePhysicsProperty;
		private SerializedProperty _physicsMaterialProperty;
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _elasticityFalloffProperty;
		private SerializedProperty _frictionProperty;
		private SerializedProperty _scatterProperty;
		private SerializedProperty _modeProperty;
		private SerializedProperty _rubberPhysicsMaterialProperty;
		private SerializedProperty _guideDistanceElasticityEnabledProperty;
		private SerializedProperty _freeSpanElasticityScaleProperty;
		private SerializedProperty _guideInfluenceLengthProperty;
		private SerializedProperty _maximumResponseSegmentLengthProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_hitEventProperty = serializedObject.FindProperty(nameof(RubberColliderComponent.HitEvent));
			_zOffset = serializedObject.FindProperty(nameof(RubberColliderComponent.ZOffset));

			_overwritePhysicsProperty = serializedObject.FindProperty(nameof(RubberColliderComponent.OverwritePhysics));
			_physicsMaterialProperty = serializedObject.FindProperty(nameof(ColliderComponent<RubberData, RubberComponent>.PhysicsMaterial));
			_elasticityProperty = serializedObject.FindProperty(nameof(RubberColliderComponent.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(RubberColliderComponent.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(RubberColliderComponent.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(RubberColliderComponent.Scatter));
			_modeProperty = serializedObject.FindProperty("_mode");
			_rubberPhysicsMaterialProperty = serializedObject.FindProperty("_rubberPhysicsMaterial");
			_guideDistanceElasticityEnabledProperty = serializedObject.FindProperty("_guideDistanceElasticityEnabled");
			_freeSpanElasticityScaleProperty = serializedObject.FindProperty("_freeSpanElasticityScale");
			_guideInfluenceLengthProperty = serializedObject.FindProperty("_guideInfluenceLength");
			_maximumResponseSegmentLengthProperty = serializedObject.FindProperty("_maximumResponseSegmentLength");
		}

		public override void OnInspectorGUI()
		{

			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();
			var currentMode = (RubberColliderMode)_modeProperty.enumValueIndex;
			EditorGUI.showMixedValue = _modeProperty.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var requestedMode = (RubberColliderMode)EditorGUILayout.EnumPopup("Collider Model", currentMode);
			if (EditorGUI.EndChangeCheck()) {
				if (requestedMode == RubberColliderMode.Physical && !AllTargetsCanUsePhysical()) {
					EditorUtility.DisplayDialog("Physical Rubber Unavailable",
						"Every selected rubber requires a current, valid guided bake.", "OK");
				} else {
					_modeProperty.enumValueIndex = (int)requestedMode;
				}
			}
			EditorGUI.showMixedValue = false;
			if (HasInvalidPhysicalTarget()) {
				EditorGUILayout.HelpBox("This Physical setting is preserved but cannot run until the guided path is valid and current.",
					MessageType.Error);
			}
			var showsPhysical = _modeProperty.hasMultipleDifferentValues
				|| (RubberColliderMode)_modeProperty.enumValueIndex == RubberColliderMode.Physical;
			if (showsPhysical) {
				EditorGUI.BeginDisabledGroup(true);
				PropertyField(_rubberPhysicsMaterialProperty, "Deformation Material (reserved)");
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.HelpBox("Physical currently uses static round-cord collision and the standard contact material below. The deformation material is reserved for experimental free-span deformation.",
					MessageType.Info);

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Guide-distance Elasticity", EditorStyles.boldLabel);
				PropertyField(_guideDistanceElasticityEnabledProperty, "Enabled", updateColliders: true);
				var enableResponseFields = _guideDistanceElasticityEnabledProperty.hasMultipleDifferentValues
					|| _guideDistanceElasticityEnabledProperty.boolValue;
				EditorGUI.BeginDisabledGroup(!enableResponseFields);
				PropertyField(_freeSpanElasticityScaleProperty, "Free-span Elasticity Scale",
					updateColliders: true);
				PropertyField(_guideInfluenceLengthProperty, "Guide Influence Length",
					updateColliders: true);
				PropertyField(_maximumResponseSegmentLengthProperty, "Maximum Response Segment Length",
					updateColliders: true);
				EditorGUI.EndDisabledGroup();
				if (enableResponseFields
					&& !_guideInfluenceLengthProperty.hasMultipleDifferentValues
					&& !_maximumResponseSegmentLengthProperty.hasMultipleDifferentValues) {
					var effectiveMaximumLength = ((RubberColliderComponent)target)
						.GuideDistanceElasticitySettings.EffectiveMaximumSegmentLength;
					EditorGUILayout.LabelField(new GUIContent("Effective Maximum Length",
						"The smaller of Maximum Response Segment Length and one eighth of Guide Influence Length."),
						new GUIContent($"{effectiveMaximumLength:0.###} VPX"));
				}
				if (!_guideDistanceElasticityEnabledProperty.hasMultipleDifferentValues
					&& _guideDistanceElasticityEnabledProperty.boolValue
					&& !_freeSpanElasticityScaleProperty.hasMultipleDifferentValues
					&& _freeSpanElasticityScaleProperty.floatValue == 1f) {
					EditorGUILayout.HelpBox("The response is inactive while Free-span Elasticity Scale is 1; current collider count and contact behavior are preserved.",
						MessageType.Info);
				}
				EditorGUILayout.HelpBox("This is a static contact approximation. Distance is measured along each free span from its nearest supporting guide. Scene-view colliders update immediately; play-mode physics uses changes after the next start.",
					MessageType.Info);
			}

			PropertyField(_hitEventProperty, "Has Hit Event");
			PropertyField(_zOffset, "Z-Offset", updateColliders: true);

			// physics material
			var materialLabel = !_modeProperty.hasMultipleDifferentValues
				&& (RubberColliderMode)_modeProperty.enumValueIndex == RubberColliderMode.Physical
				? "Ball Contact Material"
				: "Physics Material";
			if (_foldoutMaterial = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMaterial, materialLabel)) {
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

		private bool AllTargetsCanUsePhysical()
		{
			foreach (var selected in targets) {
				if (selected is RubberColliderComponent collider && !collider.CanUsePhysical) {
					return false;
				}
			}
			return true;
		}

		private bool HasInvalidPhysicalTarget()
		{
			foreach (var selected in targets) {
				if (selected is RubberColliderComponent {
					Mode: RubberColliderMode.Physical,
					CanUsePhysical: false,
				}) {
					return true;
				}
			}
			return false;
		}
	}
}
