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
		private static void CreateRubberAroundSelectedGuides()
		{
			var guides = SelectedGuides();
			if (!RubberGuideSlotPickerWindow.TryPick(guides, out var bindings)) {
				return;
			}
			var firstSlot = bindings[0].Guide.Slots.First(slot => slot.Id == bindings[0].SlotId);
			var prefab = Resources.Load<GameObject>("Prefabs/Rubber");
			if (!prefab) {
				EditorUtility.DisplayDialog("Cannot Create Rubber", "The Resources/Prefabs/Rubber prefab is missing.", "OK");
				return;
			}

			var parent = bindings[0].Guide.transform.parent;
			var gameObject = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
			if (!gameObject) {
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
				Undo.DestroyObjectImmediate(gameObject);
				EditorUtility.DisplayDialog("Cannot Create Rubber", "The rubber prefab has no RubberComponent.", "OK");
				return;
			}
			rubber.SetGuideBindings(bindings);
			var collider = rubber.GetComponent<RubberColliderComponent>();
			if (collider) {
				collider.Mode = RubberColliderMode.Legacy;
			}
			if (!RubberAutofit.TryBake(rubber, out _, out var error)) {
				Undo.DestroyObjectImmediate(gameObject);
				EditorUtility.DisplayDialog("Cannot Create Rubber", error, "OK");
				return;
			}
			PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
			if (collider) {
				PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
			}
			Selection.activeGameObject = gameObject;
			RubberGuideDependencyTracker.RebuildSoon();
		}

		[MenuItem(MenuRoot + "Create Rubber Around Selected Guides", true)]
		private static bool CanCreateRubberAroundSelectedGuides()
		{
			return SelectedGuides().Count > 0;
		}

		[MenuItem(MenuRoot + "Bind Selected Guides", false, 41)]
		private static void BindSelectedGuides()
		{
			BindSelectedGuidesToRubber("Bind Rubber To Guides");
		}

		[MenuItem(MenuRoot + "Bind Selected Guides", true)]
		private static bool CanBindSelectedGuides()
		{
			return SelectedRubber() && SelectedGuides().Count > 0;
		}

		[MenuItem(MenuRoot + "Autofit Now", false, 42)]
		private static void AutofitSelected()
		{
			Autofit(SelectedRubber(), "Autofit Rubber");
		}

		[MenuItem(MenuRoot + "Autofit Now", true)]
		private static bool CanAutofitSelected()
		{
			var rubber = SelectedRubber();
			return rubber && rubber.PathSource == RubberPathSource.Guides
				&& rubber.GuideBindings.Count > 0;
		}

		[MenuItem(MenuRoot + "Detach From Guides", false, 43)]
		private static void DetachSelected()
		{
			var rubber = SelectedRubber();
			Undo.RegisterFullObjectHierarchyUndo(rubber.gameObject, "Detach Rubber From Guides");
			rubber.DetachFromGuides();
			rubber.RebuildMeshes();
			EditorUtility.SetDirty(rubber);
			RubberGuideDependencyTracker.RebuildSoon();
		}

		[MenuItem(MenuRoot + "Detach From Guides", true)]
		private static bool CanDetachSelected()
		{
			var rubber = SelectedRubber();
			return rubber && rubber.PathSource == RubberPathSource.Guides;
		}

		[MenuItem(MenuRoot + "Convert Imported Rubber To Guided", false, 44)]
		private static void ConvertImportedRubberToGuided()
		{
			var rubber = SelectedRubber();
			if (!rubber || !RubberGuideSlotPickerWindow.TryPick(SelectedGuides(),
				out var bindings)) {
				return;
			}
			Undo.RegisterFullObjectHierarchyUndo(rubber.gameObject,
				"Convert Imported Rubber To Guided");
			if (!RubberAutofit.TryConvertToGuides(rubber, bindings, out _, out var error)) {
				EditorUtility.DisplayDialog("Rubber Conversion Failed",
					$"The manual spline remains authoritative.\n\n{error}", "OK");
			} else {
				PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
			}
			EditorUtility.SetDirty(rubber);
			RubberGuideDependencyTracker.RebuildSoon();
		}

		[MenuItem(MenuRoot + "Convert Imported Rubber To Guided", true)]
		private static bool CanConvertImportedRubberToGuided()
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
			if (!RubberAutofit.TryBake(rubber, out _, out var error)) {
				EditorUtility.DisplayDialog("Rubber Autofit Failed", error, "OK");
				return false;
			}
			EditorUtility.SetDirty(rubber);
			PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
			SceneView.RepaintAll();
			return true;
		}

		private static void BindSelectedGuidesToRubber(string undoName)
		{
			var rubber = SelectedRubber();
			if (!rubber || !RubberGuideSlotPickerWindow.TryPick(SelectedGuides(), out var bindings)) {
				return;
			}
			Undo.RegisterFullObjectHierarchyUndo(rubber.gameObject, undoName);
			var convertingSpline = rubber.PathSource == RubberPathSource.Spline;
			string error;
			var succeeded = convertingSpline
				? RubberAutofit.TryConvertToGuides(rubber, bindings, out _, out error)
				: TryReplaceGuideBindings(rubber, bindings, out error);
			if (!succeeded) {
				var retainedState = convertingSpline
					? "The manual spline remains authoritative."
					: "The bindings were preserved and the previous sampled path was retained.";
				EditorUtility.DisplayDialog("Rubber Autofit Failed",
					$"{retainedState}\n\n{error}", "OK");
			} else {
				PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
			}
			EditorUtility.SetDirty(rubber);
			RubberGuideDependencyTracker.RebuildSoon();
		}

		private static bool TryReplaceGuideBindings(RubberComponent rubber,
			RubberGuideBinding[] bindings, out string error)
		{
			rubber.SetGuideBindings(bindings);
			return RubberAutofit.TryBake(rubber, out _, out error);
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
			if (guides.Any(guide => !guide || guide.Slots.Length == 0)) {
				EditorUtility.DisplayDialog("Cannot Bind Rubber", "Every selected guide must expose at least one slot.", "OK");
				return false;
			}
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
