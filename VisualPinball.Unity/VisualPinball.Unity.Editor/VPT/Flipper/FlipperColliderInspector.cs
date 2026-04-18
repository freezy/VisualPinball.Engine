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
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(FlipperColliderComponent)), CanEditMultipleObjects]
	public class FlipperColliderInspector : ColliderInspector<FlipperData, FlipperComponent, FlipperColliderComponent>
	{
		private bool _foldoutMaterial = true;

		private SerializedProperty _massProperty;
		private SerializedProperty _strengthProperty;
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _elasticityFalloffProperty;
		private SerializedProperty _frictionProperty;
		private SerializedProperty _returnProperty;
		private SerializedProperty _rampUpProperty;
		private SerializedProperty _torqueDampingProperty;
		private SerializedProperty _torqueDampingAngleProperty;
		private SerializedProperty _scatterProperty;
		private SerializedProperty _flipperCorrectionProperty;

		private bool _foldoutFlipperTricks = true;
		private bool _foldoutLiveCatch = true;

		#region Flipper Tricks

		private SerializedProperty _useFlipperTricksPhysicsProperty;
		private SerializedProperty _sosRampUpProperty;
		private SerializedProperty _sosEmProperty;
		private SerializedProperty _eosReturnProperty;
		private SerializedProperty _eosTNewProperty;
		private SerializedProperty _eosANewProperty;
		private SerializedProperty _eosRampUpProperty;
		private SerializedProperty _overshootProperty;
		private SerializedProperty _bumpOnReleaseProperty;

		#endregion

		#region Live Catch

		private SerializedProperty _useFlipperLiveCatch;
		private SerializedProperty _liveCatchDistanceMin;
		private SerializedProperty _liveCatchDistanceMax;
		private SerializedProperty _liveCatchMinimalBallSpeed;
		private SerializedProperty _liveCatchPerfectTime;
		private SerializedProperty _liveCatchFullTime;
		private SerializedProperty _liveCatchInaccurateBounceSpeedMultiplier;
		private SerializedProperty _liveCatchMinimumBounceSpeedMultiplier;
		private SerializedProperty _liveCatchBaseDampenDistance;
		private SerializedProperty _liveCatchBaseDampen;

		#endregion

		protected override void OnEnable()
		{
			base.OnEnable();

			_massProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.Mass));
			_strengthProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.Strength));
			_returnProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.Return));
			_rampUpProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.RampUp));
			_torqueDampingProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.TorqueDamping));
			_torqueDampingAngleProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.TorqueDampingAngle));
			_elasticityProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.Scatter));
			_flipperCorrectionProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.FlipperCorrection));

			#region Flipper Tricks
			_useFlipperTricksPhysicsProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.useFlipperTricksPhysics));
			_sosRampUpProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.SOSRampUp));
			_sosEmProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.SOSEM));
			_eosReturnProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.EOSReturn));
			_eosTNewProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.EOSTNew));
			_eosANewProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.EOSANew));
			_eosRampUpProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.EOSRampup));
			_overshootProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.Overshoot));
			_bumpOnReleaseProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.BumpOnRelease));
			#endregion

			#region Live Catch
			_useFlipperLiveCatch = serializedObject.FindProperty(nameof(FlipperColliderComponent.useFlipperLiveCatch));
			_liveCatchDistanceMin = serializedObject.FindProperty(nameof(FlipperColliderComponent.LiveCatchDistanceMin));
			_liveCatchDistanceMax = serializedObject.FindProperty(nameof(FlipperColliderComponent.LiveCatchDistanceMax));
			_liveCatchMinimalBallSpeed = serializedObject.FindProperty(nameof(FlipperColliderComponent.LiveCatchMinimalBallSpeed));
			_liveCatchPerfectTime = serializedObject.FindProperty(nameof(FlipperColliderComponent.LiveCatchPerfectTime));
			_liveCatchFullTime = serializedObject.FindProperty(nameof(FlipperColliderComponent.LiveCatchFullTime));
			_liveCatchInaccurateBounceSpeedMultiplier = serializedObject.FindProperty(nameof(FlipperColliderComponent.LiveCatchInaccurateBounceSpeedMultiplier));
			_liveCatchMinimumBounceSpeedMultiplier = serializedObject.FindProperty(nameof(FlipperColliderComponent.LiveCatchMinmalBounceSpeedMultiplier));
			_liveCatchBaseDampenDistance = serializedObject.FindProperty(nameof(FlipperColliderComponent.LiveCatchBaseDampenDistance));
			_liveCatchBaseDampen = serializedObject.FindProperty(nameof(FlipperColliderComponent.LiveCatchBaseDampen));
			#endregion
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_massProperty);
			PropertyField(_strengthProperty);
			PropertyField(_returnProperty, "Return Strength");
			PropertyField(_rampUpProperty, "Coil Ramp Up");
			PropertyField(_torqueDampingProperty, "EOS Torque");
			PropertyField(_torqueDampingAngleProperty, "EOS Torque Angle");
			PropertyField(_flipperCorrectionProperty);

			// physics material
			if (_foldoutMaterial = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMaterial, "Physics Material")) {
				PropertyField(_elasticityProperty);
				PropertyField(_elasticityFalloffProperty);
				PropertyField(_frictionProperty);
				PropertyField(_scatterProperty, "Scatter Angle");
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutFlipperTricks = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutFlipperTricks, "Flipper Tricks")) {

				PropertyField(_useFlipperTricksPhysicsProperty, "Use Flipper Tricks");

				EditorGUI.BeginDisabledGroup(!_useFlipperTricksPhysicsProperty.boolValue);
				PropertyField(_sosRampUpProperty, "Start Ramp");
				PropertyField(_sosEmProperty, "Start Elasticity");
				PropertyField(_eosReturnProperty, "Return Torque");
				PropertyField(_eosTNewProperty, "EOS Torque");
				PropertyField(_eosANewProperty, "EOS Angle");
				PropertyField(_eosRampUpProperty, "EOS Ramp");
				PropertyField(_overshootProperty, "Overshoot");
				PropertyField(_bumpOnReleaseProperty, "Release Bump");
				EditorGUI.EndDisabledGroup();

			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutLiveCatch = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutLiveCatch, "Live Catch")) {

				PropertyField(_useFlipperLiveCatch, "Use Live Catch");

				EditorGUI.BeginDisabledGroup(!_useFlipperLiveCatch.boolValue);
				PropertyField(_liveCatchDistanceMin, "Min Distance");
				PropertyField(_liveCatchDistanceMax, "Max Distance");
				PropertyField(_liveCatchMinimalBallSpeed, "Min Hit Speed");
				PropertyField(_liveCatchPerfectTime, "Perfect Time");
				PropertyField(_liveCatchFullTime, "Full Time");
				PropertyField(_liveCatchMinimumBounceSpeedMultiplier, "Perfect Bounce");
				PropertyField(_liveCatchInaccurateBounceSpeedMultiplier, "Late Bounce");
				PropertyField(_liveCatchBaseDampenDistance, "Base Zone");
				PropertyField(_liveCatchBaseDampen, "Base Dampen");
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
