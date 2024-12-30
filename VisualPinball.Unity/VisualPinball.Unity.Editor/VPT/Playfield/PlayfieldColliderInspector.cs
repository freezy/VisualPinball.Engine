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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlayfieldColliderComponent)), CanEditMultipleObjects]
	public class PlayfieldColliderInspector : ColliderInspector<TableData, PlayfieldComponent, PlayfieldColliderComponent>
	{
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _elasticityFalloffProperty;
		private SerializedProperty _frictionProperty;
		private SerializedProperty _scatterProperty;
		private SerializedProperty _gravityProperty;
		private SerializedProperty _defaultScatterProperty;
		private SerializedProperty _collideWithBoundsProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_gravityProperty = serializedObject.FindProperty(nameof(PlayfieldColliderComponent.Gravity));
			_elasticityProperty = serializedObject.FindProperty(nameof(PlayfieldColliderComponent.Elasticity));
			_elasticityFalloffProperty = serializedObject.FindProperty(nameof(PlayfieldColliderComponent.ElasticityFalloff));
			_frictionProperty = serializedObject.FindProperty(nameof(PlayfieldColliderComponent.Friction));
			_scatterProperty = serializedObject.FindProperty(nameof(PlayfieldColliderComponent.Scatter));
			_defaultScatterProperty = serializedObject.FindProperty(nameof(PlayfieldColliderComponent.DefaultScatter));
			_collideWithBoundsProperty = serializedObject.FindProperty(nameof(PlayfieldColliderComponent.CollideWithBounds));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnParentPreInspectorGUI();

			PropertyField(_gravityProperty, "Gravity Constant");
			PropertyField(_frictionProperty, "Playfield Friction");
			PropertyField(_elasticityProperty, "Playfield Elasticity");
			PropertyField(_elasticityFalloffProperty, "Playfield Elasticity Falloff");
			PropertyField(_scatterProperty, "Playfield Scatter");
			PropertyField(_defaultScatterProperty, "Default Elements Scatter");
			PropertyField(_collideWithBoundsProperty, "Collide with Bounds");

			base.OnInspectorGUI();

			ColliderComponent.ShowAllColliderMeshes = EditorGUILayout.Toggle("Show All Colliders", ColliderComponent.ShowAllColliderMeshes);

			EndEditing();
		}
	}
}
