// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(WireRailComponent))]
	public class WireRailInspector : UnityEditor.Editor
	{
		private static readonly string[] ThirdRailSides = { "Left", "Right" };
		private static readonly string[] RailCounts = { "1", "2", "3", "4", "5", "6" };
		private static readonly Color TransitionCurveColor = new(0.05f, 0.75f, 1f, 1f);
		private static readonly List<SelectableKnot> SelectedGradeKnots = new();
		private static readonly List<SelectableTangent> SelectedGradeTangents = new();
		private static readonly List<int> SelectedGradeKnotIndices = new();
		private static readonly List<int> BlendedWireIndices = new();
		private static readonly GUIContent TransitionBlendHeightContent = new(
			"Blending Wires 1, 2, 3, 4, 5, 6" + TransitionBlendMessageSuffix);
		private static GUIContent _alignAngleRangeContent;
		private const string TransitionBlendMessageSuffix =
			": offset or diameter changes across this physical span.";
		private const float LayoutLineHeight = 20f;
		private const float LayoutPadding = 7f;
		private const float FixtureScaleMinimum = 0.1f;
		private const float FixtureScaleMaximum = 4f;
		private const int PlanarSplineLengthResolution = 64;
		private static SplineContainer _pendingSplineEdit;
		private readonly WireRailCrossSectionEditor _crossSectionEditor = new();
		private readonly WireRailBracePreviewEditor _bracePreviewEditor = new();
		private readonly WireRailVBracePreviewEditor _vBracePreviewEditor = new();
		private readonly WireRailCrossWirePreviewEditor _crossWirePreviewEditor = new();
		private readonly WireRailLegPreviewEditor _legPreviewEditor = new();
		[SerializeField] private bool _showRenderGeometry = true;
		[SerializeField] private bool _showBallChannelCollider = true;
		[SerializeField] private bool _showFixtures = true;
		[SerializeField] private bool _showWireLayouts = true;
		private readonly List<int> _fixtureOrder = new();
		private readonly List<int> _layoutOrder = new();
		private ReorderableList _fixtureOrderList;
		private ReorderableList _layoutOrderList;
		private int _layoutSelectionBeforePointerDown = -1;

		private void OnEnable()
		{
			_fixtureOrderList = CreateFixtureOrderList();
			_layoutOrderList = CreateLayoutOrderList();
			SplineSelection.changed -= Repaint;
			SplineSelection.changed += Repaint;
			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
		}

		private void OnDisable()
		{
			SplineSelection.changed -= Repaint;
			Undo.undoRedoPerformed -= OnUndoRedo;
			WireRailLayoutEditorSelection.Clear();
		}

		private void OnUndoRedo()
		{
			WireRailLayoutEditorSelection.Clear();
			if (_layoutOrderList != null) {
				_layoutOrderList.index = -1;
			}
			_layoutSelectionBeforePointerDown = -1;
			if (target is WireRailComponent component && component) {
				component.SynchronizeSegments();
				SynchronizeLayoutOrder(_layoutOrder, component, true);
			}
			Repaint();
			SceneView.RepaintAll();
		}

		[MenuItem("GameObject/Pinball/Wire Rail", false, 11)]
		private static void CreateWireRailGameObject(MenuCommand menuCommand)
		{
			var undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Create Wire Rail");
			var parent = menuCommand.context as GameObject;
			parent ??= Selection.activeGameObject;
			var gameObject = new GameObject {
				name = GameObjectUtility.GetUniqueNameForSibling(
					parent ? parent.transform : null, "Wire Rail"),
			};
			StageUtility.PlaceGameObjectInCurrentStage(gameObject);
			GameObjectUtility.SetParentAndAlign(gameObject, parent);
			gameObject.AddComponent<WireRailComponent>();
			Undo.RegisterCreatedObjectUndo(gameObject, "Create Wire Rail");
			Undo.CollapseUndoOperations(undoGroup);

			Selection.activeGameObject = gameObject;
			EditorGUIUtility.PingObject(gameObject);
		}

		public override void OnInspectorGUI()
		{
			var component = (WireRailComponent)target;
			if (Event.current.type == EventType.Layout) {
				component.SynchronizeSegments();
			}
			var container = component.SplineContainer;
			if (!container) {
				EditorGUILayout.HelpBox(
					"The generated Wire Rail Spline child is missing.", MessageType.Error);
				if (GUILayout.Button("Recreate Wire Rail Spline")) {
					Undo.RecordObject(component, "Recreate Wire Rail Spline");
					container = component.EnsureSplineContainerExists();
					component.SynchronizeSegments();
					component.RebuildGeneratedMeshes();
					EditorUtility.SetDirty(component);
				}
				return;
			}

			EditorGUILayout.HelpBox(
				"Spline knots and rail offsets use VPX units. The default route points along +Y; "
				+ "rail offsets are X (lateral) and Z (vertical). Knot rotations control the "
				+ "cross-section orientation when the route turns through 3D.",
				MessageType.Info);

			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.ObjectField("Spline", container, typeof(SplineContainer), true);
			}
			EditorGUILayout.Space(3f);
			var editButtonStyle = new GUIStyle(GUI.skin.button) {
				fontStyle = FontStyle.Bold,
			};
			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button("Edit Spline in Scene View", editButtonStyle,
						GUILayout.Height(30f))) {
					EditSpline(container);
				}
				if (GUILayout.Button(new GUIContent("Center Pivot",
						"Move the Wire Rail GameObject pivot to the route midpoint without moving the rail."),
						GUILayout.Height(30f))) {
					CenterPivot(component);
				}
			}
			EditorGUILayout.HelpBox(
				"While editing, click a knot to show the position gizmo. Double-click the "
				+ "spline to add a knot or double-click a knot to remove it.", MessageType.None);
			var hasGradeRange = TryGetGradeSplineRange(container, out var gradeStartKnot,
				out var gradeEndKnot, out var selectedGradeKnotCount);
			var gradeButtonLabel = selectedGradeKnotCount == 2
				? "Grade Heights Between Selected Knots" : "Grade Heights First → Last";
			var gradeButtonTooltip = selectedGradeKnotCount == 1 || selectedGradeKnotCount > 2
				? "Select either no knots to grade the complete route, or exactly two knots "
					+ "to grade only the interval between them."
				: "Set every knot and Bézier handle in the target interval to a constant "
					+ "grade weighted by horizontal spline distance. Auto Smooth knots become "
					+ "editable modes so the plan-view route and unselected intervals stay fixed.";
			using (new EditorGUI.DisabledScope(!hasGradeRange)) {
				if (GUILayout.Button(new GUIContent(gradeButtonLabel, gradeButtonTooltip))) {
					if (!GradeSplineHeights(component, gradeStartKnot, gradeEndKnot)) {
						Debug.LogWarning("Cannot grade a Wire Rail spline without horizontal length.",
							component);
					}
				}
			}

			if (container.Splines.Count > 1) {
				EditorGUILayout.HelpBox(
					"This first wire-rail slice uses the first spline only. Remove additional splines "
					+ "from the container before authoring wire layouts.", MessageType.Warning);
			}

			EditorGUILayout.Space(8f);
			_showRenderGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(
				_showRenderGeometry, "Render Geometry");
			if (_showRenderGeometry) {
				DrawRenderGeometrySettings(component);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			if (!string.IsNullOrEmpty(component.GenerationError)) {
				EditorGUILayout.HelpBox(component.GenerationError, MessageType.Error);
			}

			EditorGUILayout.Space(4f);
			_showBallChannelCollider = EditorGUILayout.BeginFoldoutHeaderGroup(
				_showBallChannelCollider, "Ball Channel Collider");
			if (_showBallChannelCollider) {
				DrawBallChannelColliderSettings(component);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			EditorGUILayout.Space(4f);
			_showWireLayouts = EditorGUILayout.BeginFoldoutHeaderGroup(
				_showWireLayouts, "Wire Layouts");
			if (_showWireLayouts) {
				DrawWireLayouts(component);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			EditorGUILayout.Space(4f);
			_showFixtures = EditorGUILayout.BeginFoldoutHeaderGroup(_showFixtures, "Fixtures");
			if (_showFixtures) {
				DrawFixtures(component);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
		}

		private void DrawWireLayouts(WireRailComponent component)
		{
			EditorGUILayout.LabelField(
				"Layouts are positioned by distance along the route and are independent from spline knots.",
				EditorStyles.wordWrappedMiniLabel);
			if (component.Segments.Count == 0) {
				EditorGUILayout.HelpBox("Add at least two spline knots to create a wire layout.",
					MessageType.Warning);
				return;
			}
			SynchronizeLayoutOrder(_layoutOrder, component);
			if (Event.current.type == EventType.MouseDown) {
				_layoutSelectionBeforePointerDown = GetSelectedLayoutIndex(component);
			}
			_layoutOrderList.DoLayoutList();
			var selectedLayoutIndex = GetSelectedLayoutIndex(component);
			var buttonContent = selectedLayoutIndex >= 0
				? new GUIContent($"Duplicate Layout {component.GetLayoutDisplayIndex(selectedLayoutIndex) + 1}",
					"Duplicate the selected layout halfway toward its next physical neighbor.")
				: new GUIContent("Add Wire Layout",
					"Add a new layout last in the list and midway between the last two physical positions.");
			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button(buttonContent)) {
					AddOrDuplicateLayout(component, selectedLayoutIndex);
					GUIUtility.ExitGUI();
				}
				using (new EditorGUI.DisabledScope(selectedLayoutIndex < 0)) {
					if (GUILayout.Button(new GUIContent("Deselect",
							"Clear the selected layout so the Add action is available."),
							GUILayout.Width(72f))) {
						DeselectLayout();
						GUIUtility.ExitGUI();
					}
				}
			}
		}

		private void DrawFixtures(WireRailComponent component)
		{
			EditorGUILayout.LabelField(
				"Fixtures are positioned by distance along the complete spline, independently "
					+ "from its wire layouts.", EditorStyles.wordWrappedMiniLabel);
			SynchronizeOrder(_fixtureOrder, component.Fixtures.Count);
			_fixtureOrderList.DoLayoutList();
			var splineLength = component.SplineLength;
			using (new EditorGUILayout.HorizontalScope()) {
				using (new EditorGUI.DisabledScope(splineLength <= 0f)) {
					if (GUILayout.Button("Add Brace")) {
						Edit(component, "Add Wire Rail Brace",
							() => component.AddBraceFixture(splineLength * 0.5f));
						SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
					}
					if (GUILayout.Button("Add V Brace")) {
						Edit(component, "Add Wire Rail V Brace",
							() => component.AddVBraceFixture(splineLength * 0.5f));
						SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
					}
				}
			}
			using (new EditorGUILayout.HorizontalScope()) {
				using (new EditorGUI.DisabledScope(splineLength <= 0f
					|| component.RailCount < 2)) {
					if (GUILayout.Button("Add Cross Wire")) {
						Edit(component, "Add Wire Rail Cross Wire",
							() => component.AddCrossWireFixture(splineLength * 0.5f));
						SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
					}
					if (GUILayout.Button("Add Leg & Foot")) {
						Edit(component, "Add Wire Rail Leg and Foot",
							() => component.AddLegFixture(splineLength * 0.5f));
						SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
					}
				}
			}
		}

		private ReorderableList CreateFixtureOrderList()
		{
			var list = new ReorderableList(_fixtureOrder, typeof(int), true, false, false, false) {
				headerHeight = 0f,
				footerHeight = 0f,
			};
			list.drawElementBackgroundCallback = (rect, index, active, focused) => {
				rect.y -= 2f;
				rect.height += 2f;
				ReorderableList.defaultBehaviours.DrawElementBackground(rect, index,
					active, focused, true);
			};
			list.elementHeightCallback = index => {
				if (target is not WireRailComponent component || index >= _fixtureOrder.Count) {
					return LayoutLineHeight;
				}
				var fixtureIndex = _fixtureOrder[index];
				return fixtureIndex >= 0 && fixtureIndex < component.Fixtures.Count
					? GetFixtureElementHeight(component.Fixtures[fixtureIndex])
					: LayoutLineHeight;
			};
			list.drawElementCallback = (rect, index, _, _) => {
				if (target is not WireRailComponent component || index >= _fixtureOrder.Count) {
					return;
				}
				var fixtureIndex = _fixtureOrder[index];
				if (fixtureIndex < 0 || fixtureIndex >= component.Fixtures.Count) {
					return;
				}
				DrawFixtureElement(rect, component, fixtureIndex);
			};
			list.onReorderCallbackWithDetails = (_, fromIndex, toIndex) => {
				if (target is WireRailComponent component) {
					Edit(component, "Reorder Wire Rail Fixtures",
						() => component.MoveFixture(fromIndex, toIndex));
					SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				}
			};
			return list;
		}

		private ReorderableList CreateLayoutOrderList()
		{
			var list = new ReorderableList(_layoutOrder, typeof(int), true, false, false, false) {
				headerHeight = 0f,
				footerHeight = 0f,
			};
			list.drawElementBackgroundCallback = (rect, index, active, focused) => {
				rect.y -= 2f;
				rect.height += 2f;
				ReorderableList.defaultBehaviours.DrawElementBackground(rect, index,
					active, focused, true);
			};
			list.elementHeightCallback = index => {
				if (target is not WireRailComponent component || index >= _layoutOrder.Count) {
					return LayoutLineHeight;
				}
				return GetLayoutElementHeight(component, _layoutOrder[index]);
			};
			list.drawElementCallback = (rect, index, _, _) => {
				if (target is not WireRailComponent component || index >= _layoutOrder.Count) {
					return;
				}
				var layoutIndex = _layoutOrder[index];
				if (layoutIndex < 0 || layoutIndex >= component.Segments.Count) {
					return;
				}
				DrawLayoutElement(rect, component, layoutIndex);
			};
			list.onReorderCallbackWithDetails = (_, fromIndex, toIndex) => {
				if (target is WireRailComponent component) {
					Edit(component, "Reorder Wire Rail Layouts",
						() => component.MoveLayout(fromIndex, toIndex));
					SynchronizeLayoutOrder(_layoutOrder, component, true);
					if (toIndex >= 0 && toIndex < _layoutOrder.Count) {
						SelectLayout(component, _layoutOrder[toIndex]);
					}
				}
			};
			list.onSelectCallback = _ => {
				if (target is WireRailComponent component) {
					var layoutIndex = GetSelectedLayoutIndex(component);
					if (layoutIndex >= 0) {
						SelectLayout(component, layoutIndex);
					}
				}
			};
			return list;
		}

		private static void SynchronizeLayoutOrder(List<int> order,
			WireRailComponent component, bool force = false)
		{
			var displayOrder = component.LayoutDisplayOrder;
			var matches = !force && order.Count == displayOrder.Count;
			if (matches) {
				for (var index = 0; index < order.Count; index++) {
					if (order[index] != displayOrder[index]) {
						matches = false;
						break;
					}
				}
			}
			if (matches) {
				return;
			}
			order.Clear();
			for (var index = 0; index < displayOrder.Count; index++) {
				order.Add(displayOrder[index]);
			}
		}

		private static void SynchronizeOrder(List<int> order, int count, bool force = false)
		{
			if (!force && order.Count == count) {
				return;
			}
			order.Clear();
			for (var index = 0; index < count; index++) {
				order.Add(index);
			}
		}

		private static float GetFixtureElementHeight(WireRailFixture fixture)
			=> fixture switch {
				WireRailBraceFixture => LayoutPadding * 2f + LayoutLineHeight * 8f
					+ WireRailBracePreviewEditor.Height + 25f,
				WireRailVBraceFixture => LayoutPadding * 2f + LayoutLineHeight * 10f
					+ WireRailVBracePreviewEditor.Height + 35f,
				WireRailCrossWireFixture => LayoutPadding * 2f + LayoutLineHeight * 5f
					+ WireRailCrossWirePreviewEditor.Height + 25f,
				WireRailLegFixture => LayoutPadding * 2f + LayoutLineHeight * 15f
					+ (GetVector3FieldHeight() - LayoutLineHeight) * 3f
					+ WireRailLegPreviewEditor.Height + 55f,
				_ => LayoutLineHeight * 2f,
			};

		private void DrawFixtureElement(Rect rect, WireRailComponent component,
			int fixtureIndex)
		{
			rect.y -= 1f;
			rect.height -= 1f;
			var content = new Rect(rect.x + LayoutPadding, rect.y + LayoutPadding,
				rect.width - LayoutPadding * 2f, rect.height - LayoutPadding * 2f);
			if (component.Fixtures[fixtureIndex] is WireRailBraceFixture brace) {
				DrawBraceFixtureElement(content, component, fixtureIndex, brace);
				return;
			}
			if (component.Fixtures[fixtureIndex] is WireRailVBraceFixture vBrace) {
				DrawVBraceFixtureElement(content, component, fixtureIndex, vBrace);
				return;
			}
			if (component.Fixtures[fixtureIndex] is WireRailCrossWireFixture crossWire) {
				DrawCrossWireFixtureElement(content, component, fixtureIndex, crossWire);
				return;
			}
			if (component.Fixtures[fixtureIndex] is WireRailLegFixture leg) {
				DrawLegFixtureElement(content, component, fixtureIndex, leg);
				return;
			}
			{
				EditorGUI.HelpBox(content, $"Fixture {fixtureIndex + 1} has an unsupported type.",
					MessageType.Warning);
				return;
			}
		}

		private void DrawVBraceFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailVBraceFixture vBrace)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"V Brace {fixtureIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this V brace",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this V brace",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail V Brace",
					() => component.DuplicateVBraceFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail V Brace",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var distance = EditorGUI.Slider(row, new GUIContent("Position",
				"Distance along the complete spline in VPX units."), vBrace.Distance,
				0f, math.max(0f, component.SplineLength));

			row.y += LayoutLineHeight + 3f;
			var ringDensity = EditorGUI.IntSlider(row, new GUIContent("Ring Density",
				"Minimum sampling density for the rounded bottom corner or corners. "
					+ "A 15° safety limit adds rings when needed to preserve wire thickness."),
				vBrace.RingDensity, 3, 128);

			row.y += LayoutLineHeight + 4f;
			var previewRect = new Rect(content.x, row.y, content.width,
				WireRailVBracePreviewEditor.Height);
			_vBracePreviewEditor.Draw(previewRect, component, fixtureIndex, vBrace);

			row.y = previewRect.yMax + 4f;
			var lateralOffset = vBrace.LateralOffset;
			var verticalOffset = vBrace.VerticalOffset;
			DrawFixtureOffsetRow(row, ref lateralOffset, ref verticalOffset,
				out var resetOffset);
			if (resetOffset) {
				Edit(component, "Reset Wire Rail V Brace Offset", () =>
					component.SetVBraceFixtureProperties(fixtureIndex, vBrace.Distance,
						vBrace.RingDensity, 0f, 0f, vBrace.HasStraightSection,
						vBrace.StraightHeight, vBrace.LeftLength, vBrace.RightLength,
						vBrace.Angle, vBrace.Rotation, vBrace.CornerRadius));
				GUIUtility.ExitGUI();
			}

			row.y += LayoutLineHeight + 3f;
			var leftLength = EditorGUI.DelayedFloatField(row, new GUIContent("Left Length",
				"Centerline length from the theoretical bottom to the left endpoint."),
				vBrace.LeftLength);

			row.y += LayoutLineHeight + 3f;
			var rightLength = EditorGUI.DelayedFloatField(row, new GUIContent("Right Length",
				"Centerline length from the theoretical bottom to the right endpoint."),
				vBrace.RightLength);

			row.y += LayoutLineHeight + 3f;
			var angle = EditorGUI.Slider(row, new GUIContent("Angle",
				"Included angle between the two arms."), vBrace.Angle, 1f, 179f);

			row.y += LayoutLineHeight + 3f;
			var rotation = EditorGUI.Slider(row, new GUIContent("Rotation",
				"Rotate the complete V around the spline tangent."),
				vBrace.Rotation, 0f, 360f);

			row.y += LayoutLineHeight + 3f;
			var cornerRadius = EditorGUI.FloatField(row, new GUIContent("Corner Radius",
				"Requested centerline radius for the rounded bottom corner or corners."),
				vBrace.CornerRadius);

			row.y += LayoutLineHeight + 3f;
			const float toggleWidth = 102f;
			var hasStraightSection = EditorGUI.ToggleLeft(new Rect(row.x, row.y,
				toggleWidth, row.height), new GUIContent("Straight Line",
				"Replace the tip with a horizontal bottom segment."),
				vBrace.HasStraightSection);
			var straightHeight = vBrace.StraightHeight;
			using (new EditorGUI.DisabledScope(!hasStraightSection)) {
				straightHeight = EditorGUI.FloatField(new Rect(row.x + toggleWidth + 4f,
					row.y, math.max(20f, row.width - toggleWidth - 4f), row.height),
					new GUIContent("Height",
						"Height above the theoretical V tip where the flat bottom crosses both arms."),
					straightHeight);
			}

			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail V Brace", () =>
					component.SetVBraceFixtureProperties(fixtureIndex, distance,
						ringDensity, lateralOffset, verticalOffset, hasStraightSection,
						straightHeight, leftLength, rightLength, angle, rotation,
						cornerRadius));
			}
		}

		private void DrawBraceFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailBraceFixture brace)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Brace {fixtureIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this brace",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this brace",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Brace",
					() => component.DuplicateBraceFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Brace",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var distance = EditorGUI.Slider(row, new GUIContent("Position",
				"Distance along the complete spline in VPX units."), brace.Distance,
				0f, math.max(0f, component.SplineLength));

			row.y += LayoutLineHeight + 3f;
			var scale = EditorGUI.Slider(row, new GUIContent("Scale",
				"Multiplier for the automatically fitted brace radius."), brace.Scale,
				FixtureScaleMinimum, FixtureScaleMaximum);

			row.y += LayoutLineHeight + 3f;
			var ringDensity = EditorGUI.IntSlider(row, new GUIContent("Ring Density",
				"Number of longitudinal tube rings around a complete brace."),
				brace.RingDensity, 3, 128);

			row.y += LayoutLineHeight + 4f;
			var previewRect = new Rect(content.x, row.y, content.width,
				WireRailBracePreviewEditor.Height);
			_bracePreviewEditor.Draw(previewRect, component, fixtureIndex, brace);

			row.y = previewRect.yMax + 4f;
			var lateralOffset = brace.LateralOffset;
			var verticalOffset = brace.VerticalOffset;
			DrawFixtureOffsetRow(row, ref lateralOffset, ref verticalOffset,
				out var resetOffset);
			if (resetOffset) {
				Edit(component, "Reset Wire Rail Brace Offset", () =>
					component.SetBraceFixtureProperties(fixtureIndex, brace.Distance,
						brace.HasCutout, brace.CutoutStartAngle, brace.CutoutEndAngle,
						brace.HasStraightSection, brace.StraightStartAngle,
						brace.StraightEndAngle, 0f, 0f, brace.Scale,
						brace.RingDensity));
				GUIUtility.ExitGUI();
			}

			row.y += LayoutLineHeight + 3f;
			var cutoutStart = brace.CutoutStartAngle;
			var cutoutEnd = brace.CutoutEndAngle;
			var hasCutout = DrawAngleRange(row, "Cutout",
				"Remove the angular range from the brace.", brace.HasCutout,
				ref cutoutStart, ref cutoutEnd);

			row.y += LayoutLineHeight + 3f;
			var straightStart = brace.StraightStartAngle;
			var straightEnd = brace.StraightEndAngle;
			var hasStraightSection = DrawAngleRange(row, "Straight Line",
				"Replace the angular range with a straight chord.", brace.HasStraightSection,
				ref straightStart, ref straightEnd);

			var propertiesChanged = EditorGUI.EndChangeCheck();
			row.y += LayoutLineHeight + 3f;
			var hasOtherBrace = HasOtherBraceFixture(component, fixtureIndex);
			var applyToAll = false;
			using (new EditorGUI.DisabledScope(!hasOtherBrace)) {
				applyToAll = GUI.Button(row, new GUIContent("Apply to All",
					"Copy every setting except Position from this brace to all other braces."));
			}
			if (applyToAll) {
				Edit(component, "Apply Wire Rail Brace Settings to All", () => {
					if (propertiesChanged) {
						component.SetBraceFixtureProperties(fixtureIndex, distance,
							hasCutout, cutoutStart, cutoutEnd,
							hasStraightSection, straightStart, straightEnd,
							lateralOffset, verticalOffset, scale, ringDensity);
					}
					component.ApplyBracePropertiesToAll(fixtureIndex);
				});
				GUIUtility.ExitGUI();
			}
			if (propertiesChanged) {
				Edit(component, "Edit Wire Rail Brace", () =>
					component.SetBraceFixtureProperties(fixtureIndex, distance,
						hasCutout, cutoutStart, cutoutEnd,
						hasStraightSection, straightStart, straightEnd,
						lateralOffset, verticalOffset, scale, ringDensity));
			}
		}

		private void DrawCrossWireFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailCrossWireFixture crossWire)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Cross Wire {fixtureIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this cross wire",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this cross wire",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Cross Wire",
					() => component.DuplicateCrossWireFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Cross Wire",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var distance = EditorGUI.Slider(row, new GUIContent("Position",
				"Distance along the complete spline in VPX units."), crossWire.Distance,
				0f, math.max(0f, component.SplineLength));

			row.y += LayoutLineHeight + 4f;
			var previewRect = new Rect(content.x, row.y, content.width,
				WireRailCrossWirePreviewEditor.Height);
			_crossWirePreviewEditor.Draw(previewRect, component, fixtureIndex, crossWire);

			row.y = previewRect.yMax + 4f;
			var lateralOffset = crossWire.LateralOffset;
			var verticalOffset = crossWire.VerticalOffset;
			DrawFixtureOffsetRow(row, ref lateralOffset, ref verticalOffset,
				out var resetOffset);
			if (resetOffset) {
				Edit(component, "Reset Wire Rail Cross Wire Offset", () =>
					component.SetCrossWireFixtureProperties(fixtureIndex, crossWire.Distance,
						crossWire.Angle, 0f, 0f,
						crossWire.LengthAdjustment));
				GUIUtility.ExitGUI();
			}

			row.y += LayoutLineHeight + 3f;
			var angle = EditorGUI.Slider(row, new GUIContent("Angle",
				"Rotation around the spline tangent. 0° is horizontal along local X; "
				+ "90° is vertical along local Z."), crossWire.Angle, 0f, 360f);

			row.y += LayoutLineHeight + 3f;
			var lengthAdjustment = EditorGUI.FloatField(row, new GUIContent("Length",
				"Signed VPX adjustment to the span between the bottom rails. Positive values "
				+ "extend the wire; negative values shorten it."), crossWire.LengthAdjustment);

			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail Cross Wire", () =>
					component.SetCrossWireFixtureProperties(fixtureIndex, distance,
						angle, lateralOffset, verticalOffset,
						lengthAdjustment));
			}
		}

		private void DrawLegFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailLegFixture leg)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Leg & Foot {fixtureIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this leg and foot",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this leg and foot",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Leg and Foot",
					() => component.DuplicateLegFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Leg and Foot",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var distance = EditorGUI.Slider(row, new GUIContent("Position",
				"Distance along the complete spline in VPX units."), leg.Distance,
				0f, math.max(0f, component.SplineLength));

			row.y += LayoutLineHeight + 4f;
			var previewRect = new Rect(content.x, row.y, content.width,
				WireRailLegPreviewEditor.Height);
			_legPreviewEditor.Draw(previewRect, component, fixtureIndex, leg);

			row.y = previewRect.yMax + 4f;
			EditorGUI.LabelField(row, "Rail Attachment", EditorStyles.boldLabel);

			row.y += LayoutLineHeight + 3f;
			var lateralOffset = leg.LateralOffset;
			var verticalOffset = leg.VerticalOffset;
			DrawFixtureOffsetRow(row, ref lateralOffset, ref verticalOffset,
				out var resetOffset);
			if (resetOffset) {
				Edit(component, "Reset Wire Rail Leg Attachment Offset", () =>
					component.SetLegFixtureProperties(fixtureIndex, leg.Distance, leg.LegSide,
						leg.StartDirection, leg.StartLength, leg.FootPosition,
						leg.FootRotation, leg.FootWidth, leg.FootLength,
						leg.FootConnectionLength, 0f, 0f, leg.LengthAdjustment));
				GUIUtility.ExitGUI();
			}

			row.y += LayoutLineHeight + 3f;
			var lengthAdjustment = EditorGUI.FloatField(row, new GUIContent("Length",
				"Signed VPX adjustment to the attachment span between the bottom rails. "
				+ "Positive values extend it; negative values shorten it."),
				leg.LengthAdjustment);

			row.y += LayoutLineHeight + 3f;
			EditorGUI.LabelField(row, "Leg", EditorStyles.boldLabel);

			row.y += LayoutLineHeight + 3f;
			var legSide = (WireRailLegSide)EditorGUI.EnumPopup(row,
				new GUIContent("Side", "End of the bottom-rail attachment where the leg begins."),
				leg.LegSide);

			row.y += LayoutLineHeight + 3f;
			var vector3FieldHeight = GetVector3FieldHeight();
			row.height = vector3FieldHeight;
			var startDirection = EditorGUI.Vector3Field(row,
				new GUIContent("Start Vector", "Route-local XYZ direction followed by the leg before it bends toward the foot."),
				leg.StartDirection);

			row.y += vector3FieldHeight + 3f;
			row.height = LayoutLineHeight;
			var startLength = EditorGUI.FloatField(row,
				new GUIContent("Start Length", "Distance in VPX units traveled along the start vector before connecting to the foot."),
				leg.StartLength);

			row.y += LayoutLineHeight + 3f;
			EditorGUI.LabelField(row, "U-Hook Foot", EditorStyles.boldLabel);

			row.y += LayoutLineHeight + 3f;
			row.height = vector3FieldHeight;
			var footPosition = EditorGUI.Vector3Field(row,
				new GUIContent("Position", "Route-local XYZ translation of the foot pivot, relative to the leg attachment."),
				leg.FootPosition);

			row.y += vector3FieldHeight + 3f;
			var footRotation = EditorGUI.Vector3Field(row,
				new GUIContent("Rotation", "Route-local XYZ Euler rotation of the complete U-hook foot."),
				leg.FootRotation);

			row.y += vector3FieldHeight + 3f;
			row.height = LayoutLineHeight;
			var footWidth = EditorGUI.FloatField(row,
				new GUIContent("Width", "Centerline width across the U-hook bend in VPX units."),
				leg.FootWidth);

			row.y += LayoutLineHeight + 3f;
			var footLength = EditorGUI.FloatField(row,
				new GUIContent("Arm Length", "Length of the free straight U-hook arm in VPX units."),
				leg.FootLength);

			row.y += LayoutLineHeight + 3f;
			var footConnectionLength = EditorGUI.FloatField(row,
				new GUIContent("Connected Arm Length", "Distance from the U-hook bend to the point where the leg's connection segment joins the hook."),
				leg.FootConnectionLength);

			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail Leg and Foot", () =>
					component.SetLegFixtureProperties(fixtureIndex, distance, legSide,
						startDirection, startLength, footPosition, footRotation,
						footWidth, footLength, footConnectionLength, lateralOffset,
						verticalOffset, lengthAdjustment));
			}
		}

		private static float GetVector3FieldHeight()
			=> EditorGUIUtility.wideMode ? LayoutLineHeight : LayoutLineHeight * 2f;

		private static void DrawFixtureOffsetRow(Rect rect, ref float lateralOffset,
			ref float verticalOffset, out bool reset)
		{
			const float resetWidth = 54f;
			const float offsetLabelWidth = 42f;
			const float axisLabelWidth = 14f;
			const float spacing = 4f;
			var numericFieldWidth = math.max(20f, (rect.width - offsetLabelWidth
				- axisLabelWidth * 2f - resetWidth - spacing * 5f) * 0.5f);
			var axisFieldWidth = axisLabelWidth + numericFieldWidth;
			var x = rect.x;
			EditorGUI.LabelField(new Rect(x, rect.y, offsetLabelWidth, LayoutLineHeight),
				"Offset");
			x += offsetLabelWidth + spacing;
			var previousLabelWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = axisLabelWidth;
			lateralOffset = EditorGUI.FloatField(new Rect(x, rect.y, axisFieldWidth,
				LayoutLineHeight), new GUIContent("X", "Lateral offset in VPX units."),
				lateralOffset);
			x += axisFieldWidth + spacing;
			verticalOffset = EditorGUI.FloatField(new Rect(x, rect.y, axisFieldWidth,
				LayoutLineHeight), new GUIContent("Z", "Vertical offset in VPX units."),
				verticalOffset);
			EditorGUIUtility.labelWidth = previousLabelWidth;
			reset = GUI.Button(new Rect(rect.xMax - resetWidth, rect.y, resetWidth,
				LayoutLineHeight), "Reset");
		}

		private static bool DrawAngleRange(Rect rect, string label, string tooltip,
			bool enabled, ref float start, ref float end)
		{
			const float toggleWidth = 102f;
			enabled = EditorGUI.ToggleLeft(new Rect(rect.x, rect.y, toggleWidth,
				LayoutLineHeight), new GUIContent(label, tooltip), enabled);
			if (!enabled) {
				return false;
			}
			DrawAnglePairControls(rect, toggleWidth, ref start, ref end);
			return true;
		}

		private static void DrawAnglePairControls(Rect rect, float prefixWidth,
			ref float start, ref float end)
		{
			const float fieldWidth = 42f;
			const float alignButtonWidth = LayoutLineHeight;
			const float alignIconSize = 16f;
			const float spacing = 4f;
			var startRect = new Rect(rect.x + prefixWidth + spacing, rect.y, fieldWidth,
				LayoutLineHeight);
			var alignRect = new Rect(rect.xMax - alignButtonWidth, rect.y, alignButtonWidth,
				LayoutLineHeight);
			var endRect = new Rect(alignRect.x - spacing - fieldWidth, rect.y, fieldWidth,
				LayoutLineHeight);
			var sliderRect = new Rect(startRect.xMax + spacing, rect.y,
				math.max(10f, endRect.x - startRect.xMax - spacing * 2f), LayoutLineHeight);
			start = math.clamp(EditorGUI.FloatField(startRect, new GUIContent(string.Empty,
				"From angle in degrees."), start), 0f, 360f);
			end = math.clamp(EditorGUI.FloatField(endRect, new GUIContent(string.Empty,
				"To angle in degrees."), end), 0f, 360f);
			if (start <= end) {
				EditorGUI.MinMaxSlider(sliderRect, ref start, ref end, 0f, 360f);
			} else {
				var gapStart = end;
				var gapEnd = start;
				EditorGUI.MinMaxSlider(sliderRect, ref gapStart, ref gapEnd, 0f, 360f);
				start = gapEnd;
				end = gapStart;
			}
			EditorGUIUtility.AddCursorRect(alignRect, MouseCursor.Link);
			var alignClicked = GUI.Button(alignRect, AlignAngleRangeContent,
				EditorStyles.miniButton);
			var iconRect = new Rect(alignRect.center.x - alignIconSize * 0.5f,
				alignRect.center.y - alignIconSize * 0.5f, alignIconSize, alignIconSize);
			GUI.DrawTexture(iconRect, Icons.Horizon(), ScaleMode.ScaleToFit, true);
			if (alignClicked) {
				var aligned = WireRailBraceFixture.AlignAngleRangeHorizontally(start, end);
				start = aligned.x;
				end = aligned.y;
			}
		}

		private static GUIContent AlignAngleRangeContent
			=> _alignAngleRangeContent ??= new GUIContent(string.Empty,
				"Align both endpoints to the same vertical height.");

		private static bool HasOtherBraceFixture(WireRailComponent component,
			int sourceFixtureIndex)
		{
			for (var fixtureIndex = 0; fixtureIndex < component.Fixtures.Count;
				fixtureIndex++) {
				if (fixtureIndex != sourceFixtureIndex
					&& component.Fixtures[fixtureIndex] is WireRailBraceFixture) {
					return true;
				}
			}
			return false;
		}

		private void DrawRenderGeometrySettings(WireRailComponent component)
		{
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();
			EditorGUI.BeginChangeCheck();
			var railCount = EditorGUILayout.Popup(new GUIContent("Rails",
				"Total number of rails available to every wire layout."),
				math.clamp(component.RailCount, 1, RailCounts.Length) - 1, RailCounts) + 1;
			var railCountChanged = EditorGUI.EndChangeCheck();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_renderMaterial"),
				new GUIContent("Material"));
			EditorGUI.BeginChangeCheck();
			var wireDiameter = EditorGUILayout.FloatField(new GUIContent("Wire Diameter",
				"Global diameter for every rail wire and fixture wire."),
				component.WireDiameter);
			var wireDiameterChanged = EditorGUI.EndChangeCheck();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_radialSegments"),
				new GUIContent("Tube Sides"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_wireCapBevelSize"),
				new GUIContent("Wire Cap Bevel",
					"One-segment bevel size on every exposed wire end. Each wire clamps the size "
					+ "to half its diameter."));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_renderSamplesPerSegment"),
				new GUIContent("Minimum Samples Per Layout Span",
					"Base longitudinal detail. Sharper wire bends receive extra rings automatically."));

			var settingsChanged = EditorGUI.EndChangeCheck();
			serializedObject.ApplyModifiedProperties();
			if (railCountChanged) {
				Edit(component, "Change Wire Rail Count",
					() => component.SetRailCount(railCount));
			} else if (wireDiameterChanged) {
				Edit(component, "Change Wire Rail Diameter",
					() => component.SetWireDiameter(math.max(0.1f, wireDiameter)));
			} else if (settingsChanged) {
				component.RebuildGeneratedMeshes();
				SceneView.RepaintAll();
			}

			var renderMesh = component.RenderMesh;
			if (renderMesh) {
				EditorGUILayout.LabelField("Generated", $"{renderMesh.vertexCount} vertices");
			}
			if (GUILayout.Button("Rebuild Render Geometry")) {
				component.RebuildGeneratedMeshes();
				SceneView.RepaintAll();
			}
		}

		private void DrawBallChannelColliderSettings(WireRailComponent component)
		{
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_referenceBallDiameter"),
				new GUIContent("Ball Diameter",
					"Diameter of the reference ball used to fit the collision channel. It changes "
					+ "only the collider, not the visible wires or the game's ball."));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_colliderSamplesPerSegment"),
				new GUIContent("Curvature Detail",
					"Controls adaptive collider tessellation. Curves receive more rows while "
					+ "straight spans remain sparse."));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_showColliderPreview"),
				new GUIContent("Show Collider Preview"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_physicsMaterial"),
				new GUIContent("Physics Material"));
			var overwritePhysics = serializedObject.FindProperty("_overwritePhysics");
			EditorGUILayout.PropertyField(overwritePhysics, new GUIContent("Overwrite Physics"));
			if (overwritePhysics.boolValue) {
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_elasticity"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_elasticityFalloff"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_friction"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_scatter"));
			}
			var settingsChanged = EditorGUI.EndChangeCheck();
			serializedObject.ApplyModifiedProperties();
			if (settingsChanged) {
				component.RebuildGeneratedMeshes();
				SceneView.RepaintAll();
			}

			var colliderMesh = component.ColliderMesh;
			if (colliderMesh) {
				EditorGUILayout.LabelField("Generated",
					$"{colliderMesh.triangles.Length / 3} triangles");
			}
			if (GUILayout.Button("Rebuild Collider")) {
				component.RebuildGeneratedMeshes();
				SceneView.RepaintAll();
			}
		}

		private float GetLayoutElementHeight(WireRailComponent component, int layoutIndex)
		{
			if (layoutIndex < 0 || layoutIndex >= component.Segments.Count) {
				return LayoutLineHeight;
			}
			var layout = component.Segments[layoutIndex];
			var height = LayoutPadding * 2f + LayoutLineHeight * 2f + 6f
				+ WireRailCrossSectionEditor.Height;
			if (component.RailCount == 3) {
				height += LayoutLineHeight + 3f;
			}
			var connectionHeight = GetConnectionHeight(component, layoutIndex);
			return height + (connectionHeight > 0f ? connectionHeight + 5f : 0f);
		}

		private void DrawLayoutElement(Rect rect, WireRailComponent component, int layoutIndex)
		{
			rect.y -= 1f;
			rect.height -= 1f;
			var content = new Rect(rect.x + LayoutPadding, rect.y + LayoutPadding,
				rect.width - LayoutPadding * 2f, rect.height - LayoutPadding * 2f);
			var layout = component.Segments[layoutIndex];
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			var displayIndex = component.GetLayoutDisplayIndex(layoutIndex);
			EditorGUI.LabelField(row, $"Layout {displayIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this wire layout between physical neighbors",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this wire layout",
			};
			var canRemoveLayout = component.Segments.Count > 1;
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			if (canRemoveLayout) {
				EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			}
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				AddOrDuplicateLayout(component, layoutIndex);
				GUIUtility.ExitGUI();
			}
			using (new EditorGUI.DisabledScope(!canRemoveLayout)) {
				if (GUI.Button(trashRect, trash, GUIStyle.none)) {
					var removedDisplayIndex = component.GetLayoutDisplayIndex(layoutIndex);
					var previousSelectedLayout = _layoutSelectionBeforePointerDown;
					Edit(component, "Remove Wire Rail Layout",
						() => component.RemoveLayout(layoutIndex));
					SynchronizeLayoutOrder(_layoutOrder, component, true);
					if (previousSelectedLayout < 0) {
						DeselectLayout();
					} else if (previousSelectedLayout == layoutIndex) {
						var selectedDisplayIndex = math.min(removedDisplayIndex,
							_layoutOrder.Count - 1);
						SelectLayout(component, _layoutOrder[selectedDisplayIndex]);
					} else {
						var selectedLayoutIndex = previousSelectedLayout > layoutIndex
							? previousSelectedLayout - 1 : previousSelectedLayout;
						SelectLayout(component, selectedLayoutIndex);
					}
					GUIUtility.ExitGUI();
				}
			}

			row.y = content.y + LayoutLineHeight + 3f;
			var positionRect = new Rect(row.x, row.y, row.width, LayoutLineHeight);
			float position;
			using (new EditorGUI.DisabledScope(layoutIndex == 0)) {
				var previousLabelWidth = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = 52f;
				var positionTooltip = layoutIndex == 0
					? "The route's physical starting layout is anchored at 0 VPX."
					: "Distance along the complete spline in VPX units.";
				position = EditorGUI.DelayedFloatField(positionRect,
					new GUIContent("Position", positionTooltip), layout.Distance);
				EditorGUIUtility.labelWidth = previousLabelWidth;
			}
			if (!Mathf.Approximately(position, layout.Distance)) {
				Edit(component, "Move Wire Rail Layout",
					() => component.SetLayoutDistance(layoutIndex, position));
				layout = component.Segments[layoutIndex];
			}

			if (component.RailCount == 3) {
				row.y += LayoutLineHeight + 3f;
				var sideLabelRect = new Rect(row.x, row.y, 70f, LayoutLineHeight);
				EditorGUI.LabelField(sideLabelRect, new GUIContent("Third Rail",
					"Choose whether the raised third rail is on the left or right."));
				var sideRect = new Rect(sideLabelRect.xMax, row.y,
					row.xMax - sideLabelRect.xMax, LayoutLineHeight);
				var side = (WireRailThirdRailSide)GUI.Toolbar(sideRect,
					(int)layout.ThirdRailSide, ThirdRailSides);
				if (side != layout.ThirdRailSide) {
					Edit(component, "Change Third Wire Rail Side",
						() => component.SetThirdRailSide(layoutIndex, side));
				}
			}

			var crossSectionY = row.yMax + 4f;
			var crossSectionRect = new Rect(content.x, crossSectionY, content.width,
				WireRailCrossSectionEditor.Height);
			_crossSectionEditor.Draw(crossSectionRect, component, layoutIndex);
			var connectionHeight = GetConnectionHeight(component, layoutIndex);
			if (connectionHeight > 0f) {
				DrawConnection(new Rect(content.x, crossSectionRect.yMax + 5f,
					content.width, connectionHeight), component, layoutIndex);
			}
		}

		private static float GetConnectionHeight(WireRailComponent component, int layoutIndex)
		{
			var nextLayoutIndex = component.GetNextSegmentIndex(layoutIndex);
			if (nextLayoutIndex < 0) {
				return 0f;
			}
			var layout = component.Segments[layoutIndex];
			var connection = layout.ConnectionToNext;
			var wireCount = component.RailCount;
			var overriddenWireCount = 0;
			for (var wireIndex = 0; wireIndex < wireCount; wireIndex++) {
				if (layout.IsRailActive(wireIndex)
					&& component.Segments[nextLayoutIndex].IsRailActive(wireIndex)
					&& connection.IsWireOverridden(wireIndex)) {
					overriddenWireCount++;
				}
			}
			var blendInfoHeight = TransitionUsesBlending(component, layoutIndex)
				? GetTransitionBlendInfoHeight() + 3f : 0f;
			return LayoutPadding * 2f + LayoutLineHeight * 2f + 3f + blendInfoHeight
				+ overriddenWireCount * (LayoutLineHeight + 3f);
		}

		private static void DrawConnection(Rect rect, WireRailComponent component,
			int layoutIndex)
		{
			GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
			var nextLayoutIndex = component.GetNextSegmentIndex(layoutIndex);
			var layout = component.Segments[layoutIndex];
			var nextLayout = component.Segments[nextLayoutIndex];
			var connection = layout.ConnectionToNext;
			var wireCount = component.RailCount;
			var row = new Rect(rect.x + LayoutPadding, rect.y + LayoutPadding,
				rect.width - LayoutPadding * 2f, LayoutLineHeight);
			var nextDisplayIndex = component.GetLayoutDisplayIndex(nextLayoutIndex);
			EditorGUI.LabelField(row, $"Transition to Layout {nextDisplayIndex + 1}",
				EditorStyles.boldLabel);

			row.y += LayoutLineHeight + 3f;
			if (TransitionUsesBlending(component, layoutIndex, BlendedWireIndices)) {
				var label = BlendedWireIndices.Count == 1 ? "Wire" : "Wires";
				var blendInfoHeight = GetTransitionBlendInfoHeight();
				EditorGUI.HelpBox(new Rect(row.x, row.y, row.width,
					blendInfoHeight),
					$"Blending {label} {string.Join(", ", BlendedWireIndices)}"
						+ TransitionBlendMessageSuffix, MessageType.Info);
				row.y += blendInfoHeight + 3f;
			}
			const float overrideButtonWidth = 24f;
			var overrideButtonsWidth = overrideButtonWidth * wireCount;
			EditorGUI.LabelField(new Rect(row.x, row.y,
				math.max(0f, row.width - overrideButtonsWidth - 4f), LayoutLineHeight),
				new GUIContent("Override Wires:",
					"Choose which wires need non-default continuity or transition curves."));
			var overrideButtonX = row.xMax - overrideButtonsWidth;
			for (var wireIndex = 0; wireIndex < wireCount; wireIndex++) {
				var canTransition = layout.IsRailActive(wireIndex)
					&& nextLayout.IsRailActive(wireIndex);
				var style = wireCount == 1 ? EditorStyles.miniButton
					: wireIndex == 0 ? EditorStyles.miniButtonLeft
					: wireIndex == wireCount - 1 ? EditorStyles.miniButtonRight
					: EditorStyles.miniButtonMid;
				var overrideRect = new Rect(overrideButtonX + wireIndex * overrideButtonWidth,
					row.y, overrideButtonWidth, LayoutLineHeight);
				var overridden = connection.IsWireOverridden(wireIndex);
				bool toggledOverride;
				using (new EditorGUI.DisabledScope(!canTransition)) {
					toggledOverride = GUI.Toggle(overrideRect, overridden,
						new GUIContent((wireIndex + 1).ToString(), canTransition
							? "Override this wire's transition."
							: "This wire is inactive in one or both layouts."), style);
				}
				if (toggledOverride != overridden) {
					var capturedWireIndex = wireIndex;
					Edit(component, "Change Wire Rail Transition Override",
						() => component.SetWireTransitionOverride(layoutIndex,
							capturedWireIndex, toggledOverride));
					GUI.changed = true;
					return;
				}
			}

			const float wireLabelWidth = 54f;
			const float continuousWidth = 88f;
			const float spacing = 4f;
			for (var wireIndex = 0; wireIndex < wireCount; wireIndex++) {
				if (!layout.IsRailActive(wireIndex) || !nextLayout.IsRailActive(wireIndex)
					|| !connection.IsWireOverridden(wireIndex)) {
					continue;
				}
				row.y += LayoutLineHeight + 3f;
				EditorGUI.LabelField(new Rect(row.x, row.y, wireLabelWidth, LayoutLineHeight),
					$"Wire {wireIndex + 1}", EditorStyles.boldLabel);
				var continuous = connection.IsWireContinuous(wireIndex);
				var continuousRect = new Rect(row.x + wireLabelWidth, row.y,
					continuousWidth, LayoutLineHeight);
				var toggled = EditorGUI.ToggleLeft(continuousRect, new GUIContent("Continuous",
					"Join this wire to the matching wire in the next layout."), continuous);
				if (toggled != continuous) {
					var capturedWireIndex = wireIndex;
					Edit(component, "Change Wire Rail Continuity",
						() => component.SetWireContinuous(layoutIndex,
							capturedWireIndex, toggled));
					connection = component.Segments[layoutIndex].ConnectionToNext;
				}
				var curveRect = new Rect(continuousRect.xMax + spacing, row.y,
					math.max(20f, row.xMax - continuousRect.xMax - spacing), LayoutLineHeight);
				using (new EditorGUI.DisabledScope(!toggled)) {
					EditorGUI.BeginChangeCheck();
					var curve = EditorGUI.CurveField(curveRect, new GUIContent(string.Empty,
						"Transition curve for this wire."),
						CloneCurve(connection.GetWireCurve(wireIndex)), TransitionCurveColor,
						new Rect(0f, 0f, 1f, 1f));
					if (EditorGUI.EndChangeCheck()) {
						var capturedWireIndex = wireIndex;
						Edit(component, "Change Wire Rail Transition Curve",
							() => component.SetWireTransitionCurve(layoutIndex,
								capturedWireIndex, curve));
						connection = component.Segments[layoutIndex].ConnectionToNext;
					}
				}
			}
		}

		private static bool TransitionUsesBlending(WireRailComponent component,
			int layoutIndex, List<int> blendedWires = null)
		{
			blendedWires?.Clear();
			var nextLayoutIndex = component.GetNextSegmentIndex(layoutIndex);
			if (nextLayoutIndex < 0) {
				return false;
			}
			var layout = component.Segments[layoutIndex];
			var nextLayout = component.Segments[nextLayoutIndex];
			var connection = layout.ConnectionToNext;
			for (var wireIndex = 0; wireIndex < component.RailCount; wireIndex++) {
				if (!layout.IsRailActive(wireIndex) || !nextLayout.IsRailActive(wireIndex)
					|| !connection.IsWireContinuous(wireIndex)) {
					continue;
				}
				var offsetDelta = (float2)layout.GetRailOffset(wireIndex)
					- (float2)nextLayout.GetRailOffset(wireIndex);
				if (math.lengthsq(offsetDelta) > 1e-8f
					|| math.abs(layout.GetWireDiameter(wireIndex)
						- nextLayout.GetWireDiameter(wireIndex)) > 1e-4f) {
					if (blendedWires == null) {
						return true;
					}
					blendedWires.Add(wireIndex + 1);
				}
			}
			return blendedWires != null && blendedWires.Count > 0;
		}

		private static float GetTransitionBlendInfoHeight()
		{
			// Reserve the HelpBox info icon as well as the reorderable-list insets.
			var availableWidth = math.max(60f,
				EditorGUIUtility.currentViewWidth - 140f);
			return math.ceil(EditorStyles.helpBox.CalcHeight(
				TransitionBlendHeightContent, availableWidth));
		}

		private static AnimationCurve CloneCurve(AnimationCurve source)
		{
			if (source == null) {
				return AnimationCurve.Linear(0f, 0f, 1f, 1f);
			}
			return new AnimationCurve(source.keys) {
				preWrapMode = source.preWrapMode,
				postWrapMode = source.postWrapMode,
			};
		}

		private int GetSelectedLayoutIndex(WireRailComponent component)
		{
			var selectedDisplayIndex = _layoutOrderList?.index ?? -1;
			if (selectedDisplayIndex < 0 || selectedDisplayIndex >= _layoutOrder.Count) {
				return -1;
			}
			var layoutIndex = _layoutOrder[selectedDisplayIndex];
			return layoutIndex >= 0 && layoutIndex < component.Segments.Count
				? layoutIndex : -1;
		}

		private void AddOrDuplicateLayout(WireRailComponent component, int sourceLayoutIndex)
		{
			var distance = component.GetSuggestedLayoutDistance(sourceLayoutIndex);
			var newLayoutIndex = -1;
			if (sourceLayoutIndex >= 0) {
				Edit(component, "Duplicate Wire Rail Layout",
					() => newLayoutIndex = component.DuplicateLayout(sourceLayoutIndex, distance));
			} else {
				Edit(component, "Add Wire Rail Layout",
					() => newLayoutIndex = component.AddLayout(distance));
			}
			SynchronizeLayoutOrder(_layoutOrder, component, true);
			if (sourceLayoutIndex >= 0) {
				SelectLayout(component, newLayoutIndex);
			} else {
				DeselectLayout();
			}
		}

		private void SelectLayout(WireRailComponent component, int layoutIndex)
		{
			if (layoutIndex < 0 || layoutIndex >= component.Segments.Count) {
				return;
			}
			WireRailLayoutEditorSelection.Select(component, layoutIndex);
			_layoutOrderList.index = component.GetLayoutDisplayIndex(layoutIndex);
			Repaint();
			SceneView.RepaintAll();
		}

		private void DeselectLayout()
		{
			_layoutOrderList.index = -1;
			_layoutSelectionBeforePointerDown = -1;
			WireRailLayoutEditorSelection.Clear();
			Repaint();
			SceneView.RepaintAll();
		}

		private static void Edit(WireRailComponent component, string undoName, Action edit)
		{
			Undo.RecordObject(component, undoName);
			edit();
			EditorUtility.SetDirty(component);
			PrefabUtility.RecordPrefabInstancePropertyModifications(component);
			SceneView.RepaintAll();
		}

		private static void CenterPivot(WireRailComponent component)
		{
			var container = component.SplineContainer;
			if (!container) {
				return;
			}
			const string undoName = "Center Wire Rail Pivot";
			Undo.RecordObjects(new UnityEngine.Object[] { component.transform, container },
				undoName);
			if (!component.CenterPivot()) {
				return;
			}
			EditorUtility.SetDirty(component.transform);
			EditorUtility.SetDirty(container);
			PrefabUtility.RecordPrefabInstancePropertyModifications(component.transform);
			PrefabUtility.RecordPrefabInstancePropertyModifications(container);
			SceneView.RepaintAll();
		}

		internal static bool TryGetGradeSplineRange(SplineContainer container,
			out int startKnotIndex, out int endKnotIndex, out int selectedKnotCount)
		{
			startKnotIndex = 0;
			endKnotIndex = container && container.Spline != null
				? container.Spline.Count - 1 : -1;
			selectedKnotCount = 0;
			var spline = container ? container.Spline : null;
			if (spline == null || spline.Closed || spline.Count < 2) {
				return false;
			}

			var splineInfo = new SplineInfo(container, 0);
			SplineSelection.GetElements(splineInfo, SelectedGradeKnots);
			SplineSelection.GetElements(splineInfo, SelectedGradeTangents);
			SelectedGradeKnotIndices.Clear();
			for (var index = 0; index < SelectedGradeKnots.Count; index++) {
				AddSelectedGradeKnotIndex(SelectedGradeKnots[index].KnotIndex);
			}
			for (var index = 0; index < SelectedGradeTangents.Count; index++) {
				AddSelectedGradeKnotIndex(SelectedGradeTangents[index].KnotIndex);
			}
			SelectedGradeKnotIndices.Sort();
			selectedKnotCount = SelectedGradeKnotIndices.Count;
			if (selectedKnotCount == 0) {
				return true;
			}
			if (selectedKnotCount != 2) {
				return false;
			}

			startKnotIndex = SelectedGradeKnotIndices[0];
			endKnotIndex = SelectedGradeKnotIndices[1];
			return true;
		}

		private static void AddSelectedGradeKnotIndex(int knotIndex)
		{
			if (!SelectedGradeKnotIndices.Contains(knotIndex)) {
				SelectedGradeKnotIndices.Add(knotIndex);
			}
		}

		internal static bool GradeSplineHeights(WireRailComponent component,
			int startKnotIndex, int endKnotIndex)
		{
			var container = component ? component.SplineContainer : null;
			var spline = container ? container.Spline : null;
			if (spline == null || spline.Closed || spline.Count < 2
				|| startKnotIndex < 0 || endKnotIndex >= spline.Count
				|| startKnotIndex >= endKnotIndex) {
				return false;
			}

			var rangeKnotCount = endKnotIndex - startKnotIndex + 1;
			var knotDistances = new float[rangeKnotCount];
			for (var segmentIndex = startKnotIndex; segmentIndex < endKnotIndex;
				segmentIndex++) {
				var rangeIndex = segmentIndex - startKnotIndex;
				knotDistances[rangeIndex + 1] = knotDistances[rangeIndex]
					+ CalculatePlanarLength(spline.GetCurve(segmentIndex));
			}
			var totalDistance = knotDistances[^1];
			if (totalDistance <= 1e-5f) {
				return false;
			}

			var originalKnots = new BezierKnot[rangeKnotCount];
			var knots = new BezierKnot[rangeKnotCount];
			var tangentModes = new TangentMode[rangeKnotCount];
			var startHeight = spline[startKnotIndex].Position.z;
			var endHeight = spline[endKnotIndex].Position.z;
			var grade = (endHeight - startHeight) / totalDistance;
			for (var knotIndex = startKnotIndex; knotIndex <= endKnotIndex; knotIndex++) {
				var rangeIndex = knotIndex - startKnotIndex;
				var knot = spline[knotIndex];
				originalKnots[rangeIndex] = knot;
				var ratio = knotDistances[rangeIndex] / totalDistance;
				knot.Position.z = math.lerp(startHeight, endHeight, ratio);
				tangentModes[rangeIndex] = spline.GetTangentMode(knotIndex);
				if (tangentModes[rangeIndex] != TangentMode.Linear) {
					if (knotIndex > startKnotIndex || startKnotIndex == 0) {
						knot.TangentIn = GradeTangent(knot.TangentIn, knot.Rotation, grade,
							false);
					}
					if (knotIndex < endKnotIndex || endKnotIndex == spline.Count - 1) {
						knot.TangentOut = GradeTangent(knot.TangentOut, knot.Rotation, grade,
							true);
					}
				}
				knots[rangeIndex] = knot;
			}

			const string undoName = "Grade Wire Rail Heights";
			Undo.RecordObjects(new UnityEngine.Object[] { component, container }, undoName);
			for (var knotIndex = startKnotIndex; knotIndex <= endKnotIndex; knotIndex++) {
				var rangeIndex = knotIndex - startKnotIndex;
				var mainTangent = knotIndex == endKnotIndex
					? BezierTangent.In : BezierTangent.Out;
				var hasUngradedTangent = (knotIndex == startKnotIndex && startKnotIndex > 0)
					|| (knotIndex == endKnotIndex && endKnotIndex < spline.Count - 1);
				var tangentMode = tangentModes[rangeIndex];
				var requiresBoundaryBreak = hasUngradedTangent
					&& RequiresBoundaryBreak(originalKnots, knots, rangeIndex,
						tangentMode);
				if (tangentMode == TangentMode.AutoSmooth) {
					if (requiresBoundaryBreak) {
						spline.SetTangentModeNoNotify(knotIndex, TangentMode.Broken,
							mainTangent);
					} else if (!hasUngradedTangent) {
						spline.SetTangentModeNoNotify(knotIndex, TangentMode.Continuous,
							mainTangent);
					}
				} else if (requiresBoundaryBreak && (tangentMode == TangentMode.Continuous
					|| tangentMode == TangentMode.Mirrored)) {
					spline.SetTangentModeNoNotify(knotIndex, TangentMode.Broken,
						mainTangent);
				}
				if (knotIndex == endKnotIndex) {
					spline.SetKnot(knotIndex, knots[rangeIndex], mainTangent);
				} else {
					spline.SetKnotNoNotify(knotIndex, knots[rangeIndex], mainTangent);
				}
			}

			EditorUtility.SetDirty(container);
			EditorUtility.SetDirty(component);
			PrefabUtility.RecordPrefabInstancePropertyModifications(container);
			PrefabUtility.RecordPrefabInstancePropertyModifications(component);
			SceneView.RepaintAll();
			return true;
		}

		private static bool RequiresBoundaryBreak(IReadOnlyList<BezierKnot> originalKnots,
			IReadOnlyList<BezierKnot> gradedKnots, int rangeIndex, TangentMode tangentMode)
		{
			var original = originalKnots[rangeIndex];
			var graded = gradedKnots[rangeIndex];
			if (!Approximately(original.Position, graded.Position)
				|| !Approximately(original.TangentIn, graded.TangentIn)
				|| !Approximately(original.TangentOut, graded.TangentOut)) {
				return true;
			}
			if (tangentMode != TangentMode.AutoSmooth) {
				return false;
			}
			var insideRangeIndex = rangeIndex == 0 ? 1 : rangeIndex - 1;
			return !Approximately(originalKnots[insideRangeIndex].Position,
				gradedKnots[insideRangeIndex].Position);

			static bool Approximately(float3 left, float3 right)
				=> math.distancesq(left, right) <= 1e-10f;
		}

		private static float CalculatePlanarLength(BezierCurve curve)
		{
			curve.P0.z = 0f;
			curve.P1.z = 0f;
			curve.P2.z = 0f;
			curve.P3.z = 0f;
			return CurveUtility.CalculateLength(curve, PlanarSplineLengthResolution);
		}

		private static float3 GradeTangent(float3 tangent, quaternion rotation,
			float grade, bool outgoing)
		{
			var splineTangent = math.rotate(rotation, tangent);
			var horizontalLength = math.length(splineTangent.xy);
			splineTangent.z = (outgoing ? grade : -grade) * horizontalLength;
			return math.rotate(math.inverse(rotation), splineTangent);
		}

		internal static void EditSpline(SplineContainer container)
		{
			if (!container) {
				return;
			}
			_pendingSplineEdit = container;
			Selection.activeGameObject = container.gameObject;
			EditorApplication.update -= ActivateSplineTool;
			EditorApplication.update += ActivateSplineTool;
		}

		private static void ActivateSplineTool()
		{
			EditorApplication.update -= ActivateSplineTool;
			var container = _pendingSplineEdit;
			_pendingSplineEdit = null;
			if (!container || Selection.activeGameObject != container.gameObject) {
				return;
			}

			ActiveEditorTracker.sharedTracker.ForceRebuild();
			ToolManager.SetActiveContext<SplineToolContext>();
			ToolManager.SetActiveTool<SplineMoveTool>();
			SceneView.RepaintAll();
		}
	}

	internal static class WireRailLayoutEditorSelection
	{
		private static WireRailComponent _component;
		private static int _layoutIndex = -1;

		internal static void Select(WireRailComponent component, int layoutIndex)
		{
			_component = component;
			_layoutIndex = component ? layoutIndex : -1;
		}

		internal static bool IsSelected(WireRailComponent component, int layoutIndex)
			=> component && component == _component
				&& layoutIndex == _layoutIndex;

		internal static void Clear()
		{
			_component = null;
			_layoutIndex = -1;
			SceneView.RepaintAll();
		}
	}

	[InitializeOnLoad]
	internal static class WireRailScenePreview
	{
		private const int SamplesPerSegment = 24;
		private static readonly Color[] RailColors = {
			new(0.05f, 0.75f, 1f, 0.95f),
			new(1f, 0.55f, 0.05f, 0.95f),
			new(0.45f, 1f, 0.2f, 0.95f),
			new(1f, 0.2f, 0.65f, 0.95f),
			new(0.65f, 0.35f, 1f, 0.95f),
		};

		static WireRailScenePreview()
		{
			SceneView.duringSceneGui += OnSceneGUI;
			SplineSelection.changed += SceneView.RepaintAll;
		}

		private static void OnSceneGUI(SceneView _)
		{
			var selected = Selection.activeGameObject;
			var component = selected ? selected.GetComponent<WireRailComponent>() : null;
			component ??= selected ? selected.GetComponentInParent<WireRailComponent>() : null;
			if (!component) {
				return;
			}

			var container = component.SplineContainer;
			if (!container) {
				return;
			}
			component.SynchronizeSegments();
			DrawEditPanel(component, container);
			if (Event.current.type != EventType.Repaint) {
				return;
			}
			var spline = container.Spline;
			if (spline == null || spline.Count < 2) {
				return;
			}
			var editing = Selection.activeGameObject == container.gameObject
				&& ToolManager.activeContextType == typeof(SplineToolContext);
			var evaluationContext = new WireRailPathEvaluationContext();

			for (var segmentIndex = 0; segmentIndex < component.Segments.Count; segmentIndex++) {
				var segment = component.Segments[segmentIndex];
				var selectedLayout = WireRailLayoutEditorSelection.IsSelected(component,
					segmentIndex);
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					if (!segment.IsRailActive(railIndex)) {
						continue;
					}
					var points = new Vector3[SamplesPerSegment + 1];
					for (var sampleIndex = 0; sampleIndex <= SamplesPerSegment; sampleIndex++) {
						var curveT = sampleIndex / (float)SamplesPerSegment;
						points[sampleIndex] = WireRailSplineGeometry.TryEvaluateRailPosition(spline,
							component.Segments, evaluationContext, segmentIndex, railIndex, curveT,
							out var position)
							? container.transform.TransformPoint((Vector3)position)
							: container.transform.position;
					}
					if (selectedLayout) {
						var previousRailZTest = Handles.zTest;
						Handles.zTest = CompareFunction.Always;
						Handles.color = new Color(0.02f, 0.02f, 0.02f, 0.95f);
						Handles.DrawAAPolyLine(7f, points);
						Handles.color = RailColors[railIndex % RailColors.Length];
						Handles.DrawAAPolyLine(4f, points);
						Handles.zTest = previousRailZTest;
					} else {
						Handles.color = RailColors[railIndex % RailColors.Length];
						Handles.DrawAAPolyLine(3f, points);
					}
				}

				var spinePoints = new Vector3[SamplesPerSegment + 1];
				for (var sampleIndex = 0; sampleIndex <= SamplesPerSegment; sampleIndex++) {
					var curveT = sampleIndex / (float)SamplesPerSegment;
					spinePoints[sampleIndex] = EvaluateWorldPosition(container, spline,
						component.Segments, segmentIndex, curveT);
				}
				var previousZTest = Handles.zTest;
				Handles.zTest = CompareFunction.Always;
				Handles.color = new Color(0.02f, 0.02f, 0.02f, 0.95f);
				Handles.DrawAAPolyLine(selectedLayout ? 13f : editing ? 11f : 8f,
					spinePoints);
				Handles.color = selectedLayout
					? new Color(1f, 0.55f, 0.05f, 1f)
					: editing
					? new Color(1f, 0.78f, 0.05f, 1f)
					: new Color(0.9f, 0.95f, 1f, 1f);
				Handles.DrawAAPolyLine(selectedLayout ? 6f : editing ? 5f : 4f,
					spinePoints);
				Handles.zTest = previousZTest;

				Handles.color = Color.white;
				var labelPosition = EvaluateWorldPosition(container, spline, component.Segments,
					segmentIndex, 0f);
				var activeRailCount = CountActiveRails(segment);
				var displayIndex = GetDisplayIndex(component.LayoutDisplayOrder, segmentIndex);
				var selectedPrefix = selectedLayout ? "▶ " : string.Empty;
				Handles.Label(labelPosition, $"{selectedPrefix}Layout {displayIndex + 1}: {activeRailCount}/"
					+ $"{component.RailCount} rails", EditorStyles.boldLabel);
			}
			DrawFixturePreviews(component, container, spline);

			if (component.ShowColliderPreview) {
				DrawColliderPreview(component.ColliderMesh, container.transform);
			}
		}

		private static int CountActiveRails(WireRailSegment segment)
		{
			var count = 0;
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				if (segment.IsRailActive(railIndex)) {
					count++;
				}
			}
			return count;
		}

		private static int GetDisplayIndex(IReadOnlyList<int> displayOrder, int layoutIndex)
		{
			for (var displayIndex = 0; displayIndex < displayOrder.Count; displayIndex++) {
				if (displayOrder[displayIndex] == layoutIndex) {
					return displayIndex;
				}
			}
			return layoutIndex;
		}

		private static void DrawFixturePreviews(WireRailComponent component,
			SplineContainer container, Spline spline)
		{
			for (var fixtureIndex = 0; fixtureIndex < component.Fixtures.Count; fixtureIndex++) {
				if (component.Fixtures[fixtureIndex] is WireRailVBraceFixture vBrace) {
					if (WireRailSplineGeometry.TryEvaluateVBrace(spline, component.Segments,
							vBrace, out var centerlinePoints)) {
						var vBracePreviousZTest = Handles.zTest;
						Handles.zTest = CompareFunction.Always;
						DrawFixturePath(container, centerlinePoints,
							new Color(1f, 0.82f, 0.1f, 1f));
						Handles.Label(container.transform.TransformPoint(
							(Vector3)centerlinePoints[0]),
							$"V Brace {fixtureIndex + 1}", EditorStyles.boldLabel);
						Handles.zTest = vBracePreviousZTest;
					}
					continue;
				}
				if (component.Fixtures[fixtureIndex] is WireRailCrossWireFixture crossWire) {
					if (WireRailSplineGeometry.TryEvaluateCrossWire(spline,
							component.Segments, crossWire, out var start, out var end)) {
						var crossWirePoints = new[] {
							container.transform.TransformPoint((Vector3)start),
							container.transform.TransformPoint((Vector3)end),
						};
						var crossWireZTest = Handles.zTest;
						Handles.zTest = CompareFunction.Always;
						Handles.color = new Color(1f, 0.82f, 0.1f, 1f);
						Handles.DrawAAPolyLine(4f, crossWirePoints);
						Handles.Label(crossWirePoints[0], $"Cross Wire {fixtureIndex + 1}",
							EditorStyles.boldLabel);
						Handles.zTest = crossWireZTest;
					}
					continue;
				}
				if (component.Fixtures[fixtureIndex] is WireRailLegFixture leg) {
					if (WireRailSplineGeometry.TryEvaluateLeg(spline, component.Segments,
							leg, out var centerlinePoints)) {
						var legPreviousZTest = Handles.zTest;
						Handles.zTest = CompareFunction.Always;
						DrawFixturePath(container, centerlinePoints,
							new Color(1f, 0.82f, 0.1f, 1f));
						Handles.Label(container.transform.TransformPoint(
							(Vector3)centerlinePoints[0]),
							$"Leg & Foot {fixtureIndex + 1}", EditorStyles.boldLabel);
						Handles.zTest = legPreviousZTest;
					}
					continue;
				}
				if (component.Fixtures[fixtureIndex] is not WireRailBraceFixture brace
					|| !brace.TryGetVisibleArc(out var startAngle, out var sweepAngle, out _)
					|| !WireRailSplineGeometry.TryEvaluateBrace(spline, component.Segments,
						brace, out var center, out _, out var right, out var up,
						out var radius)) {
					continue;
				}
				var previewSegments = math.max(2,
					(int)math.ceil(brace.RingDensity * sweepAngle / (math.PI * 2f)));
				var points = new Vector3[previewSegments + 1];
				for (var pointIndex = 0; pointIndex <= previewSegments; pointIndex++) {
					var angle = startAngle + sweepAngle * pointIndex / previewSegments;
					var centerlineOffset = brace.EvaluateCenterlineOffset(angle, radius);
					points[pointIndex] = container.transform.TransformPoint(
						(Vector3)(center + right * centerlineOffset.x
							+ up * centerlineOffset.y));
				}
				var previousZTest = Handles.zTest;
				Handles.zTest = CompareFunction.Always;
				Handles.color = new Color(1f, 0.82f, 0.1f, 1f);
				Handles.DrawAAPolyLine(4f, points);
				Handles.Label(points[0], $"Brace {fixtureIndex + 1}",
					EditorStyles.boldLabel);
				Handles.zTest = previousZTest;
			}
		}

		private static void DrawFixturePath(SplineContainer container,
			IReadOnlyList<float3> sourcePoints, Color color)
		{
			if (sourcePoints == null || sourcePoints.Count < 2) {
				return;
			}
			var points = new Vector3[sourcePoints.Count];
			for (var pointIndex = 0; pointIndex < sourcePoints.Count; pointIndex++) {
				points[pointIndex] = container.transform.TransformPoint(
					(Vector3)sourcePoints[pointIndex]);
			}
			Handles.color = color;
			Handles.DrawAAPolyLine(4f, points);
		}

		private static void DrawEditPanel(WireRailComponent component,
			SplineContainer container)
		{
			var editing = Selection.activeGameObject == container.gameObject
				&& ToolManager.activeContextType == typeof(SplineToolContext);
			Handles.BeginGUI();
			GUILayout.BeginArea(new Rect(55f, 42f, 250f, editing ? 116f : 58f),
				GUIContent.none, GUI.skin.window);
			using (new EditorGUI.DisabledScope(editing)) {
				if (GUILayout.Button(editing ? "Editing Wire Rail Spline"
						: "Edit Wire Rail Spline", GUILayout.Height(26f))) {
					WireRailInspector.EditSpline(container);
				}
			}
			if (editing) {
				var hasGradeRange = WireRailInspector.TryGetGradeSplineRange(container,
					out var startKnotIndex, out var endKnotIndex, out var selectedKnotCount);
				var label = selectedKnotCount == 2
					? "Grade Between Selected Knots" : "Grade Heights First → Last";
				using (new EditorGUI.DisabledScope(!hasGradeRange)) {
					if (GUILayout.Button(new GUIContent(label,
							"Select no knots to grade the complete route, or exactly two knots "
								+ "to grade only the interval between them."),
						GUILayout.Height(22f))) {
						if (!WireRailInspector.GradeSplineHeights(component, startKnotIndex,
								endKnotIndex)) {
							Debug.LogWarning(
								"Cannot grade a Wire Rail spline without horizontal length.",
								component);
						}
					}
				}
				GUILayout.Label("Click knot: show position gizmo\n"
					+ "Double-click line: add knot\nDouble-click knot: remove",
					EditorStyles.miniLabel);
			}
			GUILayout.EndArea();
			Handles.EndGUI();
		}

		private static void DrawColliderPreview(Mesh mesh, Transform meshTransform)
		{
			if (!mesh || mesh.vertexCount == 0) {
				return;
			}
			var vertices = mesh.vertices;
			var indices = mesh.triangles;
			var edges = new HashSet<ulong>();
			var faces = new HashSet<(int, int, int)>();
			var previousColor = Handles.color;
			var previousZTest = Handles.zTest;
			Handles.zTest = CompareFunction.LessEqual;
			Handles.color = new Color32(0, 255, 75, 128);
			for (var i = 0; i < indices.Length; i += 3) {
				var first = indices[i];
				var second = indices[i + 1];
				var third = indices[i + 2];
				if (first > second) {
					(first, second) = (second, first);
				}
				if (second > third) {
					(second, third) = (third, second);
				}
				if (first > second) {
					(first, second) = (second, first);
				}
				// The physics mesh is two-sided. Draw each geometric triangle once,
				// independently of the winding or emission order.
				if (!faces.Add((first, second, third))) {
					continue;
				}
				Handles.DrawAAConvexPolygon(
					meshTransform.TransformPoint(vertices[indices[i]]),
					meshTransform.TransformPoint(vertices[indices[i + 1]]),
					meshTransform.TransformPoint(vertices[indices[i + 2]]));
			}
			Handles.color = new Color(0f, 1f, 75f / 255f, 0.9f);
			for (var i = 0; i < indices.Length; i += 3) {
				DrawEdge(indices[i], indices[i + 1]);
				DrawEdge(indices[i + 1], indices[i + 2]);
				DrawEdge(indices[i + 2], indices[i]);
			}
			Handles.color = previousColor;
			Handles.zTest = previousZTest;

			void DrawEdge(int first, int second)
			{
				var min = (uint)math.min(first, second);
				var max = (uint)math.max(first, second);
				var key = ((ulong)min << 32) | max;
				if (!edges.Add(key)) {
					return;
				}
				Handles.DrawLine(meshTransform.TransformPoint(vertices[first]),
					meshTransform.TransformPoint(vertices[second]), 2f);
			}
		}

		private static Vector3 EvaluateWorldPosition(SplineContainer container, Spline spline,
			IReadOnlyList<WireRailSegment> layouts, int segmentIndex, float curveT)
		{
			if (!WireRailSplineGeometry.TryEvaluateLayoutPosition(spline, layouts, segmentIndex,
					curveT, out var position)) {
				return container.transform.position;
			}
			return container.transform.TransformPoint(new Vector3(position.x,
				position.y, position.z));
		}
	}
}
