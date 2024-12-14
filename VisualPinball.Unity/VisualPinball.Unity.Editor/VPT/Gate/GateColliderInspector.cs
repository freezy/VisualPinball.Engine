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
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(GateColliderComponent)), CanEditMultipleObjects]
	public class GateColliderInspector : ColliderInspector<GateData, GateComponent, GateColliderComponent>
	{
		private SerializedProperty _isKinematicProperty;
		private SerializedProperty _zLowProperty;
		private SerializedProperty _distanceProperty;
		private SerializedProperty _angleMinProperty;
		private SerializedProperty _angleMaxProperty;
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _frictionProperty;
		private SerializedProperty _dampingProperty;
		private SerializedProperty _gravityFactorProperty;
		private SerializedProperty _twoWayProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_isKinematicProperty = serializedObject.FindProperty(nameof(SpinnerColliderComponent._isKinematic));
			_zLowProperty = serializedObject.FindProperty(nameof(GateColliderComponent.ZLow));
			_distanceProperty = serializedObject.FindProperty(nameof(GateColliderComponent.Distance));
			_angleMinProperty = serializedObject.FindProperty(nameof(GateColliderComponent._angleMin));
			_angleMaxProperty = serializedObject.FindProperty(nameof(GateColliderComponent._angleMax));
			_elasticityProperty = serializedObject.FindProperty(nameof(GateColliderComponent.Elasticity));
			_frictionProperty = serializedObject.FindProperty(nameof(GateColliderComponent.Friction));
			_dampingProperty = serializedObject.FindProperty(nameof(GateColliderComponent.Damping));
			_gravityFactorProperty = serializedObject.FindProperty(nameof(GateColliderComponent.GravityFactor));
			_twoWayProperty = serializedObject.FindProperty(nameof(GateColliderComponent._twoWay));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			PropertyField(_isKinematicProperty, "Movable");
			PropertyField(_zLowProperty, "Z-Low");
			PropertyField(_distanceProperty, "Distance");
			PropertyField(_angleMinProperty, "Close Angle");
			PropertyField(_angleMaxProperty, "Open Angle");
			PropertyField(_elasticityProperty);
			PropertyField(_frictionProperty);
			PropertyField(_dampingProperty);
			PropertyField(_gravityFactorProperty);
			PropertyField(_twoWayProperty, "Is Two-Way Gate");

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
