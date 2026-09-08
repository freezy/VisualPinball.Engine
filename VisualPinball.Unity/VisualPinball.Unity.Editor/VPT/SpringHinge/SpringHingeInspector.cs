// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpringHingeComponent)), CanEditMultipleObjects]
	public class SpringHingeInspector : ItemInspector
	{
		private SerializedProperty _axis;
		private SerializedProperty _centreOfMass;
		private SerializedProperty _toyMass;
		private SerializedProperty _overrideInertia;
		private SerializedProperty _manualInertia;
		private SerializedProperty _massBoxHalfExtents;
		private SerializedProperty _springStiffness;
		private SerializedProperty _springDamping;
		private SerializedProperty _equilibriumAngle;
		private SerializedProperty _minimumAngle;
		private SerializedProperty _maximumAngle;
		private SerializedProperty _initialAngle;
		private SerializedProperty _enableAngleSwitch;
		private SerializedProperty _switchCloseAngle;
		private SerializedProperty _switchOpenAngle;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();
			_axis = serializedObject.FindProperty(nameof(SpringHingeComponent.HingeAxis));
			_centreOfMass = serializedObject.FindProperty(nameof(SpringHingeComponent.CentreOfMass));
			_toyMass = serializedObject.FindProperty(nameof(SpringHingeComponent.ToyMass));
			_overrideInertia = serializedObject.FindProperty(nameof(SpringHingeComponent.OverrideInertia));
			_manualInertia = serializedObject.FindProperty(nameof(SpringHingeComponent.ManualInertia));
			_massBoxHalfExtents = serializedObject.FindProperty(nameof(SpringHingeComponent.MassBoxHalfExtents));
			_springStiffness = serializedObject.FindProperty(nameof(SpringHingeComponent.SpringStiffness));
			_springDamping = serializedObject.FindProperty(nameof(SpringHingeComponent.SpringDamping));
			_equilibriumAngle = serializedObject.FindProperty(nameof(SpringHingeComponent.EquilibriumAngle));
			_minimumAngle = serializedObject.FindProperty(nameof(SpringHingeComponent.MinimumAngle));
			_maximumAngle = serializedObject.FindProperty(nameof(SpringHingeComponent.MaximumAngle));
			_initialAngle = serializedObject.FindProperty(nameof(SpringHingeComponent.InitialAngle));
			_enableAngleSwitch = serializedObject.FindProperty(nameof(SpringHingeComponent.EnableAngleSwitch));
			_switchCloseAngle = serializedObject.FindProperty(nameof(SpringHingeComponent.SwitchCloseAngle));
			_switchOpenAngle = serializedObject.FindProperty(nameof(SpringHingeComponent.SwitchOpenAngle));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();
			EditorGUILayout.LabelField("Pivot and Mass", EditorStyles.boldLabel);
			PropertyField(_axis);
			PropertyField(_centreOfMass);
			PropertyField(_toyMass, "Toy Mass (ball-relative)");
			PropertyField(_overrideInertia);
			if (_overrideInertia.hasMultipleDifferentValues || _overrideInertia.boolValue) {
				PropertyField(_manualInertia);
			} else {
				PropertyField(_massBoxHalfExtents);
			}

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Spring and Stops", EditorStyles.boldLabel);
			PropertyField(_springStiffness);
			PropertyField(_springDamping);
			PropertyField(_equilibriumAngle);
			PropertyField(_minimumAngle);
			PropertyField(_maximumAngle);
			PropertyField(_initialAngle);

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Angle Switch", EditorStyles.boldLabel);
			PropertyField(_enableAngleSwitch);
			if (_enableAngleSwitch.hasMultipleDifferentValues || _enableAngleSwitch.boolValue) {
				PropertyField(_switchCloseAngle);
				PropertyField(_switchOpenAngle);
			}
			EndEditing();

			if (targets.Length == 1) {
				DrawSetupActions((SpringHingeComponent)target);
			}
			EditorGUILayout.HelpBox("The analytic proxy supports the spring hinge, balls, and passive surfaces. An attached ball releases before unsupported active mechanisms such as flippers and bumpers act on it.", MessageType.Info);
		}

		private static void DrawSetupActions(SpringHingeComponent hinge)
		{
			var proxy = hinge.GetComponent<SpringHingeColliderComponent>();
			if (!proxy) {
				if (GUILayout.Button("Add Analytic Box Proxy")) {
					Undo.AddComponent<SpringHingeColliderComponent>(hinge.gameObject);
				}
				return;
			}

			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button("Fit From Renderers")) {
					Undo.RecordObjects(new Object[] { hinge, proxy }, "Fit Spring Hinge Visual Bounds");
					if (!SpringHingeAuthoring.FitFromVisuals(hinge, proxy)) {
						Debug.LogWarning($"Spring hinge '{hinge.name}' has no renderers to fit.", hinge);
					}
					EditorUtility.SetDirty(hinge);
					EditorUtility.SetDirty(proxy);
				}
				if (GUILayout.Button("Apply Bash Preset")) {
					var magnet = hinge.GetComponentInChildren<MagnetComponent>(true);
					var objects = magnet ? new Object[] { hinge, proxy, magnet } : new Object[] { hinge, proxy };
					Undo.RecordObjects(objects, "Apply Spring Hinge Bash Preset");
					SpringHingeAuthoring.ApplyBashPreset(hinge, proxy, magnet);
					foreach (var changed in objects) {
						EditorUtility.SetDirty(changed);
					}
				}
			}

			foreach (var issue in SpringHingeAuthoring.Validate(hinge, proxy)) {
				EditorGUILayout.HelpBox(issue, MessageType.Error);
			}
		}

		private void OnSceneGUI()
		{
			if (target is not SpringHingeComponent hinge) {
				return;
			}
			var pivot = hinge.transform.position;
			var localAxis = hinge.HingeAxis.sqrMagnitude > 1e-8f ? hinge.HingeAxis.normalized : Vector3.right;
			var worldAxis = hinge.transform.TransformDirection(localAxis).normalized;
			var localReference = Vector3.Cross(localAxis, Vector3.forward);
			if (localReference.sqrMagnitude < 1e-6f) {
				localReference = Vector3.Cross(localAxis, Vector3.up);
			}
			var worldReference = hinge.transform.TransformDirection(localReference.normalized).normalized;
			var radius = HandleUtility.GetHandleSize(pivot) * 0.3f;

			Handles.color = Color.cyan;
			Handles.DrawLine(pivot - worldAxis * radius, pivot + worldAxis * radius, 3f);
			Handles.ArrowHandleCap(0, pivot, Quaternion.LookRotation(worldAxis), radius, EventType.Repaint);
			Handles.color = new Color(1f, 0.65f, 0.1f, 0.9f);
			var start = Quaternion.AngleAxis(hinge.MinimumAngle, worldAxis) * worldReference;
			Handles.DrawWireArc(pivot, worldAxis, start,
				hinge.MaximumAngle - hinge.MinimumAngle, radius);

			var centreWorld = hinge.transform.TransformPoint(hinge.CentreOfMass * 0.001f);
			EditorGUI.BeginChangeCheck();
			var movedCentre = Handles.PositionHandle(centreWorld, hinge.transform.rotation);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(hinge, "Move Spring Hinge Centre of Mass");
				hinge.CentreOfMass = hinge.transform.InverseTransformPoint(movedCentre) * 1000f;
				EditorUtility.SetDirty(hinge);
			}
			Handles.color = Color.yellow;
			Handles.SphereHandleCap(0, centreWorld, Quaternion.identity, radius * 0.12f, EventType.Repaint);
		}
	}
}
