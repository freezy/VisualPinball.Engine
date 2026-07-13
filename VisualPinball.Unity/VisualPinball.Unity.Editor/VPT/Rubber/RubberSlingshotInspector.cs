// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using UnityEditor;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberSlingshotComponent))]
	public sealed class RubberSlingshotInspector : UnityEditor.Editor
	{
		private SerializedProperty _rubber;
		private SerializedProperty _span;
		private SerializedProperty _switchZones;
		private SerializedProperty _armContactPosition;
		private SerializedProperty _armRestClearance;
		private SerializedProperty _armTipTravel;
		private SerializedProperty _actuator;
		private SerializedProperty _coilArmVisual;
		private SerializedProperty _visualRotationAxis;
		private SerializedProperty _visualRestAngle;
		private SerializedProperty _visualFiredAngle;

		private void OnEnable()
		{
			_rubber = serializedObject.FindProperty("_rubber");
			_span = serializedObject.FindProperty("_span");
			_switchZones = serializedObject.FindProperty("_switchZones");
			_armContactPosition = serializedObject.FindProperty("_armContactPosition01");
			_armRestClearance = serializedObject.FindProperty("_armRestClearance");
			_armTipTravel = serializedObject.FindProperty("_armTipTravel");
			_actuator = serializedObject.FindProperty("_actuator");
			_coilArmVisual = serializedObject.FindProperty("_coilArmVisual");
			_visualRotationAxis = serializedObject.FindProperty("_visualRotationAxis");
			_visualRestAngle = serializedObject.FindProperty("_visualRestAngle");
			_visualFiredAngle = serializedObject.FindProperty("_visualFiredAngle");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUILayout.LabelField("Active Span", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_rubber);
			EditorGUILayout.PropertyField(_span);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Switches", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_switchZones, true);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Actuator", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_actuator);
			EditorGUILayout.PropertyField(_armContactPosition);
			EditorGUILayout.PropertyField(_armRestClearance);
			EditorGUILayout.PropertyField(_armTipTravel);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Arm Visual", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(_coilArmVisual);
			EditorGUILayout.PropertyField(_visualRotationAxis);
			EditorGUILayout.PropertyField(_visualRestAngle);
			EditorGUILayout.PropertyField(_visualFiredAngle);
			serializedObject.ApplyModifiedProperties();

			if (!RubberSlingshotComponent.RuntimeAvailable) {
				EditorGUILayout.HelpBox("Physical slingshot runtime unavailable. Authoring and package data are preserved, but this component does not register play-mode controls.",
					MessageType.Warning);
			}
			var component = (RubberSlingshotComponent)target;
			foreach (var error in component.ValidateConfiguration(includeSceneUniqueness: true)) {
				EditorGUILayout.HelpBox(error, MessageType.Error);
			}
		}

		private void OnSceneGUI()
		{
			var component = (RubberSlingshotComponent)target;
			if (!component.TryResolveSpan(out var resolved, out _)) {
				return;
			}
			var rubber = component.Rubber;
			var pathDirection = math.normalizesafe(resolved.Element.End - resolved.Element.Start,
				new float2(1f, 0f));
			var inward = new float2(-pathDirection.y, pathDirection.x);
			var start = resolved.IsReversed ? resolved.Element.End : resolved.Element.Start;
			var end = resolved.IsReversed ? resolved.Element.Start : resolved.Element.End;
			var worldStart = ToWorld(rubber, start);
			var worldEnd = ToWorld(rubber, end);
			Handles.color = new Color(1f, 0.45f, 0.05f);
			Handles.DrawAAPolyLine(4f, worldStart, worldEnd);
			foreach (var zone in component.SwitchZones) {
				var authoredPosition = math.lerp(start, end, zone.Position01);
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
			var armPosition = math.lerp(start, end, component.ArmContactPosition01);
			var arm = ToWorld(rubber, armPosition);
			var armRest = ToWorld(rubber,
				armPosition - inward * component.ArmRestClearance);
			var armFired = ToWorld(rubber,
				armPosition + inward * (component.ArmTipTravel - component.ArmRestClearance));
			var armSize = HandleUtility.GetHandleSize(arm) * 0.06f;
			Handles.DrawWireDisc(arm, rubber.transform.up, armSize);
			Handles.DrawDottedLine(arm, armRest, 3f);
			Handles.DrawLine(armRest, armFired);
			Handles.DrawWireDisc(armRest, rubber.transform.up, armSize * 0.75f);
			Handles.DrawWireDisc(armFired, rubber.transform.up, armSize);
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
