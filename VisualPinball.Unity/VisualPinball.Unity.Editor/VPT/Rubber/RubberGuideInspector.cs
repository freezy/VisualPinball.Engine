// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberGuideComponent)), CanEditMultipleObjects]
	public class RubberGuideInspector : UnityEditor.Editor
	{
		private SerializedProperty _slots;

		private void OnEnable()
		{
			_slots = serializedObject.FindProperty("_slots");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			if (targets.Length > 1) {
				EditorGUILayout.HelpBox("Edit rubber guide slots one component at a time.",
					MessageType.Info);
				return;
			}

			var guide = (RubberGuideComponent)target;
			foreach (var validationError in guide.ValidateSlots()) {
				EditorGUILayout.HelpBox(validationError, MessageType.Error);
			}

			for (var i = 0; i < _slots.arraySize; i++) {
				var slot = _slots.GetArrayElementAtIndex(i);
				var displayName = slot.FindPropertyRelative(nameof(RubberGuideSlot.DisplayName));
				slot.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(slot.isExpanded,
					string.IsNullOrEmpty(displayName.stringValue) ? $"Slot {i + 1}" : displayName.stringValue);
				if (slot.isExpanded) {
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField(displayName);
					EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(RubberGuideSlot.LocalHeight)),
						new GUIContent("Local Height", "Distance along the guide axis in Unity units."));
					DrawProfile(slot.FindPropertyRelative(nameof(RubberGuideSlot.Profile)));
					EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(RubberGuideSlot.RubberSupportFriction)));
					using (new EditorGUI.DisabledScope(true)) {
						EditorGUILayout.TextField("ID", ReadGuid(slot.FindPropertyRelative(nameof(RubberGuideSlot.Id))));
					}

					EditorGUILayout.BeginHorizontal();
					if (GUILayout.Button("Duplicate Slot")) {
						serializedObject.ApplyModifiedProperties();
						Undo.RecordObject(guide, "Duplicate Rubber Guide Slot");
						guide.DuplicateSlot(i);
						EditorUtility.SetDirty(guide);
						serializedObject.Update();
					}
					if (GUILayout.Button("Remove Slot")) {
						_slots.DeleteArrayElementAtIndex(i);
						EditorGUI.indentLevel--;
						EditorGUILayout.EndHorizontal();
						EditorGUILayout.EndFoldoutHeaderGroup();
						break;
					}
					EditorGUILayout.EndHorizontal();
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();
			}

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add Slot")) {
				serializedObject.ApplyModifiedProperties();
				Undo.RecordObject(guide, "Add Rubber Guide Slot");
				guide.AddSlot(RubberGuideSlot.Create($"Slot {guide.Slots.Length + 1}", 0.01f));
				EditorUtility.SetDirty(guide);
				serializedObject.Update();
			}
			if (GUILayout.Button("Repair Duplicate IDs")) {
				serializedObject.ApplyModifiedProperties();
				Undo.RecordObject(guide, "Repair Rubber Guide Slot IDs");
				var repaired = guide.RepairDuplicateIds();
				EditorUtility.SetDirty(guide);
				serializedObject.Update();
				Debug.Log($"Repaired {repaired} rubber guide slot ID(s).", guide);
			}
			EditorGUILayout.EndHorizontal();
			serializedObject.ApplyModifiedProperties();
		}

		private static void DrawProfile(SerializedProperty profile)
		{
			var type = profile.FindPropertyRelative(nameof(RubberGuideProfile.Type));
			EditorGUILayout.PropertyField(type);
			EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RubberGuideProfile.LocalCenter)),
				new GUIContent("Local Center", "Profile-plane offset in the guide's local X/Z axes."));
			if ((RubberGuideProfileType)type.enumValueIndex == RubberGuideProfileType.Circle) {
				EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RubberGuideProfile.Radius)),
					new GUIContent("Radius", "Contact radius in Unity units."));
			} else {
				EditorGUILayout.HelpBox("Convex guide profiles are reserved and not supported by autofit yet.",
					MessageType.Warning);
				EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RubberGuideProfile.ConvexHull)), true);
			}
		}

		private static string ReadGuid(SerializedProperty guid)
		{
			var a = guid.FindPropertyRelative("_a").ulongValue;
			var b = guid.FindPropertyRelative("_b").ulongValue;
			return $"{a:x16}{b:x16}";
		}

		[DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
		private static void DrawGuideGizmo(RubberGuideComponent guide, GizmoType gizmoType)
		{
			if (!guide || guide.Slots == null) {
				return;
			}
			var selected = (gizmoType & GizmoType.Selected) != 0;
			Handles.color = selected ? new Color(0.2f, 0.9f, 1f, 1f) : new Color(0.2f, 0.7f, 0.8f, 0.45f);
			foreach (var slot in guide.Slots.Where(slot => slot.Profile.Type == RubberGuideProfileType.Circle
				&& slot.Profile.Radius > 0f)) {
				var center = RubberGuideResolver.SlotWorldCenter(guide, slot);
				var radius = (guide.transform.TransformVector(Vector3.right * slot.Profile.Radius).magnitude
					+ guide.transform.TransformVector(Vector3.forward * slot.Profile.Radius).magnitude) * 0.5f;
				Handles.DrawWireDisc(center, guide.transform.up, radius);
				if (selected) {
					Handles.Label(center, string.IsNullOrEmpty(slot.DisplayName) ? "Rubber slot" : slot.DisplayName);
				}
			}
		}
	}
}
