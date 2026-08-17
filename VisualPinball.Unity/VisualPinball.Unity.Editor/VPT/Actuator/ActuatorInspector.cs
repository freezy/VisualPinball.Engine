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

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

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
		private float _previewPosition;

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

		protected override void OnDisable()
		{
			ActuatorPreview.Restore(targets);
			base.OnDisable();
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

			if (!Application.isPlaying) {
				EditorGUILayout.Space(8f);
				EditorGUILayout.LabelField("Animation Preview", EditorStyles.boldLabel);
				if (!ActuatorPreview.HasPreview(targets)) {
					_previewPosition = 0f;
				}

				EditorGUI.BeginChangeCheck();
				var previewPosition = EditorGUILayout.Slider(new GUIContent("Preview Position", "Scrub all Actuator Transform followers without entering Play Mode. Previewed transforms are restored before saving, entering Play Mode, reloading scripts, or leaving this inspector."), _previewPosition, 0f, 1f);
				if (EditorGUI.EndChangeCheck()) {
					_previewPosition = previewPosition;
					ActuatorPreview.Apply(targets, _previewPosition);
				}

				using (new EditorGUI.DisabledScope(!ActuatorPreview.HasPreview(targets))) {
					if (GUILayout.Button("Reset Preview")) {
						ActuatorPreview.Restore(targets);
						_previewPosition = 0f;
					}
				}
			}

			base.OnInspectorGUI();
			EndEditing();
		}
	}

	[InitializeOnLoad]
	internal static class ActuatorPreview
	{
		private sealed class PreviewRecord
		{
			internal ActuatorTransformComponent Follower;
			internal Vector3 LocalPosition;
			internal Quaternion LocalRotation;
		}

		private static readonly Dictionary<ActuatorComponent, List<PreviewRecord>> Records = new();

		static ActuatorPreview()
		{
			AssemblyReloadEvents.beforeAssemblyReload += RestoreAll;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorApplication.quitting += RestoreAll;
			EditorSceneManager.sceneSaving += OnSceneSaving;
			Undo.undoRedoPerformed += RestoreAll;
		}

		internal static bool HasPreview(Object[] inspectedTargets)
		{
			foreach (var inspectedTarget in inspectedTargets) {
				if (inspectedTarget is ActuatorComponent actuator && Records.ContainsKey(actuator)) {
					return true;
				}
			}
			return false;
		}

		internal static void Apply(Object[] inspectedTargets, float position)
		{
			Restore(inspectedTargets);
			var normalizedPosition = Mathf.Clamp01(position);
			if (Mathf.Approximately(normalizedPosition, 0f)) {
				return;
			}

			var followers = Object.FindObjectsByType<ActuatorTransformComponent>(FindObjectsInactive.Include);
			foreach (var inspectedTarget in inspectedTargets) {
				if (!(inspectedTarget is ActuatorComponent actuator)) {
					continue;
				}

				var records = new List<PreviewRecord>();
				foreach (var follower in followers) {
					if (EditorUtility.IsPersistent(follower) || !follower.gameObject.scene.IsValid() || !Follows(follower, actuator)) {
						continue;
					}

					var record = new PreviewRecord {
						Follower = follower,
						LocalPosition = follower.transform.localPosition,
						LocalRotation = follower.transform.localRotation,
					};
					records.Add(record);
					Apply(record, normalizedPosition);
				}

				if (records.Count > 0) {
					Records[actuator] = records;
				}
			}

			Repaint();
		}

		internal static void Restore(Object[] inspectedTargets)
		{
			foreach (var inspectedTarget in inspectedTargets) {
				if (inspectedTarget is ActuatorComponent actuator) {
					Restore(actuator);
				}
			}
			Repaint();
		}

		private static bool Follows(ActuatorTransformComponent follower, ActuatorComponent actuator)
		{
			if (follower._emitter != null) {
				return follower._emitter == actuator;
			}

			foreach (var emitter in follower.GetComponentsInParent<IAnimationValueEmitter>(true)) {
				if (emitter is IAnimationValueEmitter<float>) {
					return ReferenceEquals(emitter, actuator);
				}
			}
			return false;
		}

		private static void Apply(PreviewRecord record, float position)
		{
			var follower = record.Follower;
			var input = follower.Reverse ? 1f - position : position;
			var curve = follower.ResponseCurve;
			var factor = Mathf.Clamp01(curve == null || curve.length < 2 ? input : curve.Evaluate(input));
			if (follower.AnimatePosition) {
				follower.transform.localPosition = record.LocalPosition + follower.PositionOffset * factor;
			}
			if (follower.AnimateRotation) {
				var endRotation = record.LocalRotation * Quaternion.Euler(follower.RotationOffset);
				follower.transform.localRotation = Quaternion.SlerpUnclamped(record.LocalRotation, endRotation, factor);
			}
		}

		private static void Restore(ActuatorComponent actuator)
		{
			if (!Records.TryGetValue(actuator, out var records)) {
				return;
			}

			foreach (var record in records) {
				if (record.Follower == null) {
					continue;
				}
				record.Follower.transform.localPosition = record.LocalPosition;
				record.Follower.transform.localRotation = record.LocalRotation;
			}
			Records.Remove(actuator);
		}

		private static void RestoreAll()
		{
			foreach (var records in Records.Values) {
				foreach (var record in records) {
					if (record.Follower == null) {
						continue;
					}
					record.Follower.transform.localPosition = record.LocalPosition;
					record.Follower.transform.localRotation = record.LocalRotation;
				}
			}
			Records.Clear();
			Repaint();
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode) {
				RestoreAll();
			}
		}

		private static void OnSceneSaving(Scene scene, string path) => RestoreAll();

		private static void Repaint()
		{
			EditorApplication.QueuePlayerLoopUpdate();
			SceneView.RepaintAll();
		}
	}
}
