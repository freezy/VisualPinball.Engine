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
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(GateColliderAuthoring)), CanEditMultipleObjects]
	public class GateColliderInspector : ItemColliderInspector<GateData, GateAuthoring, GateColliderAuthoring>
	{
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

			_angleMinProperty = serializedObject.FindProperty(nameof(GateColliderAuthoring._angleMin));
			_angleMaxProperty = serializedObject.FindProperty(nameof(GateColliderAuthoring._angleMax));
			_elasticityProperty = serializedObject.FindProperty(nameof(GateColliderAuthoring.Elasticity));
			_frictionProperty = serializedObject.FindProperty(nameof(GateColliderAuthoring.Friction));
			_dampingProperty = serializedObject.FindProperty(nameof(GateColliderAuthoring.Damping));
			_gravityFactorProperty = serializedObject.FindProperty(nameof(GateColliderAuthoring.GravityFactor));
			_twoWayProperty = serializedObject.FindProperty(nameof(GateColliderAuthoring._twoWay));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			PropertyField(_angleMinProperty, "Close Angle");
			PropertyField(_angleMaxProperty, "Open Angle");
			PropertyField(_elasticityProperty);
			PropertyField(_frictionProperty);
			PropertyField(_dampingProperty);
			PropertyField(_gravityFactorProperty);
			PropertyField(_twoWayProperty, "Is Two-Way Gate");

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
