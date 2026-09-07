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
	[CustomEditor(typeof(SpringHingeColliderComponent)), CanEditMultipleObjects]
	public class SpringHingeColliderInspector : ItemInspector
	{
		private SerializedProperty _localCentre;
		private SerializedProperty _localRotation;
		private SerializedProperty _halfExtents;
		private SerializedProperty _elasticity;
		private SerializedProperty _elasticityFalloff;
		private SerializedProperty _friction;
		private SerializedProperty _hitEvent;
		private SerializedProperty _hitThreshold;
		private SerializedProperty _overwritePhysics;
		private SerializedProperty _physicsMaterial;
		private bool _materialFoldout = true;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();
			_localCentre = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.LocalCentre));
			_localRotation = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.LocalRotation));
			_halfExtents = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.HalfExtents));
			_elasticity = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.Elasticity));
			_elasticityFalloff = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.ElasticityFalloff));
			_friction = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.Friction));
			_hitEvent = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.HitEvent));
			_hitThreshold = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.HitThreshold));
			_overwritePhysics = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.OverwritePhysics));
			_physicsMaterial = serializedObject.FindProperty(nameof(SpringHingeColliderComponent.PhysicsMaterial));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();
			EditorGUILayout.LabelField("Analytic Box", EditorStyles.boldLabel);
			PropertyField(_localCentre, updateColliders: true);
			PropertyField(_localRotation, updateColliders: true);
			PropertyField(_halfExtents, updateColliders: true);
			PropertyField(_hitEvent);
			if (_hitEvent.hasMultipleDifferentValues || _hitEvent.boolValue) {
				PropertyField(_hitThreshold);
			}

			if (_materialFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_materialFoldout, "Physics Material")) {
				using (new EditorGUI.DisabledScope(_overwritePhysics.boolValue)) {
					PropertyField(_physicsMaterial, "Preset", updateColliders: true);
				}
				PropertyField(_overwritePhysics, updateColliders: true);
				using (new EditorGUI.DisabledScope(!_overwritePhysics.boolValue)) {
					PropertyField(_elasticity, updateColliders: true);
					PropertyField(_elasticityFalloff, updateColliders: true);
					PropertyField(_friction, updateColliders: true);
				}
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			EndEditing();
		}

		private void OnSceneGUI()
		{
			if (targets.Length != 1 || target is not SpringHingeColliderComponent proxy) {
				return;
			}
			var hinge = proxy.GetComponent<SpringHingeComponent>();
			if (!hinge) {
				return;
			}

			var centre = hinge.transform.TransformPoint(proxy.LocalCentre * 0.001f);
			var rotation = hinge.transform.rotation * Quaternion.Euler(proxy.LocalRotation);
			var handleSize = HandleUtility.GetHandleSize(centre) * 0.5f;
			EditorGUI.BeginChangeCheck();
			var movedCentre = Handles.PositionHandle(centre, rotation);
			var resized = Handles.ScaleHandle(proxy.HalfExtents * 0.001f, centre, rotation, handleSize);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(proxy, "Edit Spring Hinge Proxy");
				proxy.LocalCentre = hinge.transform.InverseTransformPoint(movedCentre) * 1000f;
				proxy.HalfExtents = Vector3.Max(resized * 1000f, Vector3.one * 0.001f);
				proxy.CollidersDirty = true;
				EditorUtility.SetDirty(proxy);
			}

			var matrix = hinge.transform.localToWorldMatrix
			             * Matrix4x4.TRS(proxy.LocalCentre * 0.001f,
				             Quaternion.Euler(proxy.LocalRotation), Vector3.one);
			using (new Handles.DrawingScope(new Color(0f, 1f, 1f, 0.8f), matrix)) {
				Handles.DrawWireCube(Vector3.zero, proxy.HalfExtents * 0.002f);
			}
			DrawSweep(hinge, proxy, hinge.MinimumAngle, new Color(1f, 0.6f, 0f, 0.35f));
			DrawSweep(hinge, proxy, hinge.MaximumAngle, new Color(1f, 0.6f, 0f, 0.35f));
		}

		private static void DrawSweep(SpringHingeComponent hinge,
			SpringHingeColliderComponent proxy, float angle, Color color)
		{
			var axis = hinge.HingeAxis.sqrMagnitude > 1e-8f
				? hinge.HingeAxis.normalized
				: Vector3.right;
			var rotation = Quaternion.AngleAxis(angle, axis);
			var matrix = hinge.transform.localToWorldMatrix
			             * Matrix4x4.Rotate(rotation)
			             * Matrix4x4.TRS(proxy.LocalCentre * 0.001f,
				             Quaternion.Euler(proxy.LocalRotation), Vector3.one);
			using (new Handles.DrawingScope(color, matrix)) {
				Handles.DrawWireCube(Vector3.zero, proxy.HalfExtents * 0.002f);
			}
		}
	}
}
