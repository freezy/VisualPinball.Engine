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

using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpinnerColliderComponent)), CanEditMultipleObjects]
	public class SpinnerColliderInspector : ColliderInspector<SpinnerData, SpinnerComponent, SpinnerColliderComponent>
	{
		private SerializedProperty _massProperty;
		private SerializedProperty _elasticityProperty;
		private SerializedProperty _zPosProperty;
		private SerializedProperty _distanceProperty;
		private SerializedProperty _horizontalOffsetProperty;

		protected override void OnEnable()
		{
			base.OnEnable();
			_massProperty = serializedObject.FindProperty(nameof(SpinnerColliderComponent.Mass));
			_elasticityProperty = serializedObject.FindProperty(nameof(SpinnerColliderComponent.Elasticity));
			_zPosProperty = serializedObject.FindProperty(nameof(SpinnerColliderComponent.ZPosition));
			_distanceProperty = serializedObject.FindProperty(nameof(SpinnerColliderComponent.Distance));
			_horizontalOffsetProperty = serializedObject.FindProperty(nameof(SpinnerColliderComponent.HorizontalOffset));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_massProperty);
			PropertyField(_elasticityProperty, updateTransforms: true);
			DrawOffsetField();

			base.OnInspectorGUI();

			EndEditing();
		}

		private void DrawOffsetField()
		{
			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = _horizontalOffsetProperty.hasMultipleDifferentValues
			                           || _distanceProperty.hasMultipleDifferentValues
			                           || _zPosProperty.hasMultipleDifferentValues;
			var offset = EditorGUILayout.Vector3Field(
				new GUIContent("Offset", "Collider-local X, Y, and Z offset in VPX units."),
				new Vector3(_horizontalOffsetProperty.floatValue, _distanceProperty.floatValue, _zPosProperty.floatValue)
			);
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck()) {
				_horizontalOffsetProperty.floatValue = offset.x;
				_distanceProperty.floatValue = offset.y;
				_zPosProperty.floatValue = offset.z;
				ColliderComponent.CollidersDirty = true;
			}
		}
	}
}
