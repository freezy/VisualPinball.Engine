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
			internal float Position;
		}

		private static readonly Dictionary<ActuatorComponent, List<PreviewRecord>> Records = new();

		static ActuatorPreview()
		{
			AssemblyReloadEvents.beforeAssemblyReload += RestoreAll;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorApplication.quitting += RestoreAll;
			EditorSceneManager.sceneSaving += OnSceneSaving;
			PrefabStage.prefabSaving += OnPrefabSaving;
			PrefabStage.prefabStageClosing += OnPrefabStageClosing;
			Undo.undoRedoPerformed += RestoreAll;
			EditorApplication.update += MaintainWorldTranslations;
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
			var normalizedPosition = Mathf.Clamp01(position);
			if (Mathf.Approximately(normalizedPosition, 0f)) {
				Restore(inspectedTargets);
				return;
			}

			ActuatorTransformComponent[] followers = null;
			foreach (var inspectedTarget in inspectedTargets) {
				if (!(inspectedTarget is ActuatorComponent actuator)) {
					continue;
				}

				if (!Records.TryGetValue(actuator, out var records)) {
					followers ??= Object.FindObjectsByType<ActuatorTransformComponent>(FindObjectsInactive.Include);
					records = new List<PreviewRecord>();
					foreach (var follower in followers) {
						if (!CanPreview(follower) || !Follows(follower, actuator)) {
							continue;
						}

						records.Add(new PreviewRecord {
							Follower = follower,
							LocalPosition = follower.transform.localPosition,
							LocalRotation = follower.transform.localRotation,
						});
					}

					if (records.Count > 0) {
						Records[actuator] = records;
					}
				}

				Restore(records);
				foreach (var record in records) {
					Apply(record, normalizedPosition);
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
			if (follower._emitter is IAnimationValueEmitter<float> assignedEmitter) {
				return ReferenceEquals(assignedEmitter, actuator);
			}

			foreach (var emitter in follower.GetComponentsInParent<IAnimationValueEmitter>()) {
				if (emitter is IAnimationValueEmitter<float>) {
					return ReferenceEquals(emitter, actuator);
				}
			}
			return false;
		}

		private static bool CanPreview(ActuatorTransformComponent follower)
		{
			return follower != null
			       && !EditorUtility.IsPersistent(follower)
			       && follower.gameObject.scene.IsValid()
			       && !EditorSceneManager.IsPreviewSceneObject(follower);
		}

		private static void Apply(PreviewRecord record, float position)
		{
			var follower = record.Follower;
			if (follower == null) {
				return;
			}

			record.Position = position;
			var input = follower.Reverse ? 1f - position : position;
			var curve = follower.ResponseCurve;
			var factor = Mathf.Clamp01(curve == null || curve.length < 2 ? input : curve.Evaluate(input));
			if (follower.AnimatePosition) {
				if (follower.TranslationSpace == ActuatorTranslationSpace.World) {
					ApplyWorldPosition(record, factor);
				} else {
					follower.transform.localPosition = record.LocalPosition + record.LocalRotation * follower.PositionOffset * factor;
				}
			}
			if (follower.AnimateRotation) {
				var endRotation = record.LocalRotation * Quaternion.Euler(follower.RotationOffset);
				follower.transform.localRotation = Quaternion.SlerpUnclamped(record.LocalRotation, endRotation, factor);
			}
		}

		private static bool ApplyWorldPosition(PreviewRecord record, float factor)
		{
			var follower = record.Follower;
			var parent = follower.transform.parent;
			var baseline = parent != null ? parent.TransformPoint(record.LocalPosition) : record.LocalPosition;
			var desiredPosition = baseline + follower.PositionOffset * factor;
			var desiredLocalPosition = parent != null ? parent.InverseTransformPoint(desiredPosition) : desiredPosition;
			if ((follower.transform.localPosition - desiredLocalPosition).sqrMagnitude <= 0.000000000001f) {
				return false;
			}

			follower.transform.localPosition = desiredLocalPosition;
			return true;
		}

		private static void MaintainWorldTranslations()
		{
			var changed = false;
			foreach (var records in Records.Values) {
				foreach (var record in records) {
					var follower = record.Follower;
					if (follower == null || !follower.AnimatePosition || follower.TranslationSpace != ActuatorTranslationSpace.World) {
						continue;
					}

					var input = follower.Reverse ? 1f - record.Position : record.Position;
					var curve = follower.ResponseCurve;
					var factor = Mathf.Clamp01(curve == null || curve.length < 2 ? input : curve.Evaluate(input));
					changed |= ApplyWorldPosition(record, factor);
				}
			}

			if (changed) {
				Repaint();
			}
		}

		private static void Restore(ActuatorComponent actuator)
		{
			if (!Records.TryGetValue(actuator, out var records)) {
				return;
			}

			Restore(records);
			Records.Remove(actuator);
		}

		private static void Restore(IEnumerable<PreviewRecord> records)
		{
			foreach (var record in records) {
				if (record.Follower == null) {
					continue;
				}
				record.Follower.transform.localPosition = record.LocalPosition;
				record.Follower.transform.localRotation = record.LocalRotation;
			}
		}

		private static void RestoreAll()
		{
			foreach (var records in Records.Values) {
				Restore(records);
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
		private static void OnPrefabSaving(GameObject prefabRoot) => RestoreAll();
		private static void OnPrefabStageClosing(PrefabStage stage) => RestoreAll();

		private static void Repaint()
		{
			EditorApplication.QueuePlayerLoopUpdate();
			SceneView.RepaintAll();
		}
	}
}
