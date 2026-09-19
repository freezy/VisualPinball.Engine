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

using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberColliderComponent)), CanEditMultipleObjects]
	public class RubberColliderInspector : ColliderInspector<RubberData, RubberComponent, RubberColliderComponent>
	{
		private bool _foldoutMaterial = true;
		private bool _foldoutDeformation = true;
		private bool _bakeRequested;
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
		private SerializedProperty _guideElasticityScaleProperty;
		private SerializedProperty _guideInfluenceLengthProperty;
		private SerializedProperty _maximumResponseSegmentLengthProperty;
		private SerializedProperty _deformationEnabledProperty;
		private SerializedProperty _deflectionClearanceProperty;

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
			_guideElasticityScaleProperty = serializedObject.FindProperty("_guideElasticityScale");
			_guideInfluenceLengthProperty = serializedObject.FindProperty("_guideInfluenceLength");
			_maximumResponseSegmentLengthProperty = serializedObject.FindProperty("_maximumResponseSegmentLength");
			_deformationEnabledProperty = serializedObject.FindProperty("_deformationEnabled");
			_deflectionClearanceProperty = serializedObject.FindProperty("_deflectionClearance");
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
						"Every selected rubber requires a usable serialized guided path.", "OK");
				} else {
					_modeProperty.enumValueIndex = (int)requestedMode;
				}
			}
			EditorGUI.showMixedValue = false;
			if (HasInvalidPhysicalTarget()) {
				EditorGUILayout.HelpBox("This Physical setting is preserved but cannot run until the rubber has a usable guided path.",
					MessageType.Error);
			}
			var showsPhysical = _modeProperty.hasMultipleDifferentValues
				|| (RubberColliderMode)_modeProperty.enumValueIndex == RubberColliderMode.Physical;
			if (showsPhysical) {
				var deformationEnabled = _deformationEnabledProperty.hasMultipleDifferentValues
					|| _deformationEnabledProperty.boolValue;
				DrawDeformationSection(deformationEnabled);

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Guide-distance Elasticity", EditorStyles.boldLabel);
				if (deformationEnabled) {
					EditorGUILayout.LabelField(" ", "Hidden while the compliant rebound is enabled; it attenuates the rebound's restitution instead.");
				} else {
					PropertyField(_guideDistanceElasticityEnabledProperty, "Enabled", updateColliders: true);
					var enableResponseFields = _guideDistanceElasticityEnabledProperty.hasMultipleDifferentValues
						|| _guideDistanceElasticityEnabledProperty.boolValue;
					EditorGUI.BeginDisabledGroup(!enableResponseFields);
					PropertyField(_guideElasticityScaleProperty, "Guide Elasticity Scale",
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
						&& !_guideElasticityScaleProperty.hasMultipleDifferentValues
						&& _guideElasticityScaleProperty.floatValue == 1f) {
						EditorGUILayout.HelpBox("The response is inactive while Guide Elasticity Scale is 1; current collider count and contact behavior are preserved.",
							MessageType.Info);
					}
					EditorGUILayout.HelpBox("This is a static contact approximation. Distance is measured along each free span from its nearest supporting guide. Scene-view colliders update immediately; play-mode physics uses changes after the next start.",
						MessageType.Info);
				}
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

			if (!_bakeRequested) {
				return;
			}
			_bakeRequested = false;
			foreach (var selected in targets.OfType<RubberColliderComponent>()) {
				RubberDeformationBaker.Bake(selected);
			}
			Repaint();
		}

		private void DrawDeformationSection(bool deformationEnabled)
		{
			_foldoutDeformation = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutDeformation,
				"Compliant Rebound");
			if (!_foldoutDeformation) {
				EditorGUILayout.EndFoldoutHeaderGroup();
				return;
			}

			PropertyField(_deformationEnabledProperty, "Enabled", updateColliders: true);
			EditorGUI.BeginDisabledGroup(!deformationEnabled);
			PropertyField(_rubberPhysicsMaterialProperty, "Rebound Material");
			PropertyField(_deflectionClearanceProperty, "Deflection Clearance");
			if (GUILayout.Button("Recompute Compliant Rebound")) {
				// Run after EndEditing, so the serialized object is not applied over the
				// descriptors the baker writes directly.
				_bakeRequested = true;
			}
			EditorGUI.EndDisabledGroup();

			if (!deformationEnabled) {
				EditorGUILayout.HelpBox("Physical uses static round-cord collision and the standard contact material below. The rebound material drives the compliant free-span response instead.",
					MessageType.Info);
			} else if (targets.Length == 1 && target is RubberColliderComponent collider) {
				EditorGUILayout.HelpBox("Free spans rebound off a massless-string model, so where and how hard the ball hits decides the direction and speed. The collision geometry stays static; supported arcs keep the wall response.",
					MessageType.Info);
				DrawReboundStatus(collider);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
		}

		private static void DrawReboundStatus(RubberColliderComponent collider)
		{
			// the compiled data is a cache of the inputs: bring it up to date, then say
			// whether the rebound is active and, if not, why
			collider.EnsureBakesCurrent();
			var spans = collider.SpanDescriptors;
			switch (collider.BakeOutcome) {
				case RubberDeformationBakeOutcome.Deformable: {
					var eligible = RubberSpanReboundEligibility.EligibleDescriptors(collider).Count;
					EditorGUILayout.HelpBox(
						$"Compliant rebound active on {eligible} free span{(eligible == 1 ? "" : "s")}.",
						MessageType.Info);
					break;
				}
				case RubberDeformationBakeOutcome.StaticFallback:
					EditorGUILayout.HelpBox($"Compliant rebound inactive, the rubber keeps the wall response: {collider.BakeReason}",
						MessageType.Warning);
					break;
				default:
					EditorGUILayout.HelpBox($"Compliant rebound cannot be set up: {collider.BakeReason}",
						MessageType.Error);
					break;
			}

			if (spans.Count == 0) {
				return;
			}
			EditorGUILayout.LabelField("Spans", EditorStyles.boldLabel);
			EditorGUI.BeginDisabledGroup(true);
			for (var i = 0; i < spans.Count; i++) {
				var span = spans[i];
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				EditorGUILayout.LabelField($"Span {i + 1}", $"path element {span.PathElementIndex}");
				EditorGUILayout.LabelField("Installed Stretch",
					Format(span.InstalledStretch, "0.####") + "×");
				EditorGUILayout.LabelField("Tension",
					Format(RubberDeformationBaker.ForceToNewtons(span.InstalledTension), "0.####") + " N");
				EditorGUILayout.LabelField("First-mode Frequency",
					Format(RubberDeformationBaker.FrequencyHz(span.ModalStiffness.x, span.ModalMass.x), "0.##") + " Hz");
				EditorGUILayout.LabelField("Deflection Cap",
					Format(span.MaxDeflection, "0.###") + " VPX");
				EditorGUILayout.LabelField("Segments", span.SegmentCount.ToString(CultureInfo.InvariantCulture));
				EditorGUILayout.EndVertical();
			}
			EditorGUI.EndDisabledGroup();
		}

		private static string Format(float value, string format)
			=> value.ToString(format, CultureInfo.InvariantCulture);

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
