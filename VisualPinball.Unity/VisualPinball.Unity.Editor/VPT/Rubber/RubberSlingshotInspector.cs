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
		private SerializedProperty _slingshotWall;
		private SerializedProperty _armContactPosition;
		private SerializedProperty _armRestClearance;
		private SerializedProperty _armTipTravel;
		private SerializedProperty _selfWired;
		private SerializedProperty _selfWirePulseMs;
		private SerializedProperty _motionController;

		private float _preview;
		private Transform _arm;

		/// <summary>Every open slingshot inspector, so a package export can stop their previews.</summary>
		private static readonly System.Collections.Generic.HashSet<RubberSlingshotInspector> Open = new();

		/// <summary>
		/// Ends every slingshot preview and sets the open inspectors' sliders back to zero,
		/// so nothing poses the arm or the cord again while a package is written.
		/// </summary>
		public static void StopAllPreviews()
		{
			foreach (var inspector in Open) {
				inspector.StopPreview();
			}
			foreach (var slingshot in Object.FindObjectsByType<RubberSlingshotComponent>(
				FindObjectsInactive.Include, FindObjectsSortMode.None)) {
				slingshot.EndPreview();
			}
		}

		private void OnEnable()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorSceneManager.sceneSaving += OnSceneSaving;
			PrefabStage.prefabSaving += OnPrefabSaving;
			Open.Add(this);
			_span = serializedObject.FindProperty("_span");
			_slingshotWall = serializedObject.FindProperty("_slingshotWall");
			_armContactPosition = serializedObject.FindProperty("_armContactPosition01");
			_armRestClearance = serializedObject.FindProperty("_armRestClearance");
			_armTipTravel = serializedObject.FindProperty("_armTipTravel");
			_selfWired = serializedObject.FindProperty("_selfWired");
			_selfWirePulseMs = serializedObject.FindProperty("_selfWirePulseMs");
			_motionController = serializedObject.FindProperty("_motionController");
		}

		private void OnDisable()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorSceneManager.sceneSaving -= OnSceneSaving;
			PrefabStage.prefabSaving -= OnPrefabSaving;
			Open.Remove(this);
			StopPreview();
		}

		/// <summary>The preview poses scene objects; it must never be carried into a game, a saved scene or a saved prefab.</summary>
		private void OnPlayModeStateChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.ExitingEditMode) {
				StopPreview();
			}
		}

		private void OnSceneSaving(Scene scene, string path) => StopPreview();

		private void OnPrefabSaving(GameObject prefab) => StopPreview();

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
			EditorGUILayout.LabelField("Kick", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_slingshotWall, new GUIContent("Slingshot Wall",
				"The Surface in front of the span whose slingshot edge kicks the ball. Its slingshot event closes this slingshot's switch."));
			serializedObject.ApplyModifiedProperties();
			var component = (RubberSlingshotComponent)target;
			WallGUI(component);
			serializedObject.Update();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Wire", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_selfWired);
			using (new EditorGUI.DisabledScope(!_selfWired.boolValue)) {
				EditorGUILayout.PropertyField(_selfWirePulseMs);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Arm", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_armContactPosition, new GUIContent("Arm Contact Position",
				"Where along the span the arm meets the cord, from 0 at the first post to 1 at the second."));
			EditorGUILayout.PropertyField(_armRestClearance, new GUIContent("Arm Rest Clearance",
				"How far behind the cord the arm's tip rests, in VPX units."));
			EditorGUILayout.PropertyField(_armTipTravel, new GUIContent("Arm Tip Travel",
				"How far the arm's tip travels when the coil fires, in VPX units. The cord is pushed out by the travel minus the clearance."));
			EditorGUILayout.PropertyField(_motionController, new GUIContent("Motion Controller",
				"The Motion Controller that moves the arm. The slingshot's coil fires it; the controller's mode, travel and followers decide how the arm moves, and the cord follows its position."));
			serializedObject.ApplyModifiedProperties();
			ArmSetupGUI(component);

			foreach (var error in component.ValidateConfiguration(includeSceneUniqueness: true)) {
				EditorGUILayout.HelpBox(error, MessageType.Error);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				EditorGUI.BeginChangeCheck();
				var preview = EditorGUILayout.Slider(new GUIContent("Kick",
						"Poses the arm and the cord between rest and the fully fired pose, without a game. Align the arm's contact position and travel against it, then set it back to zero. Nothing of it is saved."),
					_preview, 0f, 1f);
				if (EditorGUI.EndChangeCheck()) {
					_preview = preview;
				}
			}
			if (Application.isPlaying) {
				EditorGUILayout.HelpBox("The coil drives the arm and the cord while the game runs.",
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

		/// <summary>
		/// The slingshot wall: created in front of the span when there is none, and
		/// refitted to it after the rubber, its guides or the span changed.
		/// </summary>
		private static void WallGUI(RubberSlingshotComponent component)
		{
			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				if (GUILayout.Button(component.SlingshotWall ? "Refit Slingshot Wall" : "Create Slingshot Wall")) {
					if (!RubberSlingshotAuthoring.TryFitSlingshotWall(component, out var error)) {
						EditorUtility.DisplayDialog("Cannot fit the slingshot wall", error, "OK");
					}
				}
			}
			EditorGUILayout.HelpBox("The slingshot wall is an invisible Surface in front of the span whose slingshot edge kicks the ball, the way Visual Pinball does it. Its slingshot event closes this slingshot's switch; set the kick under Slingshot Force and Threshold on its collider.",
				MessageType.None);
		}

		/// <summary>
		/// Sets the arm up from its game object, and makes sure the cord has the renderer
		/// that animates it.
		/// </summary>
		private void ArmSetupGUI(RubberSlingshotComponent component)
		{
			var controller = component.MotionController;
			if (!_arm && controller) {
				_arm = controller.transform;
			}
			_arm = (Transform)EditorGUILayout.ObjectField(new GUIContent("Arm",
					"The arm's game object. Its origin must be the arm's pivot below the playfield; the arm swings about it, parallel to the span."),
				_arm, typeof(Transform), true);
			using (new EditorGUI.DisabledScope(!_arm || Application.isPlaying)) {
				if (GUILayout.Button(controller ? "Refit Arm Swing" : "Animate Arm")) {
					if (!RubberSlingshotAuthoring.TrySetUpArm(component, _arm, out var error)) {
						EditorUtility.DisplayDialog("Cannot animate the arm", error, "OK");
					}
				}
			}
			EditorGUILayout.HelpBox(controller
					? "The slingshot's coil fires this Motion Controller and the cord follows its position. Refit the swing after changing the arm's contact position, clearance or travel."
					: "Pick the arm and press Animate Arm: it gets a Motion Controller that the slingshot's coil fires and a Motion Transform that swings it to the arm travel. Without one only the cord animates.",
				MessageType.None);

			if (component.Rubber && !component.DeformationRenderer) {
				EditorGUILayout.HelpBox("The rubber has no Rubber Deformation Renderer, so its cord does not move.",
					MessageType.Warning);
				using (new EditorGUI.DisabledScope(Application.isPlaying)) {
					if (GUILayout.Button("Add Deformation Renderer")) {
						RubberSlingshotAuthoring.EnsureDeformationRenderer(component.Rubber);
					}
				}
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
			inward = span.InwardAt(authoredPosition01);
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
