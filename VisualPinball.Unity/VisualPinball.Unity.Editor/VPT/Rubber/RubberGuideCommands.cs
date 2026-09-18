// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public static class RubberGuideCommands
	{
		private const string MenuRoot = "GameObject/Pinball/Rubber/";

		[MenuItem(MenuRoot + "Create Rubber Around Selected Guides", false, 40)]
		internal static void CreateRubberAroundSelectedGuides()
		{
			var guides = SelectedGuides();
			if (!RubberGuideSlotPropagationWindow.TryPick(guides, out var slotName)) {
				return;
			}
			CreateRubberAroundGuides(guides, slotName);
		}

		public static void CreateRubberAroundGuides(IReadOnlyList<RubberGuideComponent> guides,
			string slotName)
		{
			if (guides == null || guides.Count < 2) {
				return;
			}
			var prefab = Resources.Load<GameObject>("Prefabs/Rubber");
			if (!prefab) {
				EditorUtility.DisplayDialog("Cannot Create Rubber", "The Resources/Prefabs/Rubber prefab is missing.", "OK");
				return;
			}
			Undo.IncrementCurrentGroup();
			var undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Create Guided Rubber");
			if (!TryPropagateSlot(guides, slotName, "Propagate Rubber Guide Slot",
				out var bindings, out var propagationError)) {
				Undo.RevertAllInCurrentGroup();
				EditorUtility.DisplayDialog("Cannot Create Rubber", propagationError, "OK");
				return;
			}
			var firstSlot = bindings[0].Guide.Slots.First(slot => slot.Id == bindings[0].SlotId);

			var parent = bindings[0].Guide.transform.parent;
			var gameObject = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
			if (!gameObject) {
				Undo.RevertAllInCurrentGroup();
				return;
			}
			Undo.RegisterCreatedObjectUndo(gameObject, "Create Guided Rubber");
			gameObject.name = GameObjectUtility.GetUniqueNameForSibling(parent, "Rubber");
			var normal = bindings[0].Guide.transform.localToWorldMatrix.MultiplyVector(Vector3.up).normalized;
			var axisX = Vector3.ProjectOnPlane(
				bindings[0].Guide.transform.localToWorldMatrix.MultiplyVector(Vector3.right), normal).normalized;
			var axisY = Vector3.Cross(normal, axisX).normalized;
			gameObject.transform.SetPositionAndRotation(
				RubberGuideResolver.SlotWorldCenter(bindings[0].Guide, firstSlot),
				Quaternion.LookRotation(-axisY, normal));

			var rubber = gameObject.GetComponent<RubberComponent>();
			if (!rubber) {
				Undo.RevertAllInCurrentGroup();
				EditorUtility.DisplayDialog("Cannot Create Rubber", "The rubber prefab has no RubberComponent.", "OK");
				return;
			}
			if (!TryEnsureRenderer(gameObject, out var renderer, out var rendererError)) {
				Undo.RevertAllInCurrentGroup();
				EditorUtility.DisplayDialog("Cannot Create Rubber", rendererError, "OK");
				return;
			}
			renderer.enabled = true;
			rubber.SetGuideBindings(bindings);
			var collider = rubber.GetComponent<RubberColliderComponent>();
			if (collider) {
				collider.Mode = RubberColliderMode.Legacy;
			}
			if (!TryBakeAligned(rubber, "Create Guided Rubber", false,
				out _, out var error)) {
				Undo.RevertAllInCurrentGroup();
				EditorUtility.DisplayDialog("Cannot Create Rubber", error, "OK");
				return;
			}
			PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
			if (collider) {
				PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
			}
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
			Selection.activeGameObject = gameObject;
			Undo.CollapseUndoOperations(undoGroup);
			RubberGuideDependencyTracker.RebuildSoon();
		}

		[MenuItem(MenuRoot + "Create Rubber Around Selected Guides", true)]
		internal static bool CanCreateRubberAroundSelectedGuides()
		{
			var guides = SelectedGuides();
			return guides.Count > 1 && GetPropagatableSlotNames(guides).Length > 0;
		}

		[MenuItem(MenuRoot + "Bind Selected Guides", false, 41)]
		internal static void BindSelectedGuides()
		{
			BindSelectedGuidesToRubber("Bind Rubber To Guides");
		}

		[MenuItem(MenuRoot + "Bind Selected Guides", true)]
		internal static bool CanBindSelectedGuides()
		{
			return SelectedRubber() && SelectedGuides().Count > 0;
		}

		[MenuItem(MenuRoot + "Autofit Now", false, 42)]
		internal static void AutofitSelected()
		{
			Autofit(SelectedRubber(), "Autofit Rubber");
		}

		[MenuItem(MenuRoot + "Autofit Now", true)]
		internal static bool CanAutofitSelected()
		{
			var rubber = SelectedRubber();
			return rubber && rubber.PathSource == RubberPathSource.Guides
				&& rubber.GuideBindings.Count > 0;
		}

		[MenuItem(MenuRoot + "Detach From Guides", false, 43)]
		internal static void DetachSelected()
		{
			var rubber = SelectedRubber();
			Undo.RegisterFullObjectHierarchyUndo(rubber.gameObject, "Detach Rubber From Guides");
			rubber.DetachFromGuides();
			rubber.RebuildMeshes();
			EditorUtility.SetDirty(rubber);
			RubberGuideDependencyTracker.RebuildSoon();
		}

		[MenuItem(MenuRoot + "Detach From Guides", true)]
		internal static bool CanDetachSelected()
		{
			var rubber = SelectedRubber();
			return rubber && rubber.PathSource == RubberPathSource.Guides;
		}

		[MenuItem(MenuRoot + "Convert Imported Rubber To Guided", false, 44)]
		internal static void ConvertImportedRubberToGuided()
		{
			var rubber = SelectedRubber();
			if (!rubber || !RubberGuideSlotPickerWindow.TryPick(SelectedGuides(),
				out var bindings)) {
				return;
			}
			Undo.RegisterFullObjectHierarchyUndo(rubber.gameObject,
				"Convert Imported Rubber To Guided");
			if (!TryApplyBindingsAligned(rubber, bindings, true, out var error)) {
				EditorUtility.DisplayDialog("Rubber Conversion Failed",
					$"The manual spline remains authoritative.\n\n{error}", "OK");
			} else {
				PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
				PrefabUtility.RecordPrefabInstancePropertyModifications(rubber.transform);
			}
			EditorUtility.SetDirty(rubber);
			RubberGuideDependencyTracker.RebuildSoon();
		}

		[MenuItem(MenuRoot + "Convert Imported Rubber To Guided", true)]
		internal static bool CanConvertImportedRubberToGuided()
		{
			var rubber = SelectedRubber();
			return rubber && rubber.PathSource == RubberPathSource.Spline && SelectedGuides().Count > 0;
		}

		internal static bool Autofit(RubberComponent rubber, string undoName)
		{
			if (!rubber) {
				return false;
			}
			Undo.RegisterFullObjectHierarchyUndo(rubber.gameObject, undoName);
			if (!TryBakeAligned(rubber, undoName, false, out _, out var error)) {
				EditorUtility.DisplayDialog("Rubber Autofit Failed", error, "OK");
				return false;
			}
			EditorUtility.SetDirty(rubber);
			PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
			SceneView.RepaintAll();
			return true;
		}

		internal static bool TryBakeAligned(RubberComponent rubber, string undoName,
			bool recordTransformUndo, out RubberAutofitResult fit, out string error)
		{
			var previousPosition = rubber ? rubber.transform.position : Vector3.zero;
			var positionChanged = false;
			if (RubberGuideResolver.TryGetGuidePlaneAlignedPosition(rubber,
				out var alignedPosition)
				&& (alignedPosition - previousPosition).sqrMagnitude > 0f) {
				if (recordTransformUndo) {
					Undo.RecordObject(rubber.transform, undoName);
				}
				rubber.transform.position = alignedPosition;
				positionChanged = true;
			}

			if (!RubberAutofit.TryBake(rubber, out fit, out error)) {
				if (positionChanged) {
					rubber.transform.position = previousPosition;
				}
				return false;
			}
			if (positionChanged) {
				EditorUtility.SetDirty(rubber.transform);
				PrefabUtility.RecordPrefabInstancePropertyModifications(rubber.transform);
			}
			// The deformation bake is derived from the path, so a path rebake also
			// re-bakes it. It is a no-op unless deformation is switched on.
			RubberDeformationBaker.RebakeIfEnabled(rubber);
			return true;
		}

		private static bool TryApplyBindingsAligned(RubberComponent rubber,
			IReadOnlyList<RubberGuideBinding> bindings, bool convertingSpline,
			out string error)
		{
			var previousPosition = rubber.transform.position;
			if (bindings.Count > 0
				&& RubberGuideResolver.TryGetGuidePlaneAlignedPosition(rubber, bindings[0],
					out var alignedPosition)
				&& (alignedPosition - previousPosition).sqrMagnitude > 0f) {
				rubber.transform.position = alignedPosition;
			}

			var succeeded = convertingSpline
				? RubberAutofit.TryConvertToGuides(rubber, bindings, out _, out error)
				: RubberAutofit.TryReplaceGuideBindings(rubber, bindings, out _, out error);
			if (!succeeded) {
				rubber.transform.position = previousPosition;
				return false;
			}
			if ((rubber.transform.position - previousPosition).sqrMagnitude > 0f) {
				EditorUtility.SetDirty(rubber.transform);
				PrefabUtility.RecordPrefabInstancePropertyModifications(rubber.transform);
			}
			return true;
		}

		internal static void EnsureDefaultSlots(IReadOnlyList<RubberGuideComponent> guides,
			string undoName)
		{
			if (guides == null) {
				return;
			}
			var missing = guides.Where(guide => guide && guide.Slots.Length == 0).ToArray();
			if (missing.Length == 0) {
				return;
			}
			Undo.RecordObjects(missing, undoName);
			foreach (var guide in missing) {
				guide.EnsureDefaultSlot();
				EditorUtility.SetDirty(guide);
				PrefabUtility.RecordPrefabInstancePropertyModifications(guide);
			}
		}

		public static string[] GetPropagatableSlotNames(
			IReadOnlyList<RubberGuideComponent> guides)
		{
			if (guides == null) {
				return Array.Empty<string>();
			}
			return guides.Where(guide => guide)
				.SelectMany(guide => guide.Slots)
				.Where(slot => IsConfiguredSlot(slot))
				.Select(slot => slot.DisplayName ?? string.Empty)
				.Distinct(StringComparer.Ordinal)
				.ToArray();
		}

		public static bool TryPropagateSlot(IReadOnlyList<RubberGuideComponent> guides,
			string slotName, string undoName, out RubberGuideBinding[] bindings, out string error)
		{
			bindings = Array.Empty<RubberGuideBinding>();
			error = null;
			if (guides == null || guides.Count == 0 || guides.Any(guide => !guide)) {
				error = "At least one valid rubber guide is required.";
				return false;
			}

			slotName ??= string.Empty;
			var sourceHeights = guides.SelectMany(guide => guide.Slots)
				.Where(slot => SlotNameEquals(slot, slotName) && IsConfiguredSlot(slot))
				.Select(slot => (double)slot.LocalHeight)
				.ToArray();
			if (sourceHeights.Length == 0) {
				error = $"Slot '{SlotLabel(slotName)}' needs a non-zero height on at least one selected guide.";
				return false;
			}
			var propagatedHeight = (float)sourceHeights.Average();
			if (!float.IsFinite(propagatedHeight) || propagatedHeight == 0f) {
				error = $"Slot '{SlotLabel(slotName)}' has a zero average height and cannot be propagated.";
				return false;
			}
			Undo.RecordObjects(guides.ToArray(), undoName);
			bindings = new RubberGuideBinding[guides.Count];
			for (var guideIndex = 0; guideIndex < guides.Count; guideIndex++) {
				var guide = guides[guideIndex];
				var slots = guide.Slots.ToArray();
				var slotIndex = Array.FindIndex(slots,
					slot => SlotNameEquals(slot, slotName) && IsConfiguredSlot(slot));
				if (slotIndex < 0) {
					slotIndex = Array.FindIndex(slots,
						slot => SlotNameEquals(slot, slotName) && slot.LocalHeight == 0f);
					if (slotIndex < 0) {
						slotIndex = Array.FindIndex(slots, slot => slot.LocalHeight == 0f);
					}
					if (slotIndex >= 0) {
						slots[slotIndex].DisplayName = slotName;
						slots[slotIndex].LocalHeight = propagatedHeight;
					} else {
						var slot = RubberGuideSlot.Create(slotName,
							RubberGuideComponent.DefaultSlotRadius);
						slot.LocalHeight = propagatedHeight;
						slots = slots.Concat(new[] { slot }).ToArray();
						slotIndex = slots.Length - 1;
					}
				}
				guide.Slots = slots;
				EditorUtility.SetDirty(guide);
				PrefabUtility.RecordPrefabInstancePropertyModifications(guide);
				bindings[guideIndex] = new RubberGuideBinding(guide, slots[slotIndex].Id);
			}
			return true;
		}

		public static bool TrySetSlotLocalHeight(IReadOnlyList<RubberGuideComponent> guides,
			string slotName, float localHeight, out string error)
		{
			return TrySetSlotValue(guides, slotName, "Set Rubber Guide Slot Height",
				slot => {
					slot.LocalHeight = localHeight;
					return slot;
				}, out error);
		}

		public static bool TrySetSlotLocalCenter(IReadOnlyList<RubberGuideComponent> guides,
			string slotName, Vector2 localCenter, out string error)
		{
			return TrySetSlotValue(guides, slotName, "Set Rubber Guide Slot Center",
				slot => {
					var profile = slot.Profile;
					profile.LocalCenter = new global::Unity.Mathematics.float2(localCenter.x,
						localCenter.y);
					slot.Profile = profile;
					return slot;
				}, out error);
		}

		public static bool TrySetSlotRadius(IReadOnlyList<RubberGuideComponent> guides,
			string slotName, float radius, out string error)
		{
			return TrySetSlotValue(guides, slotName, "Set Rubber Guide Slot Radius",
				slot => {
					var profile = slot.Profile;
					profile.Radius = radius;
					slot.Profile = profile;
					return slot;
				}, out error);
		}

		public static bool TrySetSlotSupportFriction(
			IReadOnlyList<RubberGuideComponent> guides, string slotName, float friction,
			out string error)
		{
			return TrySetSlotValue(guides, slotName, "Set Rubber Guide Slot Friction",
				slot => {
					slot.RubberSupportFriction = friction;
					return slot;
				}, out error);
		}

		private static bool TrySetSlotValue(IReadOnlyList<RubberGuideComponent> guides,
			string slotName, string undoName, Func<RubberGuideSlot, RubberGuideSlot> update,
			out string error)
		{
			slotName ??= string.Empty;
			Undo.IncrementCurrentGroup();
			var undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName(undoName);
			if (!TryPropagateSlot(guides, slotName, undoName, out _, out error)) {
				Undo.RevertAllInCurrentGroup();
				return false;
			}

			foreach (var guide in guides) {
				var slots = guide.Slots.ToArray();
				for (var i = 0; i < slots.Length; i++) {
					if (SlotNameEquals(slots[i], slotName) && IsConfiguredSlot(slots[i])) {
						slots[i] = update(slots[i]);
					}
				}
				guide.Slots = slots;
				EditorUtility.SetDirty(guide);
				PrefabUtility.RecordPrefabInstancePropertyModifications(guide);
			}
			Undo.CollapseUndoOperations(undoGroup);
			RubberGuideDependencyTracker.GuidesChanged(guides);
			return true;
		}

		private static bool TryEnsureRenderer(GameObject gameObject,
			out MeshRenderer renderer, out string error)
		{
			var mesh = gameObject.GetComponent<RubberMeshComponent>();
			if (mesh) {
				return mesh.TryEnsureRenderer(out renderer, out error);
			}
			renderer = null;
			error = "The rubber prefab has no RubberMeshComponent.";
			return false;
		}

		private static bool IsConfiguredSlot(RubberGuideSlot slot)
		{
			return float.IsFinite(slot.LocalHeight) && slot.LocalHeight != 0f;
		}

		private static bool SlotNameEquals(RubberGuideSlot slot, string slotName)
		{
			return string.Equals(slot.DisplayName ?? string.Empty, slotName,
				StringComparison.Ordinal);
		}

		internal static string SlotLabel(string slotName)
		{
			return string.IsNullOrEmpty(slotName) ? "(Unnamed)" : slotName;
		}

		private static void BindSelectedGuidesToRubber(string undoName)
		{
			var rubber = SelectedRubber();
			if (!rubber || !RubberGuideSlotPickerWindow.TryPick(SelectedGuides(), out var bindings)) {
				return;
			}
			Undo.IncrementCurrentGroup();
			Undo.RegisterFullObjectHierarchyUndo(rubber.gameObject, undoName);
			var convertingSpline = rubber.PathSource == RubberPathSource.Spline;
			string error;
			var succeeded = TryApplyBindingsAligned(rubber, bindings, convertingSpline,
				out error);
			if (!succeeded) {
				Undo.RevertAllInCurrentGroup();
				var retainedState = convertingSpline
					? "The manual spline remains authoritative."
					: "The bindings were preserved and the previous sampled path was retained.";
				EditorUtility.DisplayDialog("Rubber Autofit Failed",
					$"{retainedState}\n\n{error}", "OK");
			} else {
				PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
				PrefabUtility.RecordPrefabInstancePropertyModifications(rubber.transform);
			}
			EditorUtility.SetDirty(rubber);
			RubberGuideDependencyTracker.RebuildSoon();
		}

		private static RubberComponent SelectedRubber()
		{
			if (Selection.activeGameObject) {
				var active = Selection.activeGameObject.GetComponentInParent<RubberComponent>();
				if (active) {
					return active;
				}
			}
			return Selection.gameObjects.Select(gameObject => gameObject.GetComponentInParent<RubberComponent>())
				.FirstOrDefault(rubber => rubber);
		}

		private static List<RubberGuideComponent> SelectedGuides()
		{
			var guides = new List<RubberGuideComponent>();
			foreach (var gameObject in Selection.gameObjects) {
				foreach (var guide in gameObject.GetComponents<RubberGuideComponent>()) {
					if (!guides.Contains(guide)) {
						guides.Add(guide);
					}
				}
			}
			return guides;
		}
	}

	internal sealed class RubberGuideSlotPropagationWindow : EditorWindow
	{
		private IReadOnlyList<RubberGuideComponent> _guides;
		private string[] _slotNames;
		private int _slotIndex;
		private bool _confirmed;

		public static bool TryPick(IReadOnlyList<RubberGuideComponent> guides, out string slotName)
		{
			slotName = null;
			var slotNames = RubberGuideCommands.GetPropagatableSlotNames(guides);
			if (guides == null || guides.Count < 2 || slotNames.Length == 0) {
				return false;
			}

			var window = CreateInstance<RubberGuideSlotPropagationWindow>();
			window.titleContent = new GUIContent("Create Guided Rubber");
			window._guides = guides;
			window._slotNames = slotNames;
			window.minSize = new Vector2(360f, 100f);
			window.maxSize = new Vector2(600f, 100f);
			window.ShowModalUtility();
			if (window._confirmed) {
				slotName = window._slotNames[window._slotIndex];
			}
			var confirmed = window._confirmed;
			DestroyImmediate(window);
			return confirmed;
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField(
				$"Propagate one contact slot across {_guides.Count} selected guides.",
				EditorStyles.wordWrappedLabel);
			var labels = _slotNames.Select(RubberGuideCommands.SlotLabel).ToArray();
			_slotIndex = EditorGUILayout.Popup("Contact Slot", _slotIndex, labels);
			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Cancel")) {
				Close();
			}
			if (GUILayout.Button("Create Rubber")) {
				_confirmed = true;
				Close();
			}
			EditorGUILayout.EndHorizontal();
		}
	}

	internal sealed class RubberGuideSlotPickerWindow : EditorWindow
	{
		private IReadOnlyList<RubberGuideComponent> _guides;
		private int[] _slotIndices;
		private bool _confirmed;

		public static bool TryPick(IReadOnlyList<RubberGuideComponent> guides,
			out RubberGuideBinding[] bindings)
		{
			bindings = Array.Empty<RubberGuideBinding>();
			if (guides == null || guides.Count == 0) {
				return false;
			}
			if (guides.Any(guide => !guide)) {
				EditorUtility.DisplayDialog("Cannot Bind Rubber", "A selected rubber guide is missing.", "OK");
				return false;
			}
			RubberGuideCommands.EnsureDefaultSlots(guides, "Add Default Rubber Guide Slot");
			if (guides.All(guide => guide.Slots.Length == 1)) {
				bindings = guides.Select(guide => new RubberGuideBinding(guide, guide.Slots[0].Id)).ToArray();
				return true;
			}

			var window = CreateInstance<RubberGuideSlotPickerWindow>();
			window.titleContent = new GUIContent("Choose Rubber Slots");
			window._guides = guides;
			window._slotIndices = new int[guides.Count];
			window.minSize = new Vector2(360f, 90f + guides.Count * 22f);
			window.maxSize = new Vector2(600f, 90f + guides.Count * 22f);
			window.ShowModalUtility();
			if (window._confirmed) {
				bindings = guides.Select((guide, index) => new RubberGuideBinding(guide,
					guide.Slots[window._slotIndices[index]].Id)).ToArray();
			}
			var confirmed = window._confirmed;
			DestroyImmediate(window);
			return confirmed;
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("Choose the contact slot for each selected guide.", EditorStyles.wordWrappedLabel);
			for (var i = 0; i < _guides.Count; i++) {
				var labels = _guides[i].Slots.Select((slot, index) => string.IsNullOrEmpty(slot.DisplayName)
					? $"Slot {index + 1}" : slot.DisplayName).ToArray();
				_slotIndices[i] = EditorGUILayout.Popup(_guides[i].name, _slotIndices[i], labels);
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Cancel")) {
				Close();
			}
			if (GUILayout.Button("Bind")) {
				_confirmed = true;
				Close();
			}
			EditorGUILayout.EndHorizontal();
		}
	}
}
