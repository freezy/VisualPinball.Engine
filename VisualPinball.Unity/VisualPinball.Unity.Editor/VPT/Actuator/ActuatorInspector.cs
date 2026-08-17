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
	[CustomEditor(typeof(ActuatorComponent)), CanEditMultipleObjects]
	public class ActuatorInspector : ItemInspector
	{
		private SerializedProperty _coilModeProperty;
		private SerializedProperty _initialPositionProperty;
		private SerializedProperty _activationDurationProperty;
		private SerializedProperty _releaseDurationProperty;
		private SerializedProperty _activationCurveProperty;
		private SerializedProperty _releaseCurveProperty;
		private SerializedProperty _releaseDelayProperty;
		private SerializedProperty _activationThresholdProperty;
		private SerializedProperty _oneShotHoldDurationProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();
			_coilModeProperty = serializedObject.FindProperty(nameof(ActuatorComponent.CoilMode));
			_initialPositionProperty = serializedObject.FindProperty(nameof(ActuatorComponent.InitialPosition));
			_activationDurationProperty = serializedObject.FindProperty(nameof(ActuatorComponent.ActivationDuration));
			_releaseDurationProperty = serializedObject.FindProperty(nameof(ActuatorComponent.ReleaseDuration));
			_activationCurveProperty = serializedObject.FindProperty(nameof(ActuatorComponent.ActivationCurve));
			_releaseCurveProperty = serializedObject.FindProperty(nameof(ActuatorComponent.ReleaseCurve));
			_releaseDelayProperty = serializedObject.FindProperty(nameof(ActuatorComponent.ReleaseDelay));
			_activationThresholdProperty = serializedObject.FindProperty(nameof(ActuatorComponent.ActivationThreshold));
			_oneShotHoldDurationProperty = serializedObject.FindProperty(nameof(ActuatorComponent.OneShotHoldDuration));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();
			OnPreInspectorGUI();

			PropertyField(_coilModeProperty);
			PropertyField(_initialPositionProperty);

			EditorGUILayout.Space(8f);
			PropertyField(_activationDurationProperty);
			PropertyField(_activationCurveProperty);
			PropertyField(_releaseDurationProperty);
			PropertyField(_releaseCurveProperty);

			EditorGUILayout.Space(8f);
			PropertyField(_activationThresholdProperty);
			PropertyField(_releaseDelayProperty);
			if (!_coilModeProperty.hasMultipleDifferentValues && (ActuatorCoilMode)_coilModeProperty.enumValueIndex == ActuatorCoilMode.OneShot) {
				PropertyField(_oneShotHoldDurationProperty);
			}

			if (!_coilModeProperty.hasMultipleDifferentValues && (ActuatorCoilMode)_coilModeProperty.enumValueIndex == ActuatorCoilMode.FollowValue) {
				EditorGUILayout.HelpBox("Follow Value requires a plain coil mapping. Wire and dynamic-wire paths are boolean and cannot preserve proportional duty-cycle values.", MessageType.Info);
			} else {
				EditorGUILayout.HelpBox("Binary modes treat every normalized value above the threshold as one energized state. Coil strength controls electrical power, not mechanism position.", MessageType.Info);
			}

			base.OnInspectorGUI();
			EndEditing();
		}
	}
}
