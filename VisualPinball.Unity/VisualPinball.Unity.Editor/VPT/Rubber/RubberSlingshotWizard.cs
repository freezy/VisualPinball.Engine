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
	internal sealed class RubberSlingshotWizard : EditorWindow
	{
		private RubberComponent _rubber;
		private SlingshotActuatorAsset _actuator;
		private int _spanIndex;
		private readonly List<RubberSpanBinding> _spans = new();
		private string[] _spanLabels = System.Array.Empty<string>();

		[MenuItem("GameObject/Pinball/Create Physical Slingshot On Rubber", false, 21)]
		private static void Open()
		{
			var window = CreateInstance<RubberSlingshotWizard>();
			window.titleContent = new GUIContent("Physical Slingshot");
			window._rubber = Selection.activeGameObject
				? Selection.activeGameObject.GetComponent<RubberComponent>()
				: null;
			window.RefreshSpans();
			window.ShowUtility();
		}

		[MenuItem("GameObject/Pinball/Create Physical Slingshot On Rubber", true)]
		private static bool CanOpen() => Selection.activeGameObject
			&& Selection.activeGameObject.GetComponent<RubberComponent>();

		private void OnGUI()
		{
			EditorGUI.BeginChangeCheck();
			_rubber = (RubberComponent)EditorGUILayout.ObjectField("Rubber", _rubber,
				typeof(RubberComponent), true);
			if (EditorGUI.EndChangeCheck()) {
				RefreshSpans();
			}
			using (new EditorGUI.DisabledScope(_spanLabels.Length == 0)) {
				_spanIndex = EditorGUILayout.Popup("Free Span", _spanIndex, _spanLabels);
			}
			_actuator = (SlingshotActuatorAsset)EditorGUILayout.ObjectField("Actuator",
				_actuator, typeof(SlingshotActuatorAsset), false);

			if (!_rubber || !_rubber.HasValidGuidedPath
				|| !_rubber.TryGetComponent<RubberColliderComponent>(out var collider)
				|| collider.Mode != RubberColliderMode.Physical) {
				EditorGUILayout.HelpBox("Choose a current guide-driven Physical rubber.",
					MessageType.Error);
			} else if (_spanLabels.Length == 0) {
				EditorGUILayout.HelpBox("No unambiguous free span is available.",
					MessageType.Error);
			}

			using (new EditorGUI.DisabledScope(!_rubber || !_actuator
				|| _spanIndex < 0 || _spanIndex >= _spans.Count)) {
				if (GUILayout.Button("Create")) {
					CreateSlingshot();
				}
			}
		}

		private void RefreshSpans()
		{
			_spans.Clear();
			_spanIndex = 0;
			if (!_rubber || !_rubber.HasValidGuidedPath
				|| !_rubber.TryGetComponent<RubberColliderComponent>(out var collider)
				|| collider.Mode != RubberColliderMode.Physical) {
				_spanLabels = System.Array.Empty<string>();
				return;
			}
			var boundSpanIndices = new HashSet<int>();
			foreach (var component in FindObjectsByType<RubberSlingshotComponent>(
				FindObjectsInactive.Include, FindObjectsSortMode.None)) {
				if (component.Rubber == _rubber
					&& component.TryResolveSpan(out var resolved, out _)) {
					boundSpanIndices.Add(resolved.PathElementIndex);
				}
			}
			var freeSpans = _rubber.BakedPath
				.Select((element, index) => (element, index))
				.Where(item => item.element.Type == RubberPathElementType.FreeSpan)
				.ToArray();
			foreach (var (span, pathIndex) in freeSpans) {
				var duplicateCount = freeSpans.Count(other =>
					(other.element.StartBindingIndex == span.StartBindingIndex
						&& other.element.EndBindingIndex == span.EndBindingIndex)
					|| (other.element.StartBindingIndex == span.EndBindingIndex
						&& other.element.EndBindingIndex == span.StartBindingIndex));
				if (duplicateCount == 1 && !boundSpanIndices.Contains(pathIndex)) {
					_spans.Add(new RubberSpanBinding(
						_rubber.GuideBindings[span.StartBindingIndex],
						_rubber.GuideBindings[span.EndBindingIndex]));
				}
			}
			_spanLabels = _spans.Select(SpanLabel).ToArray();
		}

		private static string SpanLabel(RubberSpanBinding span)
		{
			var start = BindingLabel(span.StartSupport);
			var end = BindingLabel(span.EndSupport);
			return $"{start} -> {end}";
		}

		private static string BindingLabel(RubberGuideBinding binding)
		{
			if (!binding.Guide || !binding.Guide.TryGetSlot(binding.SlotId, out var slot)) {
				return "Missing support";
			}
			return $"{binding.Guide.name}/{slot.DisplayName}";
		}

		private void CreateSlingshot()
		{
			var selected = _spans[_spanIndex];
			RefreshSpans();
			var refreshedIndex = _spans.FindIndex(candidate =>
				SameBinding(candidate, selected));
			if (refreshedIndex < 0) {
				EditorUtility.DisplayDialog("Cannot create physical slingshot",
					"The selected free span changed or is no longer available. Select it again.",
					"OK");
				return;
			}
			_spanIndex = refreshedIndex;
			var gameObject = new GameObject($"{_rubber.name} Slingshot");
			Undo.RegisterCreatedObjectUndo(gameObject, "Create Physical Slingshot");
			gameObject.transform.SetParent(_rubber.transform.parent, false);
			var component = Undo.AddComponent<RubberSlingshotComponent>(gameObject);
			component.Rubber = _rubber;
			component.Span = selected;
			component.Actuator = _actuator;
			EditorUtility.SetDirty(component);
			Selection.activeGameObject = gameObject;
			Close();
		}

		private static bool SameBinding(RubberSpanBinding left, RubberSpanBinding right)
		{
			return (SameSupport(left.StartSupport, right.StartSupport)
					&& SameSupport(left.EndSupport, right.EndSupport))
				|| (SameSupport(left.StartSupport, right.EndSupport)
					&& SameSupport(left.EndSupport, right.StartSupport));
		}

		private static bool SameSupport(RubberGuideBinding left, RubberGuideBinding right)
			=> left.Guide == right.Guide && left.SlotId == right.SlotId;
	}
}
