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
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TurntableComponent))]
	public class TurntableInspector : ItemInspector
	{
		private SerializedProperty _radiusProperty;
		private SerializedProperty _heightRangeProperty;
		private SerializedProperty _maxSpeedProperty;
		private SerializedProperty _spinUpProperty;
		private SerializedProperty _spinDownProperty;
		private SerializedProperty _motorOnStartProperty;
		private SerializedProperty _spinClockwiseProperty;
		private SerializedProperty _isKinematicProperty;
		private SerializedProperty _rotationTargetProperty;
		private SerializedProperty _visualSpeedFactorProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_radiusProperty = serializedObject.FindProperty(nameof(TurntableComponent.Radius));
			_heightRangeProperty = serializedObject.FindProperty(nameof(TurntableComponent.HeightRange));
			_maxSpeedProperty = serializedObject.FindProperty(nameof(TurntableComponent.MaxSpeed));
			_spinUpProperty = serializedObject.FindProperty(nameof(TurntableComponent.SpinUp));
			_spinDownProperty = serializedObject.FindProperty(nameof(TurntableComponent.SpinDown));
			_motorOnStartProperty = serializedObject.FindProperty(nameof(TurntableComponent.MotorOnStart));
			_spinClockwiseProperty = serializedObject.FindProperty(nameof(TurntableComponent.SpinClockwise));
			_isKinematicProperty = serializedObject.FindProperty(nameof(TurntableComponent.IsKinematic));
			_rotationTargetProperty = serializedObject.FindProperty(nameof(TurntableComponent.RotationTarget));
			_visualSpeedFactorProperty = serializedObject.FindProperty(nameof(TurntableComponent.VisualSpeedFactor));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();
			OnPreInspectorGUI();

			PropertyField(_radiusProperty);
			PropertyField(_heightRangeProperty);
			PropertyField(_maxSpeedProperty);
			PropertyField(_spinUpProperty);
			PropertyField(_spinDownProperty);

			EditorGUILayout.Space(8f);
			PropertyField(_motorOnStartProperty);
			PropertyField(_spinClockwiseProperty);
			// kinematic registration is fixed at startup; toggling during play would silently do nothing
			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				PropertyField(_isKinematicProperty);
			}
			PropertyField(_rotationTargetProperty);
			PropertyField(_visualSpeedFactorProperty);

			base.OnInspectorGUI();
			EndEditing();
		}
	}
}
