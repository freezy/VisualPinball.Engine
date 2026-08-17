// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(ActuatorTransformComponent)), CanEditMultipleObjects]
	public class ActuatorTransformInspector : UnityEditor.Editor
	{
		private SerializedProperty _emitterProperty;
		private SerializedProperty _animatePositionProperty;
		private SerializedProperty _positionOffsetProperty;
		private SerializedProperty _animateRotationProperty;
		private SerializedProperty _rotationOffsetProperty;
		private SerializedProperty _responseCurveProperty;
		private SerializedProperty _reverseProperty;

		private void OnEnable()
		{
			_emitterProperty = serializedObject.FindProperty(nameof(ActuatorTransformComponent._emitter));
			_animatePositionProperty = serializedObject.FindProperty(nameof(ActuatorTransformComponent.AnimatePosition));
			_positionOffsetProperty = serializedObject.FindProperty(nameof(ActuatorTransformComponent.PositionOffset));
			_animateRotationProperty = serializedObject.FindProperty(nameof(ActuatorTransformComponent.AnimateRotation));
			_rotationOffsetProperty = serializedObject.FindProperty(nameof(ActuatorTransformComponent.RotationOffset));
			_responseCurveProperty = serializedObject.FindProperty(nameof(ActuatorTransformComponent.ResponseCurve));
			_reverseProperty = serializedObject.FindProperty(nameof(ActuatorTransformComponent.Reverse));
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(_emitterProperty);
			EditorGUILayout.PropertyField(_animatePositionProperty);
			if (_animatePositionProperty.hasMultipleDifferentValues || _animatePositionProperty.boolValue) {
				EditorGUILayout.PropertyField(_positionOffsetProperty);
			}
			EditorGUILayout.PropertyField(_animateRotationProperty);
			if (_animateRotationProperty.hasMultipleDifferentValues || _animateRotationProperty.boolValue) {
				EditorGUILayout.PropertyField(_rotationOffsetProperty);
			}
			EditorGUILayout.PropertyField(_responseCurveProperty);
			EditorGUILayout.PropertyField(_reverseProperty);

			if (!_animatePositionProperty.hasMultipleDifferentValues && !_animateRotationProperty.hasMultipleDifferentValues && !_animatePositionProperty.boolValue && !_animateRotationProperty.boolValue) {
				EditorGUILayout.HelpBox("Enable position, rotation, or both for this follower to move.", MessageType.Warning);
			}
			EditorGUILayout.HelpBox("Place the GameObject origin at the physical pivot. Any VPE collider moving with this transform must be active and marked Kinematic when the table loads.", MessageType.Info);

			serializedObject.ApplyModifiedProperties();
		}
	}
}
