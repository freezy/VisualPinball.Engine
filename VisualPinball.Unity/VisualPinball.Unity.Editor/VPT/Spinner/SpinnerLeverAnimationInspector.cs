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

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpinnerLeverAnimationComponent)), CanEditMultipleObjects]
	public class SpinnerLeverAnimationInspector : ItemInspector
	{
		private SerializedProperty _rotationSourceProperty;
		private SerializedProperty _rotationAngleProperty;
		private SerializedProperty _shiftProperty;
		private SerializedProperty _offsetProperty;
		private SerializedProperty _minAngleProperty;
		private SerializedProperty _maxAngleProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_rotationSourceProperty = serializedObject.FindProperty(nameof(RotatingComponent._rotationSource));
			_rotationAngleProperty = serializedObject.FindProperty(nameof(RotatingComponent.RotationAngle));
			_shiftProperty = serializedObject.FindProperty(nameof(SpinnerLeverAnimationComponent.Shift));
			_offsetProperty = serializedObject.FindProperty(nameof(SpinnerLeverAnimationComponent.Offset));
			_minAngleProperty = serializedObject.FindProperty(nameof(SpinnerLeverAnimationComponent.MinAngle));
			_maxAngleProperty = serializedObject.FindProperty(nameof(SpinnerLeverAnimationComponent.MaxAngle));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_rotationSourceProperty, "Rotation Source");
			PropertyField(_rotationAngleProperty);
			PropertyField(_shiftProperty);
			PropertyField(_offsetProperty);
			PropertyField(_minAngleProperty);
			PropertyField(_maxAngleProperty);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
