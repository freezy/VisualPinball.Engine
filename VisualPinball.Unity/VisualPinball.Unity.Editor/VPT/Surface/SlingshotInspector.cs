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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SlingshotComponent))]
	public class SlingshotInspector : ItemInspector
	{
		private SlingshotComponent _slingshot;

		private SerializedProperty _surfaceProperty;
		private SerializedProperty _rubberOffProperty;
		private SerializedProperty _rubberOnProperty;
		private SerializedProperty _animationDurationProperty;
		private SerializedProperty _animationCurveProperty;
		private SerializedProperty _coilArmProperty;
		private SerializedProperty _coilArmAngleProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_slingshot = target as SlingshotComponent;

			_surfaceProperty = serializedObject.FindProperty(nameof(SlingshotComponent.SlingshotSurface));
			_rubberOffProperty = serializedObject.FindProperty(nameof(SlingshotComponent.RubberOff));
			_rubberOnProperty = serializedObject.FindProperty(nameof(SlingshotComponent.RubberOn));
			_coilArmProperty = serializedObject.FindProperty(nameof(SlingshotComponent.CoilArm));
			_coilArmAngleProperty = serializedObject.FindProperty(nameof(SlingshotComponent.CoilArmAngle));
			_animationDurationProperty = serializedObject.FindProperty(nameof(SlingshotComponent.AnimationDuration));
			_animationCurveProperty = serializedObject.FindProperty(nameof(SlingshotComponent.AnimationCurve));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_surfaceProperty, "Slingshot Wall");
			PropertyField(_rubberOffProperty, "Rubber Off",  true);
			PropertyField(_rubberOnProperty, "Rubber On",  true);

			if (_slingshot.RubberOn && _slingshot.RubberOff &&
			    _slingshot.RubberOn.DragPoints.Length != _slingshot.RubberOff.DragPoints.Length) {
				EditorGUILayout.HelpBox($"In order to animate the rubber, the number of drag points of both rubbers must be equal. Here we have {_slingshot.RubberOn.DragPoints.Length} (on) and {_slingshot.RubberOff.DragPoints.Length} (off).", MessageType.Error);
			}

			EditorGUILayout.Space(10f);
			PropertyField(_coilArmProperty, "Coil Arm");
			PropertyField(_coilArmAngleProperty, "Arm Angle");

			EditorGUILayout.Space(10f);
			PropertyField(_animationDurationProperty, "Animation Duration");
			PropertyField(_animationCurveProperty, "Animation Curve");

			EditorGUILayout.Space(10f);
			EditorGUI.BeginChangeCheck();
			var pos = EditorGUILayout.Slider("Test", _slingshot.Position, 0f, 1f);
			if (EditorGUI.EndChangeCheck()) {
				_slingshot.Position = pos;
				_slingshot.RebuildMeshes();
			}

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
