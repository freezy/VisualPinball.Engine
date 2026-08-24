// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberGuideComponent)), CanEditMultipleObjects]
	public class RubberGuideInspector : UnityEditor.Editor
	{
		private SerializedProperty _slots;
		private int _propagatedSlotIndex;

		private void OnEnable()
		{
			_slots = serializedObject.FindProperty("_slots");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			if (targets.Length > 1) {
				EditorGUILayout.LabelField($"{targets.Length} Rubber Guides Selected",
					EditorStyles.boldLabel);
				DrawRubberActions(targets.OfType<RubberGuideComponent>().ToArray());
				serializedObject.ApplyModifiedProperties();
				return;
			}

			var guide = (RubberGuideComponent)target;
			foreach (var validationError in guide.ValidateSlots()) {
				EditorGUILayout.HelpBox(validationError, MessageType.Error);
			}

			if (_slots.arraySize == 1) {
				DrawSlotFields(_slots.GetArrayElementAtIndex(0), false);
				EditorGUILayout.Space();
				if (GUILayout.Button("Add Another Contact Slot")) {
					DuplicateSlot(guide, 0);
				}
			} else {
				EditorGUILayout.LabelField("Contact Slots", EditorStyles.boldLabel);
				DrawSlotList(guide);
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("Add Contact Slot")) {
					AddSlot(guide);
				}
				if (GUILayout.Button("Repair Duplicate IDs")) {
					RepairDuplicateIds(guide);
				}
				EditorGUILayout.EndHorizontal();
			}
			serializedObject.ApplyModifiedProperties();
		}

		private void DrawSlotList(RubberGuideComponent guide)
		{
			for (var i = 0; i < _slots.arraySize; i++) {
				var slot = _slots.GetArrayElementAtIndex(i);
				var displayName = slot.FindPropertyRelative(nameof(RubberGuideSlot.DisplayName));
				slot.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(slot.isExpanded,
					string.IsNullOrEmpty(displayName.stringValue) ? $"Slot {i + 1}" : displayName.stringValue);
				if (slot.isExpanded) {
					EditorGUI.indentLevel++;
					DrawSlotFields(slot, true);
					EditorGUILayout.BeginHorizontal();
					if (GUILayout.Button("Duplicate Slot")) {
						DuplicateSlot(guide, i);
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
		}

		private static void DrawSlotFields(SerializedProperty slot, bool showIdentity)
		{
			EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(RubberGuideSlot.DisplayName)));
			DrawVpxFloat(slot.FindPropertyRelative(nameof(RubberGuideSlot.LocalHeight)),
				new GUIContent("Local Height", "Distance along the guide axis."), true);
			DrawProfile(slot.FindPropertyRelative(nameof(RubberGuideSlot.Profile)));
			EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(RubberGuideSlot.RubberSupportFriction)),
				new GUIContent("Support Friction"));
			if (showIdentity) {
				using (new EditorGUI.DisabledScope(true)) {
					EditorGUILayout.TextField("ID", ReadGuid(slot.FindPropertyRelative(nameof(RubberGuideSlot.Id))));
				}
			}
		}

		private void AddSlot(RubberGuideComponent guide)
		{
			serializedObject.ApplyModifiedProperties();
			Undo.RecordObject(guide, "Add Rubber Guide Slot");
			guide.AddSlot(RubberGuideSlot.Create($"Slot {guide.Slots.Length + 1}",
				RubberGuideComponent.DefaultSlotRadius));
			EditorUtility.SetDirty(guide);
			serializedObject.Update();
		}

		private void DuplicateSlot(RubberGuideComponent guide, int index)
		{
			serializedObject.ApplyModifiedProperties();
			Undo.RecordObject(guide, "Duplicate Rubber Guide Slot");
			guide.DuplicateSlot(index);
			EditorUtility.SetDirty(guide);
			serializedObject.Update();
		}

		private void RepairDuplicateIds(RubberGuideComponent guide)
		{
			serializedObject.ApplyModifiedProperties();
			Undo.RecordObject(guide, "Repair Rubber Guide Slot IDs");
			var repaired = guide.RepairDuplicateIds();
			EditorUtility.SetDirty(guide);
			serializedObject.Update();
			Debug.Log($"Repaired {repaired} rubber guide slot ID(s).", guide);
		}

		private void DrawRubberActions(IReadOnlyList<RubberGuideComponent> guides)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Rubber Actions", EditorStyles.boldLabel);
			var slotNames = RubberGuideCommands.GetPropagatableSlotNames(guides);
			if (slotNames.Length > 0) {
				_propagatedSlotIndex = Mathf.Clamp(_propagatedSlotIndex, 0, slotNames.Length - 1);
				var labels = slotNames.Select(RubberGuideCommands.SlotLabel).ToArray();
				_propagatedSlotIndex = EditorGUILayout.Popup("Contact Slot",
					_propagatedSlotIndex, labels);
				var slotName = slotNames[_propagatedSlotIndex];
				DrawBatchSlotFields(guides, slotName);
				if (GUILayout.Button("Create Rubber Around Selected Guides")) {
					RubberGuideCommands.CreateRubberAroundGuides(guides, slotName);
					GUIUtility.ExitGUI();
				}
			} else {
				EditorGUILayout.HelpBox(
					"Set a non-zero Local Height on at least one contact slot before creating a rubber.",
					MessageType.Info);
			}
			DrawAction("Bind Selected Guides", RubberGuideCommands.CanBindSelectedGuides,
				RubberGuideCommands.BindSelectedGuides);
			DrawAction("Autofit Now", RubberGuideCommands.CanAutofitSelected,
				RubberGuideCommands.AutofitSelected);
			DrawAction("Detach From Guides", RubberGuideCommands.CanDetachSelected,
				RubberGuideCommands.DetachSelected);
			DrawAction("Convert Imported Rubber To Guided",
				RubberGuideCommands.CanConvertImportedRubberToGuided,
				RubberGuideCommands.ConvertImportedRubberToGuided);
		}

		private static void DrawBatchSlotFields(IReadOnlyList<RubberGuideComponent> guides,
			string slotName)
		{
			var slots = guides.Where(guide => guide)
				.SelectMany(guide => guide.Slots)
				.Where(slot => (slot.DisplayName ?? string.Empty) == (slotName ?? string.Empty)
					&& float.IsFinite(slot.LocalHeight) && slot.LocalHeight != 0f)
				.ToArray();
			if (slots.Length == 0) {
				return;
			}

			EditorGUILayout.Space(4f);
			EditorGUILayout.LabelField("Contact Slot Options", EditorStyles.boldLabel);
			if (DrawBatchVpxFloat(new GUIContent("Local Height", "Distance along the guide axis."),
				slots.Select(slot => slot.LocalHeight).ToArray(), out var localHeight, true)) {
				var succeeded = RubberGuideCommands.TrySetSlotLocalHeight(guides, slotName,
					localHeight, out var error);
				FinishBatchSlotEdit(succeeded, error);
			}
			if (DrawBatchVpxFloat2(new GUIContent("Local Center",
				"Profile-plane offset in the guide's local X/Z axes."),
				slots.Select(slot => new Vector2(slot.Profile.LocalCenter.x,
					slot.Profile.LocalCenter.y)).ToArray(), out var localCenter)) {
				var succeeded = RubberGuideCommands.TrySetSlotLocalCenter(guides, slotName,
					localCenter, out var error);
				FinishBatchSlotEdit(succeeded, error);
			}
			if (DrawBatchVpxFloat(new GUIContent("Radius", "Contact radius."),
				slots.Select(slot => slot.Profile.Radius).ToArray(), out var radius)) {
				var succeeded = RubberGuideCommands.TrySetSlotRadius(guides, slotName,
					radius, out var error);
				FinishBatchSlotEdit(succeeded, error);
			}
			if (DrawBatchFloat(new GUIContent("Support Friction"),
				slots.Select(slot => slot.RubberSupportFriction).ToArray(), out var friction)) {
				var succeeded = RubberGuideCommands.TrySetSlotSupportFriction(guides, slotName,
					friction, out var error);
				FinishBatchSlotEdit(succeeded, error);
			}
			EditorGUILayout.Space(4f);
		}

		private static bool DrawBatchVpxFloat(GUIContent label, IReadOnlyList<float> values,
			out float value, bool delayed = false)
		{
			var first = values[0];
			var previousMixedValue = EditorGUI.showMixedValue;
			EditorGUI.showMixedValue = values.Skip(1).Any(candidate =>
				!Mathf.Approximately(candidate, first));
			EditorGUI.BeginChangeCheck();
			var edited = delayed
				? EditorGUILayout.DelayedFloatField(label, Physics.ScaleToVpx(first))
				: EditorGUILayout.FloatField(label, Physics.ScaleToVpx(first));
			var changed = EditorGUI.EndChangeCheck();
			EditorGUI.showMixedValue = previousMixedValue;
			value = Physics.ScaleToWorld(edited);
			return changed;
		}

		private static bool DrawBatchVpxFloat2(GUIContent label,
			IReadOnlyList<Vector2> values, out Vector2 value)
		{
			var first = values[0];
			var previousMixedValue = EditorGUI.showMixedValue;
			EditorGUI.showMixedValue = values.Skip(1).Any(candidate =>
				!Mathf.Approximately(candidate.x, first.x)
				|| !Mathf.Approximately(candidate.y, first.y));
			EditorGUI.BeginChangeCheck();
			var edited = EditorGUILayout.Vector2Field(label,
				new Vector2(Physics.ScaleToVpx(first.x), Physics.ScaleToVpx(first.y)));
			var changed = EditorGUI.EndChangeCheck();
			EditorGUI.showMixedValue = previousMixedValue;
			value = new Vector2(Physics.ScaleToWorld(edited.x), Physics.ScaleToWorld(edited.y));
			return changed;
		}

		private static bool DrawBatchFloat(GUIContent label, IReadOnlyList<float> values,
			out float value)
		{
			var first = values[0];
			var previousMixedValue = EditorGUI.showMixedValue;
			EditorGUI.showMixedValue = values.Skip(1).Any(candidate =>
				!Mathf.Approximately(candidate, first));
			EditorGUI.BeginChangeCheck();
			value = EditorGUILayout.FloatField(label, first);
			var changed = EditorGUI.EndChangeCheck();
			EditorGUI.showMixedValue = previousMixedValue;
			return changed;
		}

		private static void FinishBatchSlotEdit(bool succeeded, string error)
		{
			if (!succeeded) {
				EditorUtility.DisplayDialog("Cannot Edit Rubber Slots", error, "OK");
				GUIUtility.ExitGUI();
			}
		}

		private static void DrawAction(string label, System.Func<bool> validate,
			System.Action execute)
		{
			using (new EditorGUI.DisabledScope(!validate())) {
				if (GUILayout.Button(label)) {
					execute();
					GUIUtility.ExitGUI();
				}
			}
		}

		private static void DrawProfile(SerializedProperty profile)
		{
			DrawVpxFloat2(profile.FindPropertyRelative(nameof(RubberGuideProfile.LocalCenter)),
				new GUIContent("Local Center", "Profile-plane offset in the guide's local X/Z axes."));
			DrawVpxFloat(profile.FindPropertyRelative(nameof(RubberGuideProfile.Radius)),
				new GUIContent("Radius", "Contact radius."));
		}

		private static void DrawVpxFloat(SerializedProperty property, GUIContent label,
			bool delayed = false)
		{
			EditorGUI.BeginChangeCheck();
			var value = delayed
				? EditorGUILayout.DelayedFloatField(label, Physics.ScaleToVpx(property.floatValue))
				: EditorGUILayout.FloatField(label, Physics.ScaleToVpx(property.floatValue));
			if (EditorGUI.EndChangeCheck()) {
				property.floatValue = Physics.ScaleToWorld(value);
			}
		}

		private static void DrawVpxFloat2(SerializedProperty property, GUIContent label)
		{
			var x = property.FindPropertyRelative("x");
			var y = property.FindPropertyRelative("y");
			var value = new Vector2(Physics.ScaleToVpx(x.floatValue), Physics.ScaleToVpx(y.floatValue));
			EditorGUI.BeginChangeCheck();
			value = EditorGUILayout.Vector2Field(label, value);
			if (EditorGUI.EndChangeCheck()) {
				x.floatValue = Physics.ScaleToWorld(value.x);
				y.floatValue = Physics.ScaleToWorld(value.y);
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
