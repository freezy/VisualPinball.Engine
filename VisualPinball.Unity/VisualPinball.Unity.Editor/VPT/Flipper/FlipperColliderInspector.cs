// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

		#region Flipper_Tricks
		private SerializedProperty _useFlipperTricksPhysicsProperty;
		private SerializedProperty _SOSRampUpProperty;
		private SerializedProperty _SOSEMProperty;
		private SerializedProperty _EOSReturnProperty;
		private SerializedProperty _EOSTNewProperty;
		private SerializedProperty _EOSANewProperty;
		private SerializedProperty _EOSRampupProperty;
		private SerializedProperty _OvershootProperty;
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

			#region Flipper_Tricks
			_useFlipperTricksPhysicsProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.useFlipperTricksPhysics));
			_SOSRampUpProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.SOSRampUp));
			_SOSEMProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.SOSEM));
			_EOSReturnProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.EOSReturn));
			_EOSTNewProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.EOSTNew));
			_EOSANewProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.EOSANew));
			_EOSRampupProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.EOSRampup));
			_OvershootProperty = serializedObject.FindProperty(nameof(FlipperColliderComponent.Overshoot));
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

			if (_foldoutFlipperTricks = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutFlipperTricks, "Flipper Tricks"))
			{

				PropertyField(_useFlipperTricksPhysicsProperty, "Use Flipper Tricks");

				EditorGUI.BeginDisabledGroup(!_useFlipperTricksPhysicsProperty.boolValue);
				PropertyField(_SOSRampUpProperty, "SOSRampUP");
				PropertyField(_SOSEMProperty, "SOSEM");
				PropertyField(_EOSReturnProperty, "EOSReturn");
				PropertyField(_EOSTNewProperty, "EOSTNew");
				PropertyField(_EOSANewProperty, "EOSANew");
				PropertyField(_EOSRampupProperty, "EOSRampup");
				PropertyField(_OvershootProperty, "");
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();




			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
