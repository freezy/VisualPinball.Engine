// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

// ReSharper disable AssignmentInConditionalExpression

using System;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberComponent)), CanEditMultipleObjects]
	public class RubberInspector : MainInspector<RubberData, RubberComponent>
	{
		private SerializedProperty _thicknessProperty;
		private SerializedProperty _restLengthProperty;
		private bool _bindingChangeScheduled;

		protected override void OnEnable()
		{
			base.OnEnable();

			_thicknessProperty = serializedObject.FindProperty(nameof(RubberComponent._thickness));
			_restLengthProperty = serializedObject.FindProperty("_restLength");
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();
			DrawPathSection();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
			EditorGUI.BeginChangeCheck();
			var newHeight = EditorGUILayout.FloatField(new GUIContent("Height", "Height of the rubber (in VPX units."), MainComponent.Height);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Rubber Height");
				MainComponent.Height = newHeight;
			}
			PropertyField(_thicknessProperty, rebuildMesh: true);
			PropertyField(_restLengthProperty);
			var fittedLength = MainComponent.BakedPath.Sum(element => element.Length);
			if (MainComponent.PathSource == RubberPathSource.Guides) {
				EditorGUILayout.LabelField("Fitted Centerline Length", $"{fittedLength:0.###} VPX");
				if (MainComponent.RestLength > 0f) {
					EditorGUILayout.LabelField("Installed Stretch",
						$"{fittedLength / MainComponent.RestLength:0.###}×");
				}
			}

			if (MainComponent.PathSource == RubberPathSource.Spline) {
				DragPointSplineInspectorGUI.OnInspectorGUI(MainComponent.DragPointSpline);
			} else {
				EditorGUILayout.HelpBox("The sampled spline is generated from the exact guided path. Detach the rubber before editing knots.",
					MessageType.Info);
			}

			base.OnInspectorGUI();

			EndEditing();
		}

		private void DrawPathSection()
		{
			EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);
			var source = (RubberPathSource)EditorGUILayout.EnumPopup("Source", MainComponent.PathSource);
			if (source != MainComponent.PathSource) {
				if (source == RubberPathSource.Guides) {
					EditorUtility.DisplayDialog("Guide Bindings Required",
						"Select the rubber and its guides, then use GameObject > Pinball > Rubber > Bind Selected Guides.",
						"OK");
				} else {
					ScheduleBindingsChange(MainComponent.GuideBindings.ToArray(),
						Array.Empty<RubberGuideBinding>(), "Detach Rubber From Guides");
				}
			}

			if (MainComponent.PathSource != RubberPathSource.Guides) {
				EditorGUILayout.HelpBox("VPX imports remain spline-authored and use Legacy collision. Bind explicit guide slots to opt into autofit.",
					MessageType.Info);
				return;
			}

			var expectedBindings = MainComponent.GuideBindings.ToArray();
			var bindings = expectedBindings.ToArray();
			for (var i = 0; i < bindings.Length; i++) {
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				var guide = (RubberGuideComponent)EditorGUILayout.ObjectField($"Guide {i + 1}",
					bindings[i].Guide, typeof(RubberGuideComponent), true);
				if (guide != bindings[i].Guide) {
					if (!guide) {
						ScheduleBindingsChange(expectedBindings,
							bindings.Where((_, index) => index != i).ToArray(),
							bindings.Length == 1 ? "Detach Rubber From Guides"
								: "Remove Rubber Guide Binding");
					} else {
						ScheduleGuideReplacement(i, expectedBindings, guide);
					}
					guide = bindings[i].Guide;
				}
				if (guide && guide.Slots.Length > 0) {
					var selectedSlot = Array.FindIndex(guide.Slots, slot => slot.Id == bindings[i].SlotId);
					var slotLabels = guide.Slots.Select((slot, index) => string.IsNullOrEmpty(slot.DisplayName)
						? $"Slot {index + 1}" : slot.DisplayName).ToArray();
					if (selectedSlot < 0) {
						EditorGUILayout.HelpBox($"Missing slot {bindings[i].SlotId}", MessageType.Error);
						var labels = new[] { $"Missing ({bindings[i].SlotId})" }
							.Concat(slotLabels).ToArray();
						var nextSlot = EditorGUILayout.Popup("Slot", 0, labels);
						if (nextSlot > 0) {
							bindings[i].SlotId = guide.Slots[nextSlot - 1].Id;
							ScheduleBindingsChange(expectedBindings, bindings, "Repair Rubber Guide Slot");
						}
					} else {
						var nextSlot = EditorGUILayout.Popup("Slot", selectedSlot, slotLabels);
						if (nextSlot != selectedSlot && nextSlot >= 0
							&& nextSlot < guide.Slots.Length) {
							bindings[i].SlotId = guide.Slots[nextSlot].Id;
							ScheduleBindingsChange(expectedBindings, bindings, "Change Rubber Guide Slot");
						}
					}
				} else if (guide) {
					EditorGUILayout.HelpBox("This guide has no slots.", MessageType.Error);
				}
				if (GUILayout.Button(bindings.Length == 1 ? "Detach From Guides" : "Remove Binding")) {
					ScheduleBindingsChange(expectedBindings,
						bindings.Where((_, index) => index != i).ToArray(),
						bindings.Length == 1 ? "Detach Rubber From Guides"
							: "Remove Rubber Guide Binding");
				}
				EditorGUILayout.EndVertical();
			}

			var status = RubberAutofit.GetStatus(MainComponent);
			EditorGUILayout.HelpBox(status.Message, status.IsValid ? MessageType.Info
				: status.IsStale ? MessageType.Warning : MessageType.Error);
			var supporting = MainComponent.BakedPath
				.Where(element => element.Type == RubberPathElementType.SupportedArc)
				.Select(element => element.StartBindingIndex).Distinct().Count();
			EditorGUILayout.LabelField("Supporting / Enclosed",
				$"{supporting} / {math.max(0, bindings.Length - supporting)}");
			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledScope(bindings.Length == 0)) {
				if (GUILayout.Button("Autofit Now")) {
					RubberGuideCommands.Autofit(MainComponent, "Autofit Rubber");
				}
			}
			if (GUILayout.Button("Detach From Guides")) {
				ScheduleBindingsChange(expectedBindings, Array.Empty<RubberGuideBinding>(),
					"Detach Rubber From Guides");
			}
			EditorGUILayout.EndHorizontal();
		}

		private void ScheduleBindingsChange(RubberGuideBinding[] expectedBindings,
			RubberGuideBinding[] bindings, string undoName)
		{
			if (_bindingChangeScheduled) {
				return;
			}
			expectedBindings = expectedBindings.ToArray();
			bindings = bindings.ToArray();
			_bindingChangeScheduled = true;
			var rubber = MainComponent;
			EditorApplication.delayCall += () => {
				if (!this) {
					return;
				}
				_bindingChangeScheduled = false;
				if (rubber && BindingsMatch(rubber, expectedBindings)) {
					ApplyBindings(rubber, bindings, undoName);
				}
				Repaint();
			};
		}

		private void ScheduleGuideReplacement(int bindingIndex, RubberGuideBinding[] expectedBindings,
			RubberGuideComponent guide)
		{
			if (_bindingChangeScheduled) {
				return;
			}
			expectedBindings = expectedBindings.ToArray();
			_bindingChangeScheduled = true;
			var rubber = MainComponent;
			EditorApplication.delayCall += () => {
				if (!this) {
					return;
				}
				_bindingChangeScheduled = false;
				if (!rubber || !guide || bindingIndex < 0
					|| bindingIndex >= rubber.GuideBindings.Count) {
					Repaint();
					return;
				}
				if (!BindingsMatch(rubber, expectedBindings)) {
					Repaint();
					return;
				}
				if (RubberGuideSlotPickerWindow.TryPick(new[] { guide }, out var selectedBindings)
					&& BindingsMatch(rubber, expectedBindings)) {
					var bindings = rubber.GuideBindings.ToArray();
					bindings[bindingIndex] = selectedBindings[0];
					ApplyBindings(rubber, bindings, "Change Rubber Guide Binding");
				}
				Repaint();
			};
		}

		private static bool BindingsMatch(RubberComponent rubber, RubberGuideBinding[] expectedBindings)
		{
			if (rubber.PathSource != RubberPathSource.Guides
				|| rubber.GuideBindings.Count != expectedBindings.Length) {
				return false;
			}
			for (var i = 0; i < expectedBindings.Length; i++) {
				var current = rubber.GuideBindings[i];
				var expected = expectedBindings[i];
				if (current.Guide != expected.Guide || current.SlotId != expected.SlotId) {
					return false;
				}
			}
			return true;
		}

		private static void ApplyBindings(RubberComponent rubber, RubberGuideBinding[] bindings, string undoName)
		{
			Undo.IncrementCurrentGroup();
			Undo.RegisterFullObjectHierarchyUndo(rubber.gameObject, undoName);
			var detaching = bindings.Length == 0;
			if (detaching) {
				rubber.DetachFromGuides();
				rubber.RebuildMeshes();
			} else if (!RubberAutofit.TryReplaceGuideBindings(rubber, bindings,
				out _, out var error)) {
				Undo.RevertAllInCurrentGroup();
				EditorUtility.DisplayDialog("Rubber Binding Change Failed",
					$"The previous bindings and sampled path were preserved.\n\n{error}", "OK");
				return;
			}
			EditorUtility.SetDirty(rubber);
			PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
			if (detaching) {
				var collider = rubber.GetComponent<RubberColliderComponent>();
				if (collider) {
					EditorUtility.SetDirty(collider);
					PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
				}
			}
			RubberGuideDependencyTracker.RebuildSoon();
			SceneView.RepaintAll();
		}

		private void OnSceneGUI()
		{
			if (MainComponent.PathSource != RubberPathSource.Guides) {
				return;
			}
			foreach (var element in MainComponent.BakedPath) {
				Handles.color = element.Type == RubberPathElementType.FreeSpan
					? new Color(0.15f, 0.9f, 1f, 1f)
					: new Color(1f, 0.55f, 0.1f, 1f);
				DrawPathElement(MainComponent, element);
			}

			var resolution = RubberGuideResolver.Resolve(MainComponent);
			if (!resolution.IsValid) {
				return;
			}
			for (var i = 0; i < resolution.Circles.Length; i++) {
				var circle = resolution.Circles[i];
				var center = resolution.Plane.BakeToWorld(circle.Center);
				Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
				Handles.DrawWireDisc(center, resolution.Plane.Normal, Physics.ScaleToWorld(circle.Radius));
				Handles.color = new Color(1f, 0.75f, 0.2f, 0.8f);
				Handles.DrawWireDisc(center, resolution.Plane.Normal,
					Physics.ScaleToWorld(circle.Radius + MainComponent.Thickness * 0.5f));
			}
		}

		private static void DrawPathElement(RubberComponent rubber, RubberPathElement element)
		{
			if (element.Type == RubberPathElementType.FreeSpan) {
				Handles.DrawAAPolyLine(3f, BakeToWorld(rubber, element.Start),
					BakeToWorld(rubber, element.End));
				return;
			}
			var count = math.max(4, (int)math.ceil(element.SweepAngleRad / (math.PI / 24f)));
			var points = new Vector3[count + 1];
			for (var i = 0; i <= count; i++) {
				var angle = element.StartAngleRad + element.SweepAngleRad * i / count;
				var point = element.Center + new float2(math.cos(angle), math.sin(angle)) * element.Radius;
				points[i] = BakeToWorld(rubber, point);
			}
			Handles.DrawAAPolyLine(3f, points);
		}

		private static Vector3 BakeToWorld(RubberComponent rubber, float2 point)
		{
			var localVpx = rubber.BakeFrameToLocal.MultiplyPoint3x4(new Vector3(point.x, point.y, 0f));
			return localVpx.TranslateToWorld(rubber.transform);
		}

	}
}
