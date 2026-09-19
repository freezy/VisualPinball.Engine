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
	/// <summary>
	/// The menu route to a physical slingshot. The rubber's scene handles offer the same
	/// spans in place; both go through <see cref="RubberSlingshotAuthoring"/>.
	/// </summary>
	internal sealed class RubberSlingshotWizard : EditorWindow
	{
		private RubberComponent _rubber;
		private SlingshotActuatorAsset _actuator;
		private int _spanIndex;
		private readonly List<RubberSlingshotAuthoring.SpanCandidate> _spans = new();
		private string[] _spanLabels = System.Array.Empty<string>();

		[MenuItem("GameObject/Pinball/Add Physical Slingshot To Rubber", false, 21)]
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

		[MenuItem("GameObject/Pinball/Add Physical Slingshot To Rubber", true)]
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
			_actuator = (SlingshotActuatorAsset)EditorGUILayout.ObjectField("Coil Model (optional)",
				_actuator, typeof(SlingshotActuatorAsset), false);

			if (!RubberSlingshotAuthoring.IsEligibleRubber(_rubber)) {
				EditorGUILayout.HelpBox("Choose a current guide-driven Physical rubber.",
					MessageType.Error);
			} else if (RubberSlingshotAuthoring.SlingshotOf(_rubber)) {
				EditorGUILayout.HelpBox(
					"This rubber already has a slingshot. Select the rubber and pick its span in the Scene view.",
					MessageType.Info);
			} else if (_spanLabels.Length == 0) {
				EditorGUILayout.HelpBox("No unambiguous free span is available.",
					MessageType.Error);
			} else {
				var missing = RubberSlingshotAuthoring.DescribeMissing(_rubber);
				if (missing != null) {
					EditorGUILayout.HelpBox($"{missing} The slingshot cannot fire until then.",
						MessageType.Warning);
				}
			}

			using (new EditorGUI.DisabledScope(!_rubber
				|| _spanIndex < 0 || _spanIndex >= _spans.Count)) {
				if (GUILayout.Button("Add")) {
					AddSlingshot();
				}
			}
		}

		private void RefreshSpans()
		{
			_spans.Clear();
			_spanIndex = 0;
			_spans.AddRange(RubberSlingshotAuthoring.FindUnboundSpans(_rubber));
			_spanLabels = _spans
				.Select(candidate => RubberSlingshotAuthoring.Label(candidate.Binding))
				.ToArray();
		}

		private void AddSlingshot()
		{
			var selected = _spans[_spanIndex].Binding;
			RefreshSpans();
			var refreshedIndex = _spans.FindIndex(candidate =>
				RubberSlingshotAuthoring.SameBinding(candidate.Binding, selected));
			if (refreshedIndex < 0) {
				EditorUtility.DisplayDialog("Cannot add physical slingshot",
					"The selected free span changed or is no longer available. Select it again.",
					"OK");
				return;
			}
			_spanIndex = refreshedIndex;
			RubberSlingshotAuthoring.Add(_rubber, selected, _actuator);
			Selection.activeGameObject = _rubber.gameObject;
			Close();
		}
	}
}
