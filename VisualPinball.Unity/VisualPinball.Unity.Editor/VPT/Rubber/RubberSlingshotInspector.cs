// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberSlingshotComponent))]
	public sealed class RubberSlingshotInspector : UnityEditor.Editor
	{
		private SerializedProperty _span;
		private SerializedProperty _switchZones;
		private SerializedProperty _armContactPosition;
		private SerializedProperty _armRestClearance;
		private SerializedProperty _armTipTravel;
		private SerializedProperty _actuator;
		private SerializedProperty _selfWired;
		private SerializedProperty _wireDelayMs;
		private SerializedProperty _selfWirePulseMs;
		private SerializedProperty _kickSpeed;
		private SerializedProperty _armHalfWidth01;
		private SerializedProperty _kickWindowMs;
		private SerializedProperty _minimumSwitchPulseMs;
		private SerializedProperty _motionController;

		private float _preview;

		private void OnEnable()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorSceneManager.sceneSaving += OnSceneSaving;
			_span = serializedObject.FindProperty("_span");
			_switchZones = serializedObject.FindProperty("_switchZones");
			_armContactPosition = serializedObject.FindProperty("_armContactPosition01");
			_armRestClearance = serializedObject.FindProperty("_armRestClearance");
			_armTipTravel = serializedObject.FindProperty("_armTipTravel");
			_actuator = serializedObject.FindProperty("_actuator");
			_selfWired = serializedObject.FindProperty("_selfWired");
			_wireDelayMs = serializedObject.FindProperty("_wireDelayMs");
			_selfWirePulseMs = serializedObject.FindProperty("_selfWirePulseMs");
			_kickSpeed = serializedObject.FindProperty("_kickSpeed");
			_armHalfWidth01 = serializedObject.FindProperty("_armHalfWidth01");
			_kickWindowMs = serializedObject.FindProperty("_kickWindowMs");
			_minimumSwitchPulseMs = serializedObject.FindProperty("_minimumSwitchPulseMs");
			_motionController = serializedObject.FindProperty("_motionController");
		}

		private void OnDisable()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorSceneManager.sceneSaving -= OnSceneSaving;
			StopPreview();
		}

		/// <summary>The preview poses scene objects; it must never be carried into a game or a saved scene.</summary>
		private void OnPlayModeStateChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.ExitingEditMode) {
				StopPreview();
			}
		}

		private void OnSceneSaving(Scene scene, string path) => StopPreview();

		private void StopPreview()
		{
			_preview = 0f;
			if (target is RubberSlingshotComponent component && component) {
				component.EndPreview();
			}
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUILayout.LabelField("Active Span", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_span);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Switches", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_switchZones, true);
			EditorGUILayout.HelpBox("Open deflection is not consulted by the static rebound model: a zone closes at its close deflection and stays closed for its minimum closed time. The value is kept and packaged for a future deformable model.",
				MessageType.Info);
			EditorGUILayout.PropertyField(_minimumSwitchPulseMs);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Wire", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_selfWired);
			using (new EditorGUI.DisabledScope(!_selfWired.boolValue)) {
				EditorGUILayout.PropertyField(_wireDelayMs);
				EditorGUILayout.PropertyField(_selfWirePulseMs);
				EditorGUILayout.PropertyField(_kickWindowMs);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Kick", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_actuator, new GUIContent("Coil Model"));
			EditorGUILayout.HelpBox("Optional. The coil model is stored and packaged for a future physical arm; this version applies the kick below and does not consult it.",
				MessageType.None);
			EditorGUILayout.PropertyField(_armContactPosition);
			EditorGUILayout.PropertyField(_armRestClearance);
			EditorGUILayout.PropertyField(_armTipTravel);
			EditorGUILayout.PropertyField(_armHalfWidth01);
			EditorGUILayout.PropertyField(_kickSpeed);
			EditorGUILayout.LabelField(" ", $"= {_kickSpeed.floatValue / 18.53f:F2} m/s");

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Arm", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_motionController, new GUIContent("Motion Controller"));
			EditorGUILayout.HelpBox("The slingshot's coil feeds this Motion Controller and the cord follows its position. Give it Motion Transform followers for the arm; One Shot with a short activation and release is a good start. Without a Motion Controller only the cord animates, on the renderer's timed curve.",
				MessageType.None);
			serializedObject.ApplyModifiedProperties();

			if (!RubberSlingshotComponent.RuntimeAvailable) {
				EditorGUILayout.HelpBox("Physical slingshot runtime unavailable. Authoring and package data are preserved, but this component does not register play-mode controls.",
					MessageType.Warning);
			}
			var component = (RubberSlingshotComponent)target;
			var collider = component.Rubber
				? component.Rubber.GetComponent<RubberColliderComponent>()
				: null;
			if (collider) {
				collider.EnsureBakesCurrent();
			}
			foreach (var error in component.ValidateConfiguration(includeSceneUniqueness: true)) {
				EditorGUILayout.HelpBox(error, MessageType.Error);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				EditorGUI.BeginChangeCheck();
				var preview = EditorGUILayout.Slider(new GUIContent("Kick",
						"Poses the arm and the cord between rest and the fully fired pose, without a game. Align Visual Fired Angle and Arm Contact Position against it, then set it back to zero. Nothing of it is saved."),
					_preview, 0f, 1f);
				if (EditorGUI.EndChangeCheck()) {
					_preview = preview;
				}
			}
			if (Application.isPlaying) {
				EditorGUILayout.HelpBox("The simulation drives the arm and the cord while the game runs.",
					MessageType.None);
			} else if (_preview > 0f) {
				// re-posed every repaint, so edits to the angle, the contact position or the
				// travel show while the slider is up
				component.Preview(_preview);
				SceneView.RepaintAll();
			} else if (component.IsPreviewing) {
				component.EndPreview();
				SceneView.RepaintAll();
			}
		}

		private void OnSceneGUI()
		{
			var component = (RubberSlingshotComponent)target;
			if (!component.TryResolveSpan(out var resolved, out _)) {
				return;
			}
			var rubber = component.Rubber;
			var segmentCount = math.max(8, RubberPathMath.FreeSpanSubdivisionCount(
				resolved.Element, RubberAutofit.RenderToleranceVpx));
			var pathPoints = new Vector3[segmentCount + 1];
			for (var i = 0; i <= segmentCount; i++) {
				pathPoints[i] = ToWorld(rubber, resolved.PointAt(i / (float)segmentCount));
			}
			Handles.color = new Color(1f, 0.45f, 0.05f);
			Handles.DrawAAPolyLine(4f, pathPoints);
			foreach (var zone in component.SwitchZones) {
				Evaluate(resolved, zone.Position01, out var authoredPosition,
					out var inward);
				var position = ToWorld(rubber, authoredPosition);
				var openPosition = ToWorld(rubber,
					authoredPosition + inward * zone.OpenDeflection);
				var closePosition = ToWorld(rubber,
					authoredPosition + inward * zone.CloseDeflection);
				var size = HandleUtility.GetHandleSize(position) * 0.045f;
				Handles.SphereHandleCap(0, position, Quaternion.identity, size,
					EventType.Repaint);
				Handles.DrawDottedLine(position, openPosition, 3f);
				Handles.DrawLine(openPosition, closePosition);
				Handles.DrawWireDisc(openPosition, rubber.transform.up, size * 0.6f);
				Handles.DrawWireDisc(closePosition, rubber.transform.up, size * 0.9f);
			}
			Handles.color = Color.cyan;
			Evaluate(resolved, component.ArmContactPosition01, out var armPosition,
				out var armInward);
			var arm = ToWorld(rubber, armPosition);
			var armRest = ToWorld(rubber,
				armPosition + armInward * component.ArmRestClearance);
			var armFired = ToWorld(rubber,
				armPosition - armInward
					* (component.ArmTipTravel - component.ArmRestClearance));
			var armSize = HandleUtility.GetHandleSize(arm) * 0.06f;
			Handles.DrawWireDisc(arm, rubber.transform.up, armSize);
			Handles.DrawDottedLine(arm, armRest, 3f);
			Handles.DrawLine(armRest, armFired);
			Handles.DrawWireDisc(armRest, rubber.transform.up, armSize * 0.75f);
			Handles.DrawWireDisc(armFired, rubber.transform.up, armSize);
		}

		private static void Evaluate(ResolvedRubberSpan span, float authoredPosition01,
			out float2 position, out float2 inward)
		{
			position = span.PointAt(authoredPosition01);
			var tangent = span.TangentAt(authoredPosition01);
			inward = new float2(-tangent.y, tangent.x);
		}

		private static Vector3 ToWorld(RubberComponent rubber,
			float2 point)
		{
			var local = rubber.BakeFrameToLocal.MultiplyPoint3x4(
				new Vector3(point.x, point.y, 0f));
			return local.TranslateToWorld(rubber.transform);
		}
	}
}
