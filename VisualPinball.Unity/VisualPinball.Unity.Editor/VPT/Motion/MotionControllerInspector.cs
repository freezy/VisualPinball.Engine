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
	[CustomPropertyDrawer(typeof(MotionPositionSwitch))]
	public class MotionPositionSwitchPropertyDrawer : PropertyDrawer
	{
		private const float Padding = 2f;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var typeProperty = property.FindPropertyRelative(nameof(MotionPositionSwitch.Type));
			if (typeProperty.hasMultipleDifferentValues) {
				return (EditorGUIUtility.singleLineHeight + Padding) * 5f + 4f;
			}
			var type = (MotionPositionSwitchType)typeProperty.enumValueIndex;
			var lines = type switch {
				MotionPositionSwitchType.EnableBetween => 3f,
				MotionPositionSwitchType.AlwaysPulse => 4f,
				MotionPositionSwitchType.PulseBetween => 5f,
				_ => 3f,
			};
			return (EditorGUIUtility.singleLineHeight + Padding) * lines + 4f;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			position.y += 2f;
			position.height = EditorGUIUtility.singleLineHeight;

			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			var typeProperty = property.FindPropertyRelative(nameof(MotionPositionSwitch.Type));
			EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(MotionPositionSwitch.Name)), new GUIContent("Name"));
			NextLine(ref position);
			EditorGUI.PropertyField(position, typeProperty, new GUIContent("Switch Type"));

			var hasMixedTypes = typeProperty.hasMultipleDifferentValues;
			var type = (MotionPositionSwitchType)typeProperty.enumValueIndex;
			if (hasMixedTypes || type is MotionPositionSwitchType.EnableBetween or MotionPositionSwitchType.PulseBetween) {
				NextLine(ref position);
				DrawRange(position, property);
			}

			if (hasMixedTypes || type is MotionPositionSwitchType.AlwaysPulse or MotionPositionSwitchType.PulseBetween) {
				NextLine(ref position);
				var pulseInterval = property.FindPropertyRelative(nameof(MotionPositionSwitch.PulseInterval));
				EditorGUI.showMixedValue = pulseInterval.hasMultipleDifferentValues;
				EditorGUI.BeginChangeCheck();
				var interval = EditorGUI.Slider(position, new GUIContent("Pulse Every", "Normalized motion controller travel between pulse marks."), pulseInterval.floatValue, 0.001f, 1f);
				if (EditorGUI.EndChangeCheck()) {
					pulseInterval.floatValue = interval;
				}
				EditorGUI.showMixedValue = false;

				NextLine(ref position);
				EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(MotionPositionSwitch.PulseDuration)), new GUIContent("Pulse Duration", "How long each generated pulse remains enabled."));
			}

			EditorGUI.indentLevel = indent;
			EditorGUI.EndProperty();
		}

		private static void DrawRange(Rect position, SerializedProperty property)
		{
			var rangePosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Between", "Inclusive normalized motion controller positions."));
			const float SeparatorWidth = 18f;
			const float SuffixWidth = 34f;
			var fieldWidth = (rangePosition.width - SeparatorWidth - SuffixWidth) / 2f;
			var beginningRect = new Rect(rangePosition.x, rangePosition.y, fieldWidth, rangePosition.height);
			var separatorRect = new Rect(beginningRect.xMax, rangePosition.y, SeparatorWidth, rangePosition.height);
			var endRect = new Rect(separatorRect.xMax, rangePosition.y, fieldWidth, rangePosition.height);
			var suffixRect = new Rect(endRect.xMax + 4f, rangePosition.y, SuffixWidth - 4f, rangePosition.height);

			var beginning = property.FindPropertyRelative(nameof(MotionPositionSwitch.PositionBeginning));
			var end = property.FindPropertyRelative(nameof(MotionPositionSwitch.PositionEnd));
			EditorGUI.showMixedValue = beginning.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var beginningValue = EditorGUI.FloatField(beginningRect, beginning.floatValue);
			if (EditorGUI.EndChangeCheck()) {
				beginning.floatValue = Mathf.Clamp01(beginningValue);
			}
			EditorGUI.LabelField(separatorRect, " – ", EditorStyles.centeredGreyMiniLabel);
			EditorGUI.showMixedValue = end.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var endValue = EditorGUI.FloatField(endRect, end.floatValue);
			if (EditorGUI.EndChangeCheck()) {
				end.floatValue = Mathf.Clamp01(endValue);
			}
			EditorGUI.showMixedValue = false;
			EditorGUI.LabelField(suffixRect, "pos");
		}

		private static void NextLine(ref Rect position) => position.y += EditorGUIUtility.singleLineHeight + Padding;
	}

	[CustomEditor(typeof(MotionControllerComponent)), CanEditMultipleObjects]
	public class MotionControllerInspector : ItemInspector
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
		private SerializedProperty _switchesProperty;
		private float _previewPosition;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();
			_coilModeProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.CoilMode));
			_initialPositionProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.InitialPosition));
			_activationDurationProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.ActivationDuration));
			_releaseDurationProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.ReleaseDuration));
			_activationCurveProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.ActivationCurve));
			_releaseCurveProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.ReleaseCurve));
			_releaseDelayProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.ReleaseDelay));
			_activationThresholdProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.ActivationThreshold));
			_oneShotHoldDurationProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.OneShotHoldDuration));
			_switchesProperty = serializedObject.FindProperty(nameof(MotionControllerComponent.Switches));
		}

		protected override void OnDisable()
		{
			MotionPreview.Restore(targets);
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
			if (!_coilModeProperty.hasMultipleDifferentValues && (MotionCoilMode)_coilModeProperty.enumValueIndex == MotionCoilMode.OneShot) {
				PropertyField(_oneShotHoldDurationProperty);
			}

			if (!_coilModeProperty.hasMultipleDifferentValues && (MotionCoilMode)_coilModeProperty.enumValueIndex == MotionCoilMode.FollowValue) {
				EditorGUILayout.HelpBox("Follow Value requires a plain coil mapping. Wire and dynamic-wire paths are boolean and cannot preserve proportional duty-cycle values.", MessageType.Info);
			} else {
				EditorGUILayout.HelpBox("Binary modes treat every normalized value above the threshold as one energized state. Coil strength controls electrical power, not mechanism position.", MessageType.Info);
			}

			EditorGUILayout.Space(8f);
			PropertyField(_switchesProperty, "Position Switches");
			EditorGUILayout.HelpBox("Maintained switches follow the motion controller's actual normalized position. Pulse switches emit distinct close/open edges using their configured pulse duration.", MessageType.Info);

			if (!Application.isPlaying) {
				EditorGUILayout.Space(8f);
				EditorGUILayout.LabelField("Animation Preview", EditorStyles.boldLabel);
				if (!MotionPreview.HasPreview(targets)) {
					_previewPosition = 0f;
				}

				EditorGUI.BeginChangeCheck();
				var previewPosition = EditorGUILayout.Slider(new GUIContent("Preview Position", "Scrub all Motion Transform followers without entering Play Mode. Previewed transforms are restored before saving, entering Play Mode, reloading scripts, or leaving this inspector."), _previewPosition, 0f, 1f);
				if (EditorGUI.EndChangeCheck()) {
					_previewPosition = previewPosition;
					MotionPreview.Apply(targets, _previewPosition);
				}

				using (new EditorGUI.DisabledScope(!MotionPreview.HasPreview(targets))) {
					if (GUILayout.Button("Reset Preview")) {
						MotionPreview.Restore(targets);
						_previewPosition = 0f;
					}
				}
			}

			base.OnInspectorGUI();
			EndEditing();
		}
	}

	[InitializeOnLoad]
	internal static class MotionPreview
	{
		private sealed class PreviewRecord
		{
			internal MotionTransformComponent Follower;
			internal Vector3 LocalPosition;
			internal Quaternion LocalRotation;
			internal float Position;
		}

		private static readonly Dictionary<MotionControllerComponent, List<PreviewRecord>> Records = new();

		static MotionPreview()
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
				if (inspectedTarget is MotionControllerComponent motionController && Records.ContainsKey(motionController)) {
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

			MotionTransformComponent[] followers = null;
			foreach (var inspectedTarget in inspectedTargets) {
				if (!(inspectedTarget is MotionControllerComponent motionController)) {
					continue;
				}

				if (!Records.TryGetValue(motionController, out var records)) {
					followers ??= Object.FindObjectsByType<MotionTransformComponent>(FindObjectsInactive.Include);
					records = new List<PreviewRecord>();
					foreach (var follower in followers) {
						if (!CanPreview(follower) || !Follows(follower, motionController)) {
							continue;
						}

						records.Add(new PreviewRecord {
							Follower = follower,
							LocalPosition = follower.transform.localPosition,
							LocalRotation = follower.transform.localRotation,
						});
					}

					if (records.Count > 0) {
						Records[motionController] = records;
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
				if (inspectedTarget is MotionControllerComponent motionController) {
					Restore(motionController);
				}
			}
			Repaint();
		}

		private static bool Follows(MotionTransformComponent follower, MotionControllerComponent motionController)
		{
			if (follower._emitter is IAnimationValueEmitter<float> assignedEmitter) {
				return ReferenceEquals(assignedEmitter, motionController);
			}

			foreach (var emitter in follower.GetComponentsInParent<IAnimationValueEmitter>()) {
				if (emitter is IAnimationValueEmitter<float>) {
					return ReferenceEquals(emitter, motionController);
				}
			}
			return false;
		}

		private static bool CanPreview(MotionTransformComponent follower)
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
			var factor = follower.EvaluateFactor(position);
			if (follower.AnimatePosition) {
				if (follower.TranslationSpace == MotionTranslationSpace.World) {
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
					if (follower == null || !follower.AnimatePosition || follower.TranslationSpace != MotionTranslationSpace.World) {
						continue;
					}

					var factor = follower.EvaluateFactor(record.Position);
					changed |= ApplyWorldPosition(record, factor);
				}
			}

			if (changed) {
				Repaint();
			}
		}

		private static void Restore(MotionControllerComponent motionController)
		{
			if (!Records.TryGetValue(motionController, out var records)) {
				return;
			}

			Restore(records);
			Records.Remove(motionController);
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
