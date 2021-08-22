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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlayfieldColliderAuthoring)), CanEditMultipleObjects]
	public class PlayfieldColliderInspector : ItemColliderInspector<Table, TableData, PlayfieldAuthoring, PlayfieldColliderAuthoring>
	{
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _elasticityFalloffProperty;
		private SerializedProperty _frictionProperty;
		private SerializedProperty _scatterProperty;
		private SerializedProperty _gravityProperty;
		private SerializedProperty _defaultScatterProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_gravityProperty = serializedObject.FindProperty(nameof(PlayfieldColliderAuthoring.Gravity));
			_elasticityProperty = serializedObject.FindProperty(nameof(PlayfieldColliderAuthoring.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(PlayfieldColliderAuthoring.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(PlayfieldColliderAuthoring.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(PlayfieldColliderAuthoring.Scatter));
			_defaultScatterProperty = serializedObject.FindProperty(nameof(PlayfieldColliderAuthoring.DefaultScatter));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_gravityProperty, "Gravity Constant");
			PropertyField(_frictionProperty, "Playfield Friction");
			PropertyField(_elasticityProperty, "Playfield Elasticity");
			PropertyField(_elasticityFalloffProperty, "Playfield Elasticity Falloff");
			PropertyField(_scatterProperty, "Playfield Scatter");
			PropertyField(_defaultScatterProperty, "Default Elements Scatter");

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
