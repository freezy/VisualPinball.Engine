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
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(FlipperColliderAuthoring))]
	public class FlipperColliderInspector : ItemColliderInspector<Flipper, FlipperData, FlipperAuthoring, FlipperColliderAuthoring>
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

		protected override void OnEnable()
		{
			base.OnEnable();

			_massProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.Mass));
			_strengthProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.Strength));
			_returnProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.Return));
			_rampUpProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.RampUp));
			_torqueDampingProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.TorqueDamping));
			_torqueDampingAngleProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.TorqueDampingAngle));
			_elasticityProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.Scatter));
			_flipperCorrectionProperty = serializedObject.FindProperty(nameof(FlipperColliderAuthoring.FlipperCorrection));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

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

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
