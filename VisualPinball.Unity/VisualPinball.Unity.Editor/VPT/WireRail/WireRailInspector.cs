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
using System.Linq;
using Unity.Mathematics;
using Unity.Profiling;
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
		private static readonly GUIContent[][] RailOptions = Enumerable.Range(0, 7)
			.Select(count => Enumerable.Range(1, count)
				.Select(railIndex => new GUIContent($"Rail {railIndex}"))
				.ToArray())
			.ToArray();
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
		// The list insets an element by the drag handle on the left but only by a few
		// pixels on the right; this evens out the padding inside the element frame.
		private const float ElementRightInset = 14f;
		// Extra inset of the element frame's right border.
		private const float FrameRightInset = 2f;
		private const float SolderRowsContentHeight = LayoutLineHeight;
		private const float SolderRowHeight = SolderRowsContentHeight + 3f;
		private const float HelpBoxHeight = 38f;

		/// <summary>
		/// Width of a list element's content, estimated from the inspector width so element
		/// heights can be computed before the element is laid out.
		/// </summary>
		private static float EstimatedElementContentWidth
			=> math.max(120f, EditorGUIUtility.currentViewWidth - 91f);

		private static float HelpBoxHeightFor(string message)
			=> math.max(HelpBoxHeight, EditorStyles.helpBox.CalcHeight(
				new GUIContent(message), EstimatedElementContentWidth));

		private static string GetHairpinMessage(WireRailComponent component,
			WireRailHairpinFixture hairpin, WireRailEndpoint endpoint, int firstRailIndex,
			int secondRailIndex, float railOffset, out bool warning)
		{
			var spline = component.SplineContainer ? component.SplineContainer.Spline : null;
			warning = true;
			if (spline != null && spline.Closed) {
				return "Hairpins require an open spline with a real start and end.";
			}
			if (firstRailIndex == secondRailIndex) {
				return "Select two different rails. Invalid Hairpins are not generated.";
			}
			if (railOffset >= component.SplineLength - 1e-5f) {
				return "The offset consumes the whole route. Move it back so the loop has rail to attach to; the Hairpin is not generated.";
			}
			if (component.HasRailTrimConflict(endpoint, firstRailIndex, secondRailIndex, hairpin)) {
				return "A Rail Trim shortens an attached rail at this endpoint. Remove the conflict or select different rails; the Hairpin is not generated.";
			}
			warning = false;
			return "The terminal semicircle uses Terminal Impact Material; its leads use the ordinary physics material.";
		}

		private static string GetElbowMessage(WireRailComponent component,
			WireRailElbowFixture elbow, WireRailEndpoint endpoint, int firstRailIndex,
			int secondRailIndex, float offset, out bool warning)
		{
			var spline = component.SplineContainer ? component.SplineContainer.Spline : null;
			warning = true;
			if (spline != null && spline.Closed) {
				return "Elbows require an open spline with a real start and end.";
			}
			if (firstRailIndex == secondRailIndex) {
				return "Select two different rails. Invalid Elbows are not generated.";
			}
			if (offset >= component.SplineLength - 1e-5f) {
				return "The offset consumes the whole route. Move it back so the elbow has rail to attach to; the Elbow is not generated.";
			}
			if (!component.AreEndpointRailsActive(endpoint, firstRailIndex, secondRailIndex, offset)) {
				return "Both selected rails must be active at this endpoint. Inactive Elbows are not generated.";
			}
			if (component.HasRailTrimConflict(endpoint, firstRailIndex, secondRailIndex, elbow)) {
				return "Another endpoint cutoff shortens an attached rail. Remove the conflict or select different rails; the Elbow is not generated.";
			}
			warning = false;
			return "The offset shortens the two attached rails' colliders, and two vertical faces extend the floor down at the drop point. Other Rail Cutoffs change only the visible tubes.";
		}

		private static string GetRailTrimMessage(WireRailComponent component, out bool warning)
		{
			var spline = component.SplineContainer ? component.SplineContainer.Spline : null;
			warning = spline != null && spline.Closed;
			return warning
				? "Rail Trims require an open spline with a real start and end."
				: "Zero leaves a rail unchanged. Multiple trims at one endpoint use the largest offset per rail.";
		}
		private const float FixtureScaleMinimum = 0.1f;
		private const float FixtureScaleMaximum = 4f;
		private const int PlanarSplineLengthResolution = 64;
		private static SplineContainer _pendingSplineEdit;
		private readonly WireRailCrossSectionEditor _crossSectionEditor = new();
		private readonly WireRailRingPreviewEditor _ringPreviewEditor = new();
		private readonly WireRailCradlePreviewEditor _cradlePreviewEditor = new();
		private readonly WireRailRungPreviewEditor _rungPreviewEditor = new();
		private readonly WireRailStandPreviewEditor _standPreviewEditor = new();
		private readonly WireRailElbowPreviewEditor _elbowPreviewEditor = new();
		private readonly WireRailHairpinPreviewEditor _hairpinPreviewEditor = new();
		private readonly float[] _railTrimOffsets = new float[RailCounts.Length];
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
			WireRailFixtureEditorSelection.Clear();
		}

		/// <summary>
		/// Mirrors the fixture list's selection into the static selection the Scene view
		/// reads, so the highlighted fixture always matches the highlighted panel.
		/// </summary>
		private void SynchronizeFixtureSelection(WireRailComponent component)
		{
			var displayIndex = _fixtureOrderList?.index ?? -1;
			var fixtureIndex = displayIndex >= 0 && displayIndex < _fixtureOrder.Count
				? _fixtureOrder[displayIndex] : -1;
			if (fixtureIndex < 0 || fixtureIndex >= component.Fixtures.Count) {
				if (WireRailFixtureEditorSelection.GetSelectedIndex(component) >= 0) {
					WireRailFixtureEditorSelection.Clear();
				}
				return;
			}
			if (!WireRailFixtureEditorSelection.IsSelected(component, fixtureIndex)) {
				WireRailFixtureEditorSelection.Select(component, fixtureIndex);
				SceneView.RepaintAll();
			}
		}

		private void OnUndoRedo()
		{
			WireRailLayoutEditorSelection.Clear();
			WireRailFixtureEditorSelection.Clear();
			if (_layoutOrderList != null) {
				_layoutOrderList.index = -1;
			}
			if (_fixtureOrderList != null) {
				_fixtureOrderList.index = -1;
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
				SynchronizeFixtureSelection(component);
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
			EditorGUILayout.Space(4f);
			_showBallChannelCollider = EditorGUILayout.BeginFoldoutHeaderGroup(
				_showBallChannelCollider, "Ball Channel Collider");
			if (_showBallChannelCollider) {
				DrawBallChannelColliderSettings(component);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			if (component.ColliderGeometryDirty) {
				EditorGUILayout.HelpBox(
					"Collider validation is pending. Expand Ball Channel Collider or enable its "
						+ "Scene preview to validate the current rail geometry.", MessageType.Info);
			} else if (!string.IsNullOrEmpty(component.GenerationError)) {
				EditorGUILayout.HelpBox(component.GenerationError, MessageType.Error);
			}

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
					"Duplicate the selected layout halfway to the next layout, or halfway to the end of the route.")
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
				"Supports are positioned by distance along the complete spline; end fittings "
					+ "attach to its start or end. Both are independent from wire layouts.",
				EditorStyles.wordWrappedMiniLabel);
			SynchronizeOrder(_fixtureOrder, component.Fixtures.Count);
			_fixtureOrderList.DoLayoutList();
			var splineLength = component.SplineLength;
			var spline = component.SplineContainer
				? component.SplineContainer.Spline : null;
			var canAddSupport = splineLength > 0f;
			var canAddRailPairSupport = canAddSupport && component.RailCount >= 2;
			var isOpenRoute = canAddSupport && spline != null && !spline.Closed;
			var canAddRailPairFitting = isOpenRoute && component.RailCount >= 2;

			EditorGUILayout.LabelField("Supports", EditorStyles.miniBoldLabel);
			DrawAddFixtureButtons(component, new[] {
				new AddFixtureButton("Ring", Icons.WireRailRing,
					"A ring around the wire bundle, fitted to the wires at its position.",
					canAddSupport, "Add Wire Rail Ring",
					() => component.AddRingFixture(splineLength * 0.5f)),
				new AddFixtureButton("Cradle", Icons.WireRailCradle,
					"A bottom wire with two angled arms that holds the rail from below.",
					canAddSupport, "Add Wire Rail Cradle",
					() => component.AddCradleFixture(splineLength * 0.5f)),
				new AddFixtureButton("Rung", Icons.WireRailRung,
					"A straight wire between the two bottom rails.",
					canAddRailPairSupport, "Add Wire Rail Rung",
					() => component.AddRungFixture(splineLength * 0.5f)),
				new AddFixtureButton("Stand", Icons.WireRailStand,
					"A leg from the bottom rails down to a U-hook foot on the playfield.",
					canAddRailPairSupport, "Add Wire Rail Stand",
					() => component.AddStandFixture(splineLength * 0.5f)),
			});

			EditorGUILayout.LabelField("End Fittings", EditorStyles.miniBoldLabel);
			DrawAddFixtureButtons(component, new[] {
				new AddFixtureButton("Hairpin", Icons.WireRailHairpin,
					"Join two rails at an endpoint with a terminal loop.",
					canAddRailPairFitting, "Add Wire Rail Hairpin",
					() => component.AddHairpinFixture()),
				new AddFixtureButton("Elbow", Icons.WireRailElbow,
					"Bend two endpoint rails vertically down toward a hole.",
					canAddRailPairFitting, "Add Wire Rail Elbow",
					() => component.AddElbowFixture()),
				new AddFixtureButton("Rail Trim", Icons.WireRailTrim,
					"Independently move each rail start or end inward along the route.",
					isOpenRoute, "Add Wire Rail Trim",
					() => component.AddRailTrimFixture()),
			});
		}

		private readonly struct AddFixtureButton
		{
			public readonly string Label;
			public readonly Texture2D Icon;
			public readonly string Tooltip;
			public readonly bool Enabled;
			public readonly string UndoName;
			public readonly Action Add;

			public AddFixtureButton(string label, Texture2D icon, string tooltip, bool enabled,
				string undoName, Action add)
			{
				Label = label;
				Icon = icon;
				Tooltip = tooltip;
				Enabled = enabled;
				UndoName = undoName;
				Add = add;
			}
		}

		private const float AddFixtureButtonWidth = 108f;
		private const float AddFixtureButtonHeight = 124f;
		private const float AddFixtureButtonSpacing = 4f;
		private const float AddFixtureButtonMargins = 40f;
		private static GUIStyle _addFixtureButtonStyle;

		private static GUIStyle AddFixtureButtonStyle => _addFixtureButtonStyle ??= new GUIStyle(GUI.skin.button) {
			alignment = TextAnchor.LowerCenter,
			imagePosition = ImagePosition.ImageAbove,
			padding = new RectOffset(6, 6, 6, 6),
		};

		/// <summary>
		/// Draws fixed-size icon buttons, wrapping into as many rows as the inspector width needs.
		/// </summary>
		private void DrawAddFixtureButtons(WireRailComponent component,
			IReadOnlyList<AddFixtureButton> buttons)
		{
			var availableWidth = EditorGUIUtility.currentViewWidth - AddFixtureButtonMargins;
			var perRow = math.max(1, (int)math.floor((availableWidth + AddFixtureButtonSpacing)
				/ (AddFixtureButtonWidth + AddFixtureButtonSpacing)));
			for (var rowStart = 0; rowStart < buttons.Count; rowStart += perRow) {
				using (new EditorGUILayout.HorizontalScope()) {
					var rowEnd = math.min(buttons.Count, rowStart + perRow);
					for (var index = rowStart; index < rowEnd; index++) {
						var button = buttons[index];
						bool clicked;
						using (new EditorGUI.DisabledScope(!button.Enabled)) {
							clicked = GUILayout.Button(
								new GUIContent(button.Label, button.Icon, button.Tooltip),
								AddFixtureButtonStyle,
								GUILayout.Width(AddFixtureButtonWidth),
								GUILayout.Height(AddFixtureButtonHeight));
						}
						if (clicked) {
							Edit(component, button.UndoName, button.Add);
							SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
						}
					}
					GUILayout.FlexibleSpace();
				}
			}
		}

		/// <summary>
		/// Frames each list element with a bordered box and a header band, so it is obvious
		/// where one layout or fixture ends and the next begins. The selected element keeps
		/// Unity's selection tint underneath.
		/// </summary>
		private static void DrawListElementFrame(Rect rect, int index, bool active, bool focused)
		{
			if (Event.current.type != EventType.Repaint) {
				return;
			}
			rect.y -= 2f;
			rect.height += 2f;
			var frame = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f - FrameRightInset,
				rect.height - 4f);
			var pro = EditorGUIUtility.isProSkin;
			if (active) {
				ReorderableList.defaultBehaviours.DrawElementBackground(frame, index, true,
					focused, true);
			} else {
				EditorGUI.DrawRect(frame,
					pro ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0.035f));
			}
			var header = new Rect(frame.x, frame.y, frame.width, LayoutPadding + LayoutLineHeight);
			EditorGUI.DrawRect(header,
				pro ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.06f));
			var border = pro ? new Color(0f, 0f, 0f, 0.6f) : new Color(0f, 0f, 0f, 0.3f);
			EditorGUI.DrawRect(new Rect(frame.x, frame.y, frame.width, 1f), border);
			EditorGUI.DrawRect(new Rect(frame.x, frame.yMax - 1f, frame.width, 1f), border);
			EditorGUI.DrawRect(new Rect(frame.x, frame.y, 1f, frame.height), border);
			EditorGUI.DrawRect(new Rect(frame.xMax - 1f, frame.y, 1f, frame.height), border);
		}

		private ReorderableList CreateFixtureOrderList()
		{
			var list = new ReorderableList(_fixtureOrder, typeof(int), true, false, false, false) {
				headerHeight = 0f,
				footerHeight = 0f,
			};
			list.drawElementBackgroundCallback = DrawListElementFrame;
			list.elementHeightCallback = index => {
				if (target is not WireRailComponent component || index >= _fixtureOrder.Count) {
					return LayoutLineHeight;
				}
				var fixtureIndex = _fixtureOrder[index];
				return fixtureIndex >= 0 && fixtureIndex < component.Fixtures.Count
					? GetFixtureElementHeight(component, component.Fixtures[fixtureIndex])
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
			list.drawElementBackgroundCallback = DrawListElementFrame;
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

		private static float GetFixtureElementHeight(WireRailComponent component,
			WireRailFixture fixture)
		{
			var componentRailCount = component.RailCount;
			return fixture switch {
				WireRailRingFixture => LayoutPadding * 2f + LayoutLineHeight * 8f
					+ WireRailRingPreviewEditor.Height + 25f + SolderRowHeight,
				WireRailCradleFixture => LayoutPadding * 2f + LayoutLineHeight * 10f
					+ WireRailCradlePreviewEditor.Height + 35f + SolderRowHeight,
				WireRailRungFixture => LayoutPadding * 2f + LayoutLineHeight * 5f
					+ WireRailRungPreviewEditor.Height + 25f + SolderRowHeight,
				WireRailStandFixture => LayoutPadding * 2f + LayoutLineHeight * 16f
					+ (GetVector3FieldHeight() - LayoutLineHeight) * 3f
					+ WireRailStandPreviewEditor.Height + 55f + SolderRowHeight,
				// Hairpin and Drop are endpoint fittings that WireRailSolderMeshGenerator
				// never solders, so they carry no solder-threshold row.
				WireRailHairpinFixture hairpin => LayoutPadding * 2f
					+ (LayoutLineHeight + 3f) * 10f + WireRailHairpinPreviewEditor.Height + 6f
					+ HelpBoxHeightFor(GetHairpinMessage(component, hairpin, hairpin.Endpoint,
						hairpin.FirstRailIndex, hairpin.SecondRailIndex, hairpin.RailOffset, out _)),
				WireRailElbowFixture elbow => LayoutPadding * 2f
					+ (LayoutLineHeight + 3f) * (componentRailCount + 8)
					+ WireRailElbowPreviewEditor.Height + 6f
					+ HelpBoxHeightFor(GetElbowMessage(component, elbow, elbow.Endpoint,
						elbow.FirstRailIndex, elbow.SecondRailIndex, elbow.Offset, out _)),
				WireRailTrimFixture => LayoutPadding * 2f
					+ (LayoutLineHeight + 3f) * (componentRailCount + 2)
					+ HelpBoxHeightFor(GetRailTrimMessage(component, out _)),
				_ => LayoutLineHeight * 2f,
			};
		}

		private void DrawFixtureElement(Rect rect, WireRailComponent component,
			int fixtureIndex)
		{
			rect.y -= 1f;
			rect.height -= 1f;
			var content = new Rect(rect.x + LayoutPadding, rect.y + LayoutPadding,
				rect.width - LayoutPadding * 2f - ElementRightInset, rect.height - LayoutPadding * 2f);

			// Per-fixture "Enabled" toggle, drawn in the header row to the left of the
			// duplicate/trash icons so it applies uniformly to every fixture type. Disabling
			// only hides the fixture from the render mesh; colliders are unaffected.
			var enabledRect = new Rect(content.xMax - LayoutLineHeight * 2f - 6f - 72f,
				content.y - 2f, 72f, LayoutLineHeight);
			EditorGUI.BeginChangeCheck();
			var fixtureEnabled = EditorGUI.ToggleLeft(enabledRect,
				new GUIContent("Enabled",
					"Uncheck to hide this fixture from rendering. Colliders are unaffected."),
				component.Fixtures[fixtureIndex].Enabled);
			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Toggle Wire Rail Fixture",
					() => component.SetFixtureEnabled(fixtureIndex, fixtureEnabled));
			}

			if (component.Fixtures[fixtureIndex] is WireRailRingFixture ring) {
				DrawRingFixtureElement(content, component, fixtureIndex, ring);
			} else if (component.Fixtures[fixtureIndex] is WireRailCradleFixture cradle) {
				DrawCradleFixtureElement(content, component, fixtureIndex, cradle);
			} else if (component.Fixtures[fixtureIndex] is WireRailRungFixture rung) {
				DrawRungFixtureElement(content, component, fixtureIndex, rung);
			} else if (component.Fixtures[fixtureIndex] is WireRailStandFixture leg) {
				DrawStandFixtureElement(content, component, fixtureIndex, leg);
			} else if (component.Fixtures[fixtureIndex] is WireRailHairpinFixture hairpin) {
				DrawHairpinFixtureElement(content, component, fixtureIndex, hairpin);
			} else if (component.Fixtures[fixtureIndex] is WireRailElbowFixture elbow) {
				DrawElbowFixtureElement(content, component, fixtureIndex, elbow);
			} else if (component.Fixtures[fixtureIndex] is WireRailTrimFixture railTrim) {
				DrawRailTrimFixtureElement(content, component, fixtureIndex, railTrim);
				return;
			} else {
				EditorGUI.HelpBox(content, $"Fixture {fixtureIndex + 1} has an unsupported type.",
					MessageType.Warning);
				return;
			}

			// Endpoint fittings (Drop, Hairpin) are never soldered, so they get no row.
			if (component.Fixtures[fixtureIndex] is WireRailElbowFixture
				or WireRailHairpinFixture) {
				return;
			}
			// Rings keep their Apply to All button as the very last row, so their solder
			// line moves up by one row.
			var solderBottomInset = component.Fixtures[fixtureIndex] is WireRailRingFixture
				? LayoutLineHeight + 3f : 0f;
			DrawSolderRow(new Rect(content.x,
				content.yMax - SolderRowsContentHeight - solderBottomInset,
				content.width, LayoutLineHeight), component, fixtureIndex);
		}

		/// <summary>
		/// One line: a "Solder" caption, then the threshold and the size fields.
		/// </summary>
		private static void DrawSolderRow(Rect rect, WireRailComponent component,
			int fixtureIndex)
		{
			const float captionWidth = 52f;
			const float spacing = 8f;
			var fixture = component.Fixtures[fixtureIndex];
			EditorGUI.LabelField(new Rect(rect.x, rect.y, captionWidth, rect.height),
				new GUIContent("Solder:", "Blobs where this fixture touches a rail."));
			var fieldsWidth = rect.width - captionWidth - spacing;
			var sizeRect = new Rect(rect.x + captionWidth, rect.y,
				fieldsWidth * 0.44f, rect.height);
			var thresholdRect = new Rect(sizeRect.xMax + spacing, rect.y,
				fieldsWidth * 0.56f - spacing, rect.height);
			var previousLabelWidth = EditorGUIUtility.labelWidth;

			EditorGUIUtility.labelWidth = 64f;
			EditorGUI.BeginChangeCheck();
			var solderThreshold = math.max(0f, EditorGUI.DelayedFloatField(thresholdRect,
				new GUIContent("Threshold",
					"Maximum surface gap in VPX units at which this fixture is soldered to a rail."),
				fixture.SolderThreshold));
			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail Solder Threshold", () =>
					component.SetFixtureSolderThreshold(fixtureIndex, solderThreshold));
			}

			EditorGUIUtility.labelWidth = 32f;
			EditorGUI.BeginChangeCheck();
			var solderSize = math.max(0.01f, EditorGUI.DelayedFloatField(sizeRect,
				new GUIContent("Size",
					"Uniform scale of this fixture's solder blobs. 1 is the default size; "
					+ "doubling it produces eight times the volume."),
				fixture.SolderSize));
			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail Solder Size", () =>
					component.SetFixtureSolderSize(fixtureIndex, solderSize));
			}
			EditorGUIUtility.labelWidth = previousLabelWidth;
		}

		private void DrawHairpinFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailHairpinFixture hairpin)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Hairpin {fixtureIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this hairpin",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this hairpin",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Hairpin",
					() => component.DuplicateHairpinFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Hairpin",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var endpoint = (WireRailEndpoint)EditorGUI.EnumPopup(row,
				new GUIContent("Endpoint", "Spline end where the fitting is attached."),
				hairpin.Endpoint);

			var railNames = RailOptions[component.RailCount];
			row.y += LayoutLineHeight + 3f;
			var firstRailIndex = EditorGUI.Popup(row,
				new GUIContent("Rail A", "First attached rail."),
				math.clamp(hairpin.FirstRailIndex, 0, component.RailCount - 1), railNames);

			row.y += LayoutLineHeight + 3f;
			var secondRailIndex = EditorGUI.Popup(row,
				new GUIContent("Rail B", "Second attached rail."),
				math.clamp(hairpin.SecondRailIndex, 0, component.RailCount - 1), railNames);

			row.y += LayoutLineHeight + 3f;
			var ringDensity = EditorGUI.IntSlider(row, new GUIContent("Ring Density",
				"Longitudinal sampling density around the complete circular loop."),
				hairpin.RingDensity, 4, 128);

			row.y += LayoutLineHeight + 3f;
			var railOffset = math.max(0f, EditorGUI.FloatField(row, new GUIContent("Offset",
				"How far the loop sits back from the endpoint along the rails. The two connected "
				+ "rails shorten to follow it, so the loop stays attached."),
				hairpin.RailOffset));

			row.y += LayoutLineHeight + 3f;
			var loopDiameter = EditorGUI.DelayedFloatField(row,
				new GUIContent("Loop Diameter",
					"Centerline diameter of the terminal semicircle."),
				hairpin.LoopDiameter);

			row.y += LayoutLineHeight + 3f;
			var leadLength = EditorGUI.DelayedFloatField(row,
				new GUIContent("Lead Length",
					"Distance the loop center extends beyond the spline endpoint."),
				hairpin.LeadLength);

			row.y += LayoutLineHeight + 3f;
			var tangentLength = EditorGUI.DelayedFloatField(row,
				new GUIContent("Tangent Length",
					"Bezier handle length used to blend each rail into the loop."),
				hairpin.TangentLength);

			row.y += LayoutLineHeight + 3f;
			var rotation = EditorGUI.Slider(row, new GUIContent("Rotation",
				"Rotate the loop diameter around the spline tangent."),
				hairpin.Rotation, 0f, 360f);

			row.y += LayoutLineHeight + 3f;
			var loopPreviewRect = new Rect(content.x, row.y, content.width,
				WireRailHairpinPreviewEditor.Height);
			_hairpinPreviewEditor.Draw(loopPreviewRect, component, fixtureIndex, hairpin);
			row.y = loopPreviewRect.yMax + 6f;
			var message = GetHairpinMessage(component, hairpin, endpoint, firstRailIndex,
				secondRailIndex, railOffset, out var warning);
			EditorGUI.HelpBox(new Rect(row.x, row.y, row.width, HelpBoxHeightFor(message)),
				message, warning ? MessageType.Warning : MessageType.Info);

			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail Hairpin", () =>
					component.SetHairpinFixtureProperties(fixtureIndex, endpoint,
						firstRailIndex, secondRailIndex, loopDiameter, leadLength,
						tangentLength, ringDensity, railOffset, rotation));
			}
		}

		private void DrawElbowFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailElbowFixture elbow)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Elbow {fixtureIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this elbow",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this elbow",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Elbow",
					() => component.DuplicateElbowFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Elbow",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var endpoint = (WireRailEndpoint)EditorGUI.EnumPopup(row,
				new GUIContent("Endpoint", "Spline end where the fitting is attached."),
				elbow.Endpoint);

			var railNames = RailOptions[component.RailCount];
			row.y += LayoutLineHeight + 3f;
			var firstRailIndex = EditorGUI.Popup(row,
				new GUIContent("Rail A", "First rail that continues into the hole."),
				math.clamp(elbow.FirstRailIndex, 0, component.RailCount - 1), railNames);

			row.y += LayoutLineHeight + 3f;
			var secondRailIndex = EditorGUI.Popup(row,
				new GUIContent("Rail B", "Second rail that continues into the hole."),
				math.clamp(elbow.SecondRailIndex, 0, component.RailCount - 1), railNames);

			row.y += LayoutLineHeight + 3f;
			var offset = math.max(0f, EditorGUI.DelayedFloatField(row,
				new GUIContent("Offset",
					"Moves the elbow inward from the spline endpoint, shortening the two rails. "
					+ "Zero bends at the endpoint."),
				elbow.Offset));

			row.y += LayoutLineHeight + 3f;
			var dropLength = math.max(0.1f, EditorGUI.DelayedFloatField(row,
				new GUIContent("Drop Length",
					"Vertical length from the bend down into the hole."),
				elbow.DropLength));

			row.y += LayoutLineHeight + 3f;
			var zAngle = EditorGUI.DelayedFloatField(row, new GUIContent("Z Angle",
				"Rotate the horizontal leg around the route-local vertical axis."),
				elbow.ZAngle);

			row.y += LayoutLineHeight + 3f;
			EditorGUI.LabelField(row, "Other Rail Cutoffs", EditorStyles.boldLabel);
			for (var railIndex = 0; railIndex < component.RailCount; railIndex++) {
				row.y += LayoutLineHeight + 3f;
				var attached = railIndex == firstRailIndex || railIndex == secondRailIndex;
				var currentOffset = railIndex < elbow.RailCount
					? elbow.GetRailOffset(railIndex) : 0f;
				using (new EditorGUI.DisabledScope(attached)) {
					var edited = EditorGUI.DelayedFloatField(row,
						new GUIContent($"Rail {railIndex + 1}" + (attached
								? " (attached)" : string.Empty),
							attached
								? "Attached rails continue into the elbow and are not cut off."
								: "Distance measured inward from the selected endpoint."),
						attached ? 0f : currentOffset);
					_railTrimOffsets[railIndex] = attached ? 0f : math.max(0f, edited);
				}
			}

			row.y += LayoutLineHeight + 3f;
			var dropPreviewRect = new Rect(content.x, row.y, content.width,
				WireRailElbowPreviewEditor.Height);
			_elbowPreviewEditor.Draw(dropPreviewRect, component, fixtureIndex, elbow);
			row.y = dropPreviewRect.yMax + 6f;
			var message = GetElbowMessage(component, elbow, endpoint, firstRailIndex,
				secondRailIndex, offset, out var warning);
			EditorGUI.HelpBox(new Rect(row.x, row.y, row.width, HelpBoxHeightFor(message)),
				message, warning ? MessageType.Warning : MessageType.Info);

			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail Elbow", () =>
					component.SetElbowFixtureProperties(fixtureIndex, endpoint,
						firstRailIndex, secondRailIndex, offset, dropLength,
						zAngle, _railTrimOffsets));
			}
		}

		private void DrawRailTrimFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailTrimFixture railTrim)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Rail Trim {fixtureIndex + 1}",
				EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this rail trim",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this rail trim",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Trim",
					() => component.DuplicateRailTrimFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Trim",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var endpoint = (WireRailEndpoint)EditorGUI.EnumPopup(row,
				new GUIContent("Endpoint", "Spline end whose rails are shortened."),
				railTrim.Endpoint);
			for (var railIndex = 0; railIndex < component.RailCount; railIndex++) {
				row.y += LayoutLineHeight + 3f;
				var currentOffset = railIndex < railTrim.RailCount
					? railTrim.GetRailOffset(railIndex) : 0f;
				_railTrimOffsets[railIndex] = math.max(0f, EditorGUI.DelayedFloatField(row,
					new GUIContent($"Rail {railIndex + 1}",
						"Distance measured inward from the selected spline endpoint."),
					currentOffset));
			}

			row.y += LayoutLineHeight + 3f;
			var message = GetRailTrimMessage(component, out var warning);
			EditorGUI.HelpBox(new Rect(row.x, row.y, row.width, HelpBoxHeightFor(message)),
				message, warning ? MessageType.Warning : MessageType.Info);

			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail Trim", () =>
					component.SetRailTrimFixtureProperties(fixtureIndex, endpoint,
						_railTrimOffsets));
			}
		}

		private void DrawCradleFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailCradleFixture cradle)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Cradle {fixtureIndex + 1}",
				EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this cradle",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this cradle",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Cradle",
					() => component.DuplicateCradleFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Cradle",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var distance = EditorGUI.Slider(row, new GUIContent("Position",
				"Distance along the complete spline in VPX units."), cradle.Distance,
				0f, math.max(0f, component.SplineLength));

			row.y += LayoutLineHeight + 3f;
			var ringDensity = EditorGUI.IntSlider(row, new GUIContent("Ring Density",
				"Minimum sampling density for the rounded arm corners. "
					+ "A 15° safety limit adds rings when needed to preserve wire thickness."),
				cradle.RingDensity, 3, 128);

			row.y += LayoutLineHeight + 4f;
			var previewRect = new Rect(content.x, row.y, content.width,
				WireRailCradlePreviewEditor.Height);
			_cradlePreviewEditor.Draw(previewRect, component, fixtureIndex, cradle);

			row.y = previewRect.yMax + 4f;
			var lateralOffset = cradle.LateralOffset;
			var verticalOffset = cradle.VerticalOffset;
			DrawFixtureOffsetRow(row, ref lateralOffset, ref verticalOffset,
				out var resetOffset);
			if (resetOffset) {
				Edit(component, "Reset Wire Rail Cradle Offset", () =>
					component.SetCradleFixtureProperties(fixtureIndex, cradle.Distance,
						cradle.RingDensity, 0f, 0f, cradle.BottomLength,
						cradle.LeftLength, cradle.RightLength,
						cradle.Angle, cradle.Rotation, cradle.CornerRadius));
				GUIUtility.ExitGUI();
			}

			row.y += LayoutLineHeight + 3f;
			var bottomLength = EditorGUI.DelayedFloatField(row,
				new GUIContent("Bottom Length",
					"Length of the always-present straight bottom wire. Positive arms may "
						+ "raise the minimum enough to fit their rounded corners."),
				cradle.BottomLength);

			row.y += LayoutLineHeight + 3f;
			var leftLength = EditorGUI.DelayedFloatField(row,
				new GUIContent("Left Arm Length",
					"Length from the left end of the bottom wire. Set to zero to omit it; "
						+ "positive values are clamped to fit the rounded corner."),
				cradle.LeftLength);

			row.y += LayoutLineHeight + 3f;
			var rightLength = EditorGUI.DelayedFloatField(row,
				new GUIContent("Right Arm Length",
					"Length from the right end of the bottom wire. Set to zero to omit it; "
						+ "positive values are clamped to fit the rounded corner."),
				cradle.RightLength);

			row.y += LayoutLineHeight + 3f;
			var angle = EditorGUI.Slider(row, new GUIContent("Arm Angle",
				"Included angle between the left and right arm directions."),
				cradle.Angle, 1f, 179f);

			row.y += LayoutLineHeight + 3f;
			var rotation = EditorGUI.Slider(row, new GUIContent("Rotation",
				"Rotate the complete fixture around the spline tangent."),
				cradle.Rotation, 0f, 360f);

			row.y += LayoutLineHeight + 3f;
			var cornerRadius = EditorGUI.FloatField(row, new GUIContent("Corner Radius",
				"Requested centerline radius where each non-zero arm meets the bottom wire."),
				cradle.CornerRadius);

			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail Cradle", () =>
					component.SetCradleFixtureProperties(fixtureIndex, distance,
						ringDensity, lateralOffset, verticalOffset, bottomLength,
						leftLength, rightLength, angle, rotation,
						cornerRadius));
			}
		}

		private void DrawRingFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailRingFixture ring)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Ring {fixtureIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this ring",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this ring",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Ring",
					() => component.DuplicateRingFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Ring",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var distance = EditorGUI.Slider(row, new GUIContent("Position",
				"Distance along the complete spline in VPX units."), ring.Distance,
				0f, math.max(0f, component.SplineLength));

			row.y += LayoutLineHeight + 3f;
			var scale = EditorGUI.Slider(row, new GUIContent("Scale",
				"Multiplier for the automatically fitted ring radius."), ring.Scale,
				FixtureScaleMinimum, FixtureScaleMaximum);

			row.y += LayoutLineHeight + 3f;
			var ringDensity = EditorGUI.IntSlider(row, new GUIContent("Ring Density",
				"Number of tube segments around the complete ring."),
				ring.RingDensity, 3, 128);

			row.y += LayoutLineHeight + 4f;
			var previewRect = new Rect(content.x, row.y, content.width,
				WireRailRingPreviewEditor.Height);
			_ringPreviewEditor.Draw(previewRect, component, fixtureIndex, ring);

			row.y = previewRect.yMax + 4f;
			var lateralOffset = ring.LateralOffset;
			var verticalOffset = ring.VerticalOffset;
			DrawFixtureOffsetRow(row, ref lateralOffset, ref verticalOffset,
				out var resetOffset);
			if (resetOffset) {
				Edit(component, "Reset Wire Rail Ring Offset", () =>
					component.SetRingFixtureProperties(fixtureIndex, ring.Distance,
						ring.HasCutout, ring.CutoutStartAngle, ring.CutoutEndAngle,
						ring.HasStraightSection, ring.StraightStartAngle,
						ring.StraightEndAngle, 0f, 0f, ring.Scale,
						ring.RingDensity));
				GUIUtility.ExitGUI();
			}

			row.y += LayoutLineHeight + 3f;
			var cutoutStart = ring.CutoutStartAngle;
			var cutoutEnd = ring.CutoutEndAngle;
			var hasCutout = DrawAngleRange(row, "Cutout",
				"Remove the angular range from the ring.", ring.HasCutout,
				ref cutoutStart, ref cutoutEnd);

			row.y += LayoutLineHeight + 3f;
			var straightStart = ring.StraightStartAngle;
			var straightEnd = ring.StraightEndAngle;
			var hasStraightSection = DrawAngleRange(row, "Straight Line",
				"Replace the angular range with a straight chord.", ring.HasStraightSection,
				ref straightStart, ref straightEnd);

			var propertiesChanged = EditorGUI.EndChangeCheck();
			// Last row of the panel; DrawFixtureElement puts the solder line above it.
			row.y = content.yMax - LayoutLineHeight;
			var hasOtherRing = HasOtherRingFixture(component, fixtureIndex);
			var applyToAll = false;
			using (new EditorGUI.DisabledScope(!hasOtherRing)) {
				applyToAll = GUI.Button(row, new GUIContent("Apply to All",
					"Copy every setting except Position from this ring to all other rings."));
			}
			if (applyToAll) {
				Edit(component, "Apply Wire Rail Ring Settings to All", () => {
					if (propertiesChanged) {
						component.SetRingFixtureProperties(fixtureIndex, distance,
							hasCutout, cutoutStart, cutoutEnd,
							hasStraightSection, straightStart, straightEnd,
							lateralOffset, verticalOffset, scale, ringDensity);
					}
					component.ApplyRingPropertiesToAll(fixtureIndex);
				});
				GUIUtility.ExitGUI();
			}
			if (propertiesChanged) {
				Edit(component, "Edit Wire Rail Ring", () =>
					component.SetRingFixtureProperties(fixtureIndex, distance,
						hasCutout, cutoutStart, cutoutEnd,
						hasStraightSection, straightStart, straightEnd,
						lateralOffset, verticalOffset, scale, ringDensity));
			}
		}

		private void DrawRungFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailRungFixture rung)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Rung {fixtureIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this rung",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this rung",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Rung",
					() => component.DuplicateRungFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Rung",
					() => component.RemoveFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}

			EditorGUI.BeginChangeCheck();
			row.y = content.y + LayoutLineHeight + 3f;
			var distance = EditorGUI.Slider(row, new GUIContent("Position",
				"Distance along the complete spline in VPX units."), rung.Distance,
				0f, math.max(0f, component.SplineLength));

			row.y += LayoutLineHeight + 4f;
			var previewRect = new Rect(content.x, row.y, content.width,
				WireRailRungPreviewEditor.Height);
			_rungPreviewEditor.Draw(previewRect, component, fixtureIndex, rung);

			row.y = previewRect.yMax + 4f;
			var lateralOffset = rung.LateralOffset;
			var verticalOffset = rung.VerticalOffset;
			DrawFixtureOffsetRow(row, ref lateralOffset, ref verticalOffset,
				out var resetOffset);
			if (resetOffset) {
				Edit(component, "Reset Wire Rail Rung Offset", () =>
					component.SetRungFixtureProperties(fixtureIndex, rung.Distance,
						rung.Angle, 0f, 0f,
						rung.LengthAdjustment));
				GUIUtility.ExitGUI();
			}

			row.y += LayoutLineHeight + 3f;
			var angle = EditorGUI.Slider(row, new GUIContent("Angle",
				"Rotation around the spline tangent. 0° is horizontal along local X; "
				+ "90° is vertical along local Z."), rung.Angle, 0f, 360f);

			row.y += LayoutLineHeight + 3f;
			var lengthAdjustment = EditorGUI.FloatField(row, new GUIContent("Length",
				"Signed VPX adjustment to the span between the bottom rails. Positive values "
				+ "extend the wire; negative values shorten it."), rung.LengthAdjustment);

			if (EditorGUI.EndChangeCheck()) {
				Edit(component, "Edit Wire Rail Rung", () =>
					component.SetRungFixtureProperties(fixtureIndex, distance,
						angle, lateralOffset, verticalOffset,
						lengthAdjustment));
			}
		}

		private void DrawStandFixtureElement(Rect content, WireRailComponent component,
			int fixtureIndex, WireRailStandFixture leg)
		{
			var row = new Rect(content.x, content.y - 2f, content.width, LayoutLineHeight);
			EditorGUI.LabelField(row, $"Stand {fixtureIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var duplicateRect = new Rect(trashRect.x - LayoutLineHeight - 2f, row.y,
				LayoutLineHeight, LayoutLineHeight);
			var duplicate = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Duplicate")) {
				tooltip = "Duplicate this stand",
			};
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this stand",
			};
			EditorGUIUtility.AddCursorRect(duplicateRect, MouseCursor.Link);
			EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			if (GUI.Button(duplicateRect, duplicate, GUIStyle.none)) {
				Edit(component, "Duplicate Wire Rail Stand",
					() => component.DuplicateStandFixture(fixtureIndex));
				SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
				GUIUtility.ExitGUI();
			}
			if (GUI.Button(trashRect, trash, GUIStyle.none)) {
				Edit(component, "Remove Wire Rail Stand",
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
				WireRailStandPreviewEditor.Height);
			_standPreviewEditor.Draw(previewRect, component, fixtureIndex, leg);

			row.y = previewRect.yMax + 4f;
			EditorGUI.LabelField(row, "Rail Attachment", EditorStyles.boldLabel);

			row.y += LayoutLineHeight + 3f;
			var lateralOffset = leg.LateralOffset;
			var verticalOffset = leg.VerticalOffset;
			DrawFixtureOffsetRow(row, ref lateralOffset, ref verticalOffset,
				out var resetOffset);
			if (resetOffset) {
				Edit(component, "Reset Wire Rail Stand Attachment Offset", () =>
					component.SetStandFixtureProperties(fixtureIndex, leg.Distance, leg.LegSide,
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
			const float mirrorButtonWidth = 58f;
			var sideRect = new Rect(row.x, row.y,
				row.width - mirrorButtonWidth - 4f, row.height);
			var mirrorRect = new Rect(sideRect.xMax + 4f, row.y,
				mirrorButtonWidth, row.height);
			var legSide = (WireRailStandSide)EditorGUI.EnumPopup(sideRect,
				new GUIContent("Side", "End of the bottom-rail attachment where the leg begins."),
				leg.LegSide);
			if (GUI.Button(mirrorRect, new GUIContent("Mirror",
					"Reflect the complete stand to the opposite route-local side."))) {
				Edit(component, "Mirror Wire Rail Stand",
					() => component.MirrorStandFixture(fixtureIndex));
				GUIUtility.ExitGUI();
			}

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
			var footClockwise = EditorGUI.Toggle(row,
				new GUIContent("Clockwise",
					"Reverse the U-hook winding direction around its bend."),
				leg.FootClockwise);

			row.y += LayoutLineHeight + 3f;
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
				Edit(component, "Edit Wire Rail Stand", () =>
					component.SetStandFixtureProperties(fixtureIndex, distance, legSide,
						startDirection, startLength, footPosition, footRotation,
						footWidth, footLength, footConnectionLength, lateralOffset,
						verticalOffset, lengthAdjustment, footClockwise));
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
				var aligned = WireRailRingFixture.AlignAngleRangeHorizontally(start, end);
				start = aligned.x;
				end = aligned.y;
			}
		}

		private static GUIContent AlignAngleRangeContent
			=> _alignAngleRangeContent ??= new GUIContent(string.Empty,
				"Align both endpoints to the same vertical height.");

		private static bool HasOtherRingFixture(WireRailComponent component,
			int sourceFixtureIndex)
		{
			for (var fixtureIndex = 0; fixtureIndex < component.Fixtures.Count;
				fixtureIndex++) {
				if (fixtureIndex != sourceFixtureIndex
					&& component.Fixtures[fixtureIndex] is WireRailRingFixture) {
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
				component.RebuildRenderGeometry();
				SceneView.RepaintAll();
			}

			var renderMesh = component.RenderMesh;
			if (renderMesh) {
				EditorGUILayout.LabelField("Generated", $"{renderMesh.vertexCount} vertices");
			}
			if (GUILayout.Button("Rebuild Render Geometry")) {
				component.RebuildRenderGeometry();
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
			var colliderGeometryChanged = EditorGUI.EndChangeCheck();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_showColliderPreview"),
				new GUIContent("Show Collider Preview"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_physicsMaterial"),
				new GUIContent("Physics Material"));
			EditorGUILayout.PropertyField(
				serializedObject.FindProperty("_terminalPhysicsMaterial"),
				new GUIContent("Terminal Impact Material",
					"Optional physics material used only by Hairpin terminal arcs. It takes precedence even when Overwrite Physics is enabled."));
			var overwritePhysics = serializedObject.FindProperty("_overwritePhysics");
			EditorGUILayout.PropertyField(overwritePhysics, new GUIContent("Overwrite Physics",
				"Use the inline values for the channel and Hairpin leads. A Terminal Impact Material remains a deliberate terminal-only override."));
			if (overwritePhysics.boolValue) {
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_elasticity"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_elasticityFalloff"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_friction"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_scatter"));
			}
			var otherSettingsChanged = EditorGUI.EndChangeCheck();
			serializedObject.ApplyModifiedProperties();
			if (colliderGeometryChanged) {
				component.InvalidateColliderGeometry();
			}
			if (colliderGeometryChanged || otherSettingsChanged) {
				SceneView.RepaintAll();
			}

			var colliderMesh = component.ColliderMesh;
			if (colliderMesh && colliderMesh.subMeshCount > 0) {
				var triangleCount = 0UL;
				for (var submeshIndex = 0; submeshIndex < colliderMesh.subMeshCount;
					submeshIndex++) {
					triangleCount += colliderMesh.GetIndexCount(submeshIndex) / 3;
				}
				EditorGUILayout.LabelField("Generated", $"{triangleCount} triangles");
			}
			if (GUILayout.Button("Rebuild Collider")) {
				component.RebuildColliderMesh();
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
				rect.width - LayoutPadding * 2f - ElementRightInset, rect.height - LayoutPadding * 2f);
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
				var wasSelected = WireRailLayoutEditorSelection.IsSelected(component, layoutIndex);
				var newLayoutIndex = layoutIndex;
				Edit(component, "Move Wire Rail Layout",
					() => newLayoutIndex = component.SetLayoutDistance(layoutIndex, position));
				if (newLayoutIndex != layoutIndex) {
					// The layout moved past a neighbor and the physical list was re-sorted;
					// the display list is remapped by the component, so only the scene
					// selection, which is keyed by physical index, needs to follow.
					SynchronizeLayoutOrder(_layoutOrder, component, true);
					if (wasSelected) {
						WireRailLayoutEditorSelection.Select(component, newLayoutIndex);
					}
					GUIUtility.ExitGUI();
				}
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

	internal static class WireRailFixtureEditorSelection
	{
		private static WireRailComponent _component;
		private static int _fixtureIndex = -1;

		internal static void Select(WireRailComponent component, int fixtureIndex)
		{
			_component = component;
			_fixtureIndex = component ? fixtureIndex : -1;
		}

		internal static bool IsSelected(WireRailComponent component, int fixtureIndex)
			=> component && component == _component && fixtureIndex == _fixtureIndex;

		internal static int GetSelectedIndex(WireRailComponent component)
			=> component && component == _component ? _fixtureIndex : -1;

		internal static void Clear()
		{
			_component = null;
			_fixtureIndex = -1;
			SceneView.RepaintAll();
		}
	}

	[InitializeOnLoad]
	internal static class WireRailScenePreview
	{
		private const int SamplesPerSegment = 24;
		private static readonly ProfilerMarker ScenePreviewMarker =
			new("WireRail.ScenePreview");
		private static readonly Color[] RailColors = {
			new(0.05f, 0.75f, 1f, 0.95f),
			new(1f, 0.55f, 0.05f, 0.95f),
			new(0.45f, 1f, 0.2f, 0.95f),
			new(1f, 0.2f, 0.65f, 0.95f),
			new(0.65f, 0.35f, 1f, 0.95f),
		};
		private static readonly List<SegmentPreview> SegmentPreviews = new();
		private static readonly List<FixturePreview> FixturePreviews = new();
		private static WireRailComponent _cachedComponent;
		private static int _cachedRenderGeometryVersion = -1;
		private static Matrix4x4 _cachedLocalToWorld;
		private static Mesh _cachedColliderMesh;
		private static int _cachedColliderGeometryVersion = -1;
		private static Matrix4x4 _cachedColliderLocalToWorld;
		private static Vector3[] _cachedColliderFaces = Array.Empty<Vector3>();
		private static Vector3[] _cachedColliderEdges = Array.Empty<Vector3>();

		private sealed class SegmentPreview
		{
			public Vector3[][] RailPoints;
			public Vector3[] SpinePoints;
			public Vector3 LabelPosition;
			public int ActiveRailCount;
			public int CachedDisplayIndex = -1;
			public readonly GUIContent Label = new();
			public readonly GUIContent SelectedLabel = new();
		}

		private sealed class FixturePreview
		{
			public Vector3[] Points;
			public GUIContent Label;
			public int FixtureIndex = -1;
		}

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
			DrawEditPanel(component, container);
			var spline = container.Spline;
			if (spline == null || spline.Count < 2) {
				return;
			}
			using (ScenePreviewMarker.Auto()) {
				EnsurePreviewCache(component, container, spline);
				DrawLayoutHandles(component, container, spline);
				DrawFixtureHandles(component, container, spline);
				if (Event.current.type != EventType.Repaint) {
					return;
				}
				DrawSegmentPreviews(component, container);
				DrawSelectedSpanOverlay(component, container);
				DrawFixturePreviews(component);
				DrawSelectedFixtureOverlay(component, container);

				if (component.ShowColliderPreview) {
					DrawColliderPreview(component, component.ColliderMesh, container.transform);
				}
			}
		}

		private static void EnsurePreviewCache(WireRailComponent component,
			SplineContainer container, Spline spline)
		{
			var localToWorld = container.transform.localToWorldMatrix;
			if (_cachedComponent == component
				&& _cachedRenderGeometryVersion == component.RenderGeometryVersion
				&& _cachedLocalToWorld == localToWorld) {
				return;
			}

			_cachedComponent = component;
			_cachedRenderGeometryVersion = component.RenderGeometryVersion;
			_cachedLocalToWorld = localToWorld;
			SegmentPreviews.Clear();
			FixturePreviews.Clear();
			var evaluationContext = new WireRailPathEvaluationContext();
			for (var segmentIndex = 0; segmentIndex < component.Segments.Count;
				segmentIndex++) {
				var segment = component.Segments[segmentIndex];
				var preview = new SegmentPreview {
					RailPoints = new Vector3[segment.RailCount][],
					SpinePoints = new Vector3[SamplesPerSegment + 1],
					ActiveRailCount = CountActiveRails(segment),
				};
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					if (!segment.IsRailActive(railIndex)) {
						continue;
					}
					var points = new Vector3[SamplesPerSegment + 1];
					preview.RailPoints[railIndex] = points;
					for (var sampleIndex = 0; sampleIndex <= SamplesPerSegment; sampleIndex++) {
						var curveT = sampleIndex / (float)SamplesPerSegment;
						points[sampleIndex] = WireRailSplineGeometry.TryEvaluateRailPosition(spline,
							component.Segments, evaluationContext, segmentIndex, railIndex, curveT,
							out var position)
							? localToWorld.MultiplyPoint3x4((Vector3)position)
							: container.transform.position;
					}
				}
				for (var sampleIndex = 0; sampleIndex <= SamplesPerSegment; sampleIndex++) {
					var curveT = sampleIndex / (float)SamplesPerSegment;
					preview.SpinePoints[sampleIndex] = WireRailSplineGeometry
						.TryEvaluateLayoutPosition(spline, component.Segments, evaluationContext,
							segmentIndex, curveT, out var position)
						? localToWorld.MultiplyPoint3x4((Vector3)position)
						: container.transform.position;
				}
				preview.LabelPosition = preview.SpinePoints[0];
				SegmentPreviews.Add(preview);
			}
			BuildFixturePreviews(component, spline, localToWorld);
		}

		private static readonly Color SelectionColor = new(1f, 0.6f, 0.1f, 1f);
		private static readonly Color SelectionGlowColor = new(1f, 0.6f, 0.1f, 0.28f);
		private static readonly Color OutlineColor = new(0.02f, 0.02f, 0.02f, 0.95f);
		private static readonly Color DimmedOutlineColor = new(0.02f, 0.02f, 0.02f, 0.35f);
		private const float DimmedAlpha = 0.28f;
		private static GUIStyle _selectedLabelStyle;
		private static Texture2D _selectedLabelBackground;
		private static readonly List<(float angle, Vector3 point)> FrameScratch = new();

		private static GUIStyle SelectedLabelStyle {
			get {
				if (_selectedLabelStyle != null && _selectedLabelBackground) {
					return _selectedLabelStyle;
				}
				_selectedLabelBackground = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
					hideFlags = HideFlags.HideAndDontSave,
				};
				_selectedLabelBackground.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.05f, 0.85f));
				_selectedLabelBackground.Apply();
				_selectedLabelStyle = new GUIStyle(EditorStyles.boldLabel) {
					fontSize = 13,
					padding = new RectOffset(7, 7, 3, 3),
					normal = {
						textColor = SelectionColor,
						background = _selectedLabelBackground,
					},
				};
				return _selectedLabelStyle;
			}
		}

		private static void DrawSegmentPreviews(WireRailComponent component,
			SplineContainer container)
		{
			var editing = Selection.activeGameObject == container.gameObject
				&& ToolManager.activeContextType == typeof(SplineToolContext);
			var selectedIndex = GetSelectedLayoutIndex(component);

			// Everything that is not the selected layout steps back, so the selection
			// reads from contrast rather than from a couple of extra pixels of width.
			for (var segmentIndex = 0; segmentIndex < SegmentPreviews.Count; segmentIndex++) {
				var preview = SegmentPreviews[segmentIndex];
				var selectedLayout = segmentIndex == selectedIndex;
				var dimmed = selectedIndex >= 0 && !selectedLayout;

				if (selectedLayout) {
					DrawSelectionGlow(preview);
				}

				for (var railIndex = 0; railIndex < preview.RailPoints.Length; railIndex++) {
					var points = preview.RailPoints[railIndex];
					if (points == null) {
						continue;
					}
					var railColor = RailColors[railIndex % RailColors.Length];
					if (selectedLayout) {
						var previousRailZTest = Handles.zTest;
						Handles.zTest = CompareFunction.Always;
						Handles.color = OutlineColor;
						Handles.DrawAAPolyLine(8f, points);
						Handles.color = railColor;
						Handles.DrawAAPolyLine(5f, points);
						Handles.zTest = previousRailZTest;
					} else {
						if (dimmed) {
							railColor.a = DimmedAlpha;
						}
						Handles.color = railColor;
						Handles.DrawAAPolyLine(dimmed ? 2f : 3f, points);
					}
				}

				var previousZTest = Handles.zTest;
				Handles.zTest = CompareFunction.Always;
				Handles.color = dimmed ? DimmedOutlineColor : OutlineColor;
				Handles.DrawAAPolyLine(selectedLayout ? 13f : editing ? 11f : dimmed ? 6f : 8f,
					preview.SpinePoints);
				var spineColor = selectedLayout
					? SelectionColor
					: editing
						? new Color(1f, 0.78f, 0.05f, 1f)
						: new Color(0.9f, 0.95f, 1f, 1f);
				if (dimmed) {
					spineColor.a = DimmedAlpha;
				}
				Handles.color = spineColor;
				Handles.DrawAAPolyLine(selectedLayout ? 6f : editing ? 5f : dimmed ? 3f : 4f,
					preview.SpinePoints);
				Handles.zTest = previousZTest;

				if (selectedLayout) {
					DrawSelectionFrames(preview);
				}

				Handles.color = Color.white;
				var displayIndex = GetDisplayIndex(component.LayoutDisplayOrder, segmentIndex);
				if (preview.CachedDisplayIndex != displayIndex) {
					preview.CachedDisplayIndex = displayIndex;
					preview.Label.text = $"Layout {displayIndex + 1}: {preview.ActiveRailCount}/"
						+ $"{component.RailCount} rails";
					preview.SelectedLabel.text = "▶ " + preview.Label.text;
				}
				// Arrows sit at the start of the selected span and of the next one; the
				// labels there would only cover them up.
				var hasHandle = selectedIndex >= 0
					&& (segmentIndex == selectedIndex || segmentIndex == selectedIndex + 1);
				if (!hasHandle) {
					Handles.Label(preview.LabelPosition, preview.Label,
						dimmed ? EditorStyles.miniBoldLabel : EditorStyles.boldLabel);
				}
			}
		}

		private static int GetSelectedLayoutIndex(WireRailComponent component)
		{
			for (var segmentIndex = 0; segmentIndex < SegmentPreviews.Count; segmentIndex++) {
				if (WireRailLayoutEditorSelection.IsSelected(component, segmentIndex)) {
					return segmentIndex;
				}
			}
			return -1;
		}

		/// <summary>
		/// Move handles at the start and the end of the selected span. Dragging one slides
		/// that layout along the route; the end handle belongs to the next layout.
		/// </summary>
		private static void DrawLayoutHandles(WireRailComponent component,
			SplineContainer container, Spline spline)
		{
			var selectedIndex = GetSelectedLayoutIndex(component);
			if (selectedIndex < 0 || selectedIndex >= component.Segments.Count) {
				return;
			}
			// Layout 1 is pinned to the route start and has nothing to drag.
			if (selectedIndex > 0) {
				DrawLayoutHandle(component, container, spline, selectedIndex);
			}
			var nextIndex = selectedIndex + 1;
			if (nextIndex < component.Segments.Count && nextIndex < SegmentPreviews.Count) {
				DrawLayoutHandle(component, container, spline, nextIndex);
			}
		}

		private static void DrawLayoutHandle(WireRailComponent component,
			SplineContainer container, Spline spline, int layoutIndex)
		{
			var spine = SegmentPreviews[layoutIndex].SpinePoints;
			var tangent = spine.Length > 1 ? spine[1] - spine[0] : Vector3.forward;
			var current = component.Segments[layoutIndex].Distance;
			DrawRouteDistanceHandle(container, spline, spine[0], tangent,
				$"{current:0} units", distance => {
					var selectedIndex = GetSelectedLayoutIndex(component);
					var selectedLayout = selectedIndex >= 0
						&& selectedIndex < component.Segments.Count
						? component.Segments[selectedIndex] : null;
					Undo.RecordObject(component, "Move Wire Rail Layout");
					component.SetLayoutDistance(layoutIndex, distance);
					EditorUtility.SetDirty(component);
					// Dragging a layout past its neighbor re-sorts the physical list; keep
					// the selection on the same layout.
					if (selectedLayout == null) {
						return;
					}
					for (var index = 0; index < component.Segments.Count; index++) {
						if (component.Segments[index] == selectedLayout) {
							if (index != selectedIndex) {
								WireRailLayoutEditorSelection.Select(component, index);
							}
							break;
						}
					}
				});
		}

		/// <summary>
		/// One arrow for the selected fixture. Supports slide along the route; hairpins and
		/// elbows set how far from their endpoint they sit. Rail trims have no single
		/// position and get no handle.
		/// </summary>
		private static void DrawFixtureHandles(WireRailComponent component,
			SplineContainer container, Spline spline)
		{
			var fixtureIndex = WireRailFixtureEditorSelection.GetSelectedIndex(component);
			if (fixtureIndex < 0 || fixtureIndex >= component.Fixtures.Count) {
				return;
			}
			var fixture = component.Fixtures[fixtureIndex];
			var splineLength = component.SplineLength;
			float routeDistance;
			string hoverLabel;
			Action<float> apply;
			switch (fixture) {
				case WireRailHairpinFixture hairpin:
					routeDistance = hairpin.Endpoint == WireRailEndpoint.Start
						? hairpin.RailOffset : splineLength - hairpin.RailOffset;
					hoverLabel = $"offset {hairpin.RailOffset:0} units";
					apply = distance => {
						Undo.RecordObject(component, "Move Wire Rail Hairpin");
						component.SetHairpinFixtureOffset(fixtureIndex,
							hairpin.Endpoint == WireRailEndpoint.Start
								? distance : splineLength - distance);
						EditorUtility.SetDirty(component);
					};
					break;
				case WireRailElbowFixture elbow:
					routeDistance = elbow.Endpoint == WireRailEndpoint.Start
						? elbow.Offset : splineLength - elbow.Offset;
					hoverLabel = $"offset {elbow.Offset:0} units";
					apply = distance => {
						Undo.RecordObject(component, "Move Wire Rail Elbow");
						component.SetElbowFixtureOffset(fixtureIndex,
							elbow.Endpoint == WireRailEndpoint.Start
								? distance : splineLength - distance);
						EditorUtility.SetDirty(component);
					};
					break;
				case WireRailTrimFixture:
					return;
				default:
					routeDistance = fixture.Distance;
					hoverLabel = $"{fixture.Distance:0} units";
					apply = distance => {
						Undo.RecordObject(component, "Move Wire Rail Fixture");
						component.SetFixtureDistance(fixtureIndex, distance);
						EditorUtility.SetDirty(component);
					};
					break;
			}
			if (!TryEvaluateRoutePoint(container, spline, routeDistance, out var position,
					out var tangent)) {
				return;
			}
			DrawRouteDistanceHandle(container, spline, position, tangent, hoverLabel, apply);
		}

		private static bool TryEvaluateRoutePoint(SplineContainer container, Spline spline,
			float distance, out Vector3 position, out Vector3 tangent)
		{
			position = default;
			tangent = Vector3.forward;
			if (spline == null || spline.Count < 2) {
				return false;
			}
			var length = spline.GetLength();
			var t = length > 1e-5f
				? spline.ConvertIndexUnit(math.clamp(distance, 0f, length),
					PathIndexUnit.Distance, PathIndexUnit.Normalized)
				: 0f;
			if (!spline.Evaluate(t, out var localPosition, out var localTangent, out _)) {
				return false;
			}
			var localToWorld = container.transform.localToWorldMatrix;
			position = localToWorld.MultiplyPoint3x4((Vector3)localPosition);
			tangent = localToWorld.MultiplyVector((Vector3)localTangent);
			return true;
		}

		/// <summary>
		/// A double-headed arrow at a point on the route. Dragging it snaps to the nearest
		/// point on the spline and hands that route distance to <paramref name="applyDistance"/>.
		/// </summary>
		private static void DrawRouteDistanceHandle(SplineContainer container, Spline spline,
			Vector3 position, Vector3 tangent, string hoverLabel, Action<float> applyDistance)
		{
			if (tangent.sqrMagnitude < 1e-10f) {
				tangent = Vector3.forward;
			}
			tangent.Normalize();
			var size = HandleUtility.GetHandleSize(position) * 0.5f;
			var controlId = GUIUtility.GetControlID(FocusType.Passive);
			var hovered = HandleUtility.nearestControl == controlId
				|| GUIUtility.hotControl == controlId;
			var previousColor = Handles.color;
			var previousZTest = Handles.zTest;
			Handles.zTest = CompareFunction.Always;
			if (Event.current.type == EventType.Repaint) {
				// Repaint only: a cap drawn during Layout would register as a pickable
				// control and steal the click from the move handle below.
				Handles.color = hovered ? Color.white : OutlineColor;
				DoubleArrowCap(tangent, 0, position, Quaternion.identity, size * 1.25f,
					EventType.Repaint);
			}
			Handles.color = hovered ? new Color(1f, 0.8f, 0.35f, 1f) : SelectionColor;
			EditorGUI.BeginChangeCheck();
			var dragged = Handles.FreeMoveHandle(controlId, position, size, Vector3.zero,
				(id, capPosition, rotation, capSize, eventType) =>
					DoubleArrowCap(tangent, id, capPosition, rotation, capSize, eventType));
			if (EditorGUI.EndChangeCheck()) {
				var local = (float3)container.transform.InverseTransformPoint(dragged);
				SplineUtility.GetNearestPoint(spline, local, out _, out var t);
				var distance = spline.ConvertIndexUnit(t, PathIndexUnit.Normalized,
					PathIndexUnit.Distance);
				applyDistance(distance);
			}
			if (hovered) {
				Handles.Label(position, "    " + hoverLabel, SelectedLabelStyle);
			}
			Handles.color = previousColor;
			Handles.zTest = previousZTest;
		}

		/// <summary>
		/// A double-headed arrow along the route: two cones on a short bar. Picking treats
		/// the whole bar as the control, not only its centerline.
		/// </summary>
		private static void DoubleArrowCap(Vector3 tangent, int controlId, Vector3 position,
			Quaternion _, float size, EventType eventType)
		{
			var halfBar = size * 0.5f;
			var start = position - tangent * halfBar;
			var end = position + tangent * halfBar;
			switch (eventType) {
				case EventType.Layout:
				case EventType.MouseMove:
					// The arrow is roughly 10px thick on screen; anywhere on it counts as a hit.
					HandleUtility.AddControl(controlId,
						math.max(0f, HandleUtility.DistanceToLine(start, end) - 10f));
					break;
				case EventType.Repaint:
					var coneSize = size * 0.7f;
					Handles.ConeHandleCap(0, end, Quaternion.LookRotation(tangent), coneSize,
						EventType.Repaint);
					Handles.ConeHandleCap(0, start, Quaternion.LookRotation(-tangent), coneSize,
						EventType.Repaint);
					// Bar thickness in pixels, scaling with the cap size so the outline pass
					// (drawn slightly larger) stays visible around the colored pass.
					Handles.DrawAAPolyLine(size / HandleUtility.GetHandleSize(position) * 24f,
						start, end);
					break;
			}
		}

		private static readonly Color SpanOverlayColor = new(1f, 0.6f, 0.1f, 0.35f);
		// Push the overlay this far toward the camera so it wins the depth test against the
		// mesh it copies instead of z-fighting with it. World units, i.e. meters.
		private const float SpanOverlayDepthBias = 0.00025f;
		private static Material _spanOverlayMaterial;
		private static readonly MeshRangeOverlay SpanOverlay = new();
		private static readonly MeshRangeOverlay FixtureOverlay = new();

		private static Material SpanOverlayMaterial {
			get {
				if (_spanOverlayMaterial) {
					return _spanOverlayMaterial;
				}
				_spanOverlayMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) {
					hideFlags = HideFlags.HideAndDontSave,
				};
				_spanOverlayMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
				_spanOverlayMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
				_spanOverlayMaterial.SetInt("_Cull", (int)CullMode.Back);
				_spanOverlayMaterial.SetInt("_ZWrite", 0);
				_spanOverlayMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
				return _spanOverlayMaterial;
			}
		}

		/// <summary>
		/// Tints the rendered tubes of the selected span in the selection color, so the
		/// selection shows on the actual geometry and not only on the centerlines.
		/// </summary>
		private static void DrawSelectedSpanOverlay(WireRailComponent component,
			SplineContainer container)
		{
			var selectedIndex = GetSelectedLayoutIndex(component);
			var ranges = component.RenderSegmentIndexRanges;
			if (selectedIndex >= 0 && selectedIndex < ranges.Count) {
				SpanOverlay.Draw(component, container, ranges[selectedIndex]);
			}
		}

		private static void DrawSelectedFixtureOverlay(WireRailComponent component,
			SplineContainer container)
		{
			var fixtureIndex = WireRailFixtureEditorSelection.GetSelectedIndex(component);
			var ranges = component.RenderFixtureIndexRanges;
			if (fixtureIndex >= 0 && fixtureIndex < ranges.Count) {
				FixtureOverlay.Draw(component, container, ranges[fixtureIndex]);
			}
		}

		/// <summary>
		/// Redraws one index range of the render mesh with the selection tint. Caches the
		/// world-space triangles per geometry version, range and transform.
		/// </summary>
		private sealed class MeshRangeOverlay
		{
			private WireRailComponent _component;
			private int _version = -1;
			private int2 _range;
			private Matrix4x4 _localToWorld;
			private Vector3[] _triangles = Array.Empty<Vector3>();

			public void Draw(WireRailComponent component, SplineContainer container, int2 range)
			{
				var mesh = component.RenderMesh;
				if (!mesh || range.y < 3) {
					return;
				}
				var localToWorld = container.transform.localToWorldMatrix;
				// Keyed on the mesh generation: the geometry version already advances when a
				// rebuild is requested, while the rebuild itself is deferred in the editor.
				if (_component != component || _version != component.RenderMeshVersion
					|| !_range.Equals(range) || _localToWorld != localToWorld) {
					_component = component;
					_version = component.RenderMeshVersion;
					_range = range;
					_localToWorld = localToWorld;
					var vertices = mesh.vertices;
					var indices = mesh.triangles;
					var end = math.min(indices.Length, range.x + range.y);
					var triangles = new Vector3[math.max(0, end - range.x)];
					for (var i = range.x; i < end; i++) {
						triangles[i - range.x] = localToWorld.MultiplyPoint3x4(vertices[indices[i]]);
					}
					_triangles = triangles;
				}
				if (_triangles.Length < 3) {
					return;
				}
				var camera = Camera.current;
				var cameraPosition = camera ? camera.transform.position : Vector3.zero;
				SpanOverlayMaterial.SetPass(0);
				GL.PushMatrix();
				GL.MultMatrix(Matrix4x4.identity);
				GL.Begin(GL.TRIANGLES);
				GL.Color(SpanOverlayColor);
				foreach (var vertex in _triangles) {
					var toCamera = cameraPosition - vertex;
					var length = toCamera.magnitude;
					GL.Vertex(length > 1e-6f
						? vertex + toCamera * (SpanOverlayDepthBias / length)
						: vertex);
				}
				GL.End();
				GL.PopMatrix();
			}
		}

		/// <summary>
		/// A wide, translucent band under the selected span. Drawn on top of everything so the
		/// highlighted stretch is visible even where the rail runs behind other geometry.
		/// </summary>
		private static void DrawSelectionGlow(SegmentPreview preview)
		{
			var previousZTest = Handles.zTest;
			Handles.zTest = CompareFunction.Always;
			Handles.color = SelectionGlowColor;
			Handles.DrawAAPolyLine(26f, preview.SpinePoints);
			for (var railIndex = 0; railIndex < preview.RailPoints.Length; railIndex++) {
				var points = preview.RailPoints[railIndex];
				if (points != null) {
					Handles.DrawAAPolyLine(16f, points);
				}
			}
			Handles.zTest = previousZTest;
		}

		/// <summary>
		/// Outlines the cross-section at the start and the end of the selected span, so the
		/// author sees exactly which stretch of the route the layout governs.
		/// </summary>
		private static void DrawSelectionFrames(SegmentPreview preview)
		{
			var lastSample = preview.SpinePoints.Length - 1;
			if (lastSample < 1) {
				return;
			}
			DrawSelectionFrame(preview, 0,
				preview.SpinePoints[1] - preview.SpinePoints[0]);
			DrawSelectionFrame(preview, lastSample,
				preview.SpinePoints[lastSample] - preview.SpinePoints[lastSample - 1]);
		}

		private static void DrawSelectionFrame(SegmentPreview preview, int sampleIndex,
			Vector3 tangent)
		{
			var center = preview.SpinePoints[sampleIndex];
			if (tangent.sqrMagnitude < 1e-10f) {
				return;
			}
			tangent.Normalize();
			var reference = math.abs(Vector3.Dot(tangent, Vector3.up)) < 0.9f
				? Vector3.up : Vector3.right;
			var axisX = Vector3.Normalize(Vector3.Cross(reference, tangent));
			var axisY = Vector3.Cross(tangent, axisX);

			FrameScratch.Clear();
			for (var railIndex = 0; railIndex < preview.RailPoints.Length; railIndex++) {
				var points = preview.RailPoints[railIndex];
				if (points == null || sampleIndex >= points.Length) {
					continue;
				}
				var offset = points[sampleIndex] - center;
				var angle = math.atan2(Vector3.Dot(offset, axisY), Vector3.Dot(offset, axisX));
				FrameScratch.Add((angle, points[sampleIndex]));
			}
			if (FrameScratch.Count == 0) {
				return;
			}
			FrameScratch.Sort((a, b) => a.angle.CompareTo(b.angle));

			var previousZTest = Handles.zTest;
			Handles.zTest = CompareFunction.Always;
			var camera = Camera.current;
			var discNormal = camera ? -camera.transform.forward : tangent;
			if (FrameScratch.Count >= 2) {
				var loop = new Vector3[FrameScratch.Count + 1];
				for (var i = 0; i < FrameScratch.Count; i++) {
					loop[i] = FrameScratch[i].point;
				}
				loop[FrameScratch.Count] = FrameScratch[0].point;
				Handles.color = OutlineColor;
				Handles.DrawAAPolyLine(7f, loop);
				Handles.color = SelectionColor;
				Handles.DrawAAPolyLine(4f, loop);
			}
			foreach (var (_, point) in FrameScratch) {
				var radius = HandleUtility.GetHandleSize(point) * 0.028f;
				Handles.color = OutlineColor;
				Handles.DrawSolidDisc(point, discNormal, radius * 1.6f);
				Handles.color = Color.white;
				Handles.DrawSolidDisc(point, discNormal, radius);
			}
			Handles.zTest = previousZTest;
		}

		private static void BuildFixturePreviews(WireRailComponent component, Spline spline,
			Matrix4x4 localToWorld)
		{
			for (var fixtureIndex = 0; fixtureIndex < component.Fixtures.Count; fixtureIndex++) {
				if (component.Fixtures[fixtureIndex] is WireRailCradleFixture cradle) {
					if (WireRailSplineGeometry.TryEvaluateCradle(spline, component.Segments,
							cradle, out var points)) {
						AddFixturePreview(points, $"Cradle {fixtureIndex + 1}", fixtureIndex);
					}
					continue;
				}
				if (component.Fixtures[fixtureIndex] is WireRailRungFixture rung) {
					if (WireRailSplineGeometry.TryEvaluateRung(spline, component.Segments,
							rung, out var start, out var end)) {
						FixturePreviews.Add(new FixturePreview {
							Points = new[] {
								localToWorld.MultiplyPoint3x4((Vector3)start),
								localToWorld.MultiplyPoint3x4((Vector3)end),
							},
							Label = new GUIContent($"Rung {fixtureIndex + 1}"),
							FixtureIndex = fixtureIndex,
						});
					}
					continue;
				}
				if (component.Fixtures[fixtureIndex] is WireRailStandFixture leg) {
					if (WireRailSplineGeometry.TryEvaluateStand(spline, component.Segments, leg,
							out var points)) {
						AddFixturePreview(points, $"Stand {fixtureIndex + 1}", fixtureIndex);
					}
					continue;
				}
				if (component.Fixtures[fixtureIndex] is WireRailHairpinFixture hairpin) {
					if (WireRailSplineGeometry.TryEvaluateHairpin(spline, component.Segments,
							hairpin, out var points)) {
						AddFixturePreview(points, $"Hairpin {fixtureIndex + 1}", fixtureIndex);
					}
					continue;
				}
				if (component.Fixtures[fixtureIndex] is WireRailElbowFixture elbow) {
					if (WireRailSplineGeometry.TryEvaluateElbow(spline, component.Segments,
							elbow, out var firstRailPoints, out var secondRailPoints)) {
						AddFixturePreview(firstRailPoints, $"Elbow {fixtureIndex + 1}", fixtureIndex);
						AddFixturePreview(secondRailPoints, string.Empty, fixtureIndex);
					}
					continue;
				}
				if (component.Fixtures[fixtureIndex] is not WireRailRingFixture ring
					|| !ring.TryGetVisibleArc(out var startAngle, out var sweepAngle, out _)
					|| !WireRailSplineGeometry.TryEvaluateRing(spline, component.Segments,
						ring, out var center, out _, out var right, out var up,
						out var radius)) {
					continue;
				}
				var previewSegments = math.max(2,
					(int)math.ceil(ring.RingDensity * sweepAngle / (math.PI * 2f)));
				var ringPoints = new Vector3[previewSegments + 1];
				for (var pointIndex = 0; pointIndex <= previewSegments; pointIndex++) {
					var angle = startAngle + sweepAngle * pointIndex / previewSegments;
					var centerlineOffset = ring.EvaluateCenterlineOffset(angle, radius);
					ringPoints[pointIndex] = localToWorld.MultiplyPoint3x4(
						(Vector3)(center + right * centerlineOffset.x + up * centerlineOffset.y));
				}
				FixturePreviews.Add(new FixturePreview {
					Points = ringPoints,
					Label = new GUIContent($"Ring {fixtureIndex + 1}"),
					FixtureIndex = fixtureIndex,
				});
			}

			void AddFixturePreview(IReadOnlyList<float3> sourcePoints, string label,
				int previewFixtureIndex)
			{
				if (sourcePoints == null || sourcePoints.Count < 2) {
					return;
				}
				var points = new Vector3[sourcePoints.Count];
				for (var pointIndex = 0; pointIndex < sourcePoints.Count; pointIndex++) {
					points[pointIndex] = localToWorld.MultiplyPoint3x4(
						(Vector3)sourcePoints[pointIndex]);
				}
				FixturePreviews.Add(new FixturePreview {
					Points = points,
					Label = new GUIContent(label),
					FixtureIndex = previewFixtureIndex,
				});
			}
		}

		private static readonly Color FixtureColor = new(1f, 0.82f, 0.1f, 1f);

		private static void DrawFixturePreviews(WireRailComponent component)
		{
			var selectedFixture = WireRailFixtureEditorSelection.GetSelectedIndex(component);
			var previousColor = Handles.color;
			var previousZTest = Handles.zTest;
			Handles.zTest = CompareFunction.Always;
			foreach (var preview in FixturePreviews) {
				var selected = selectedFixture >= 0 && preview.FixtureIndex == selectedFixture;
				var dimmed = selectedFixture >= 0 && !selected;
				if (selected) {
					Handles.color = SelectionGlowColor;
					Handles.DrawAAPolyLine(16f, preview.Points);
					Handles.color = OutlineColor;
					Handles.DrawAAPolyLine(8f, preview.Points);
					Handles.color = SelectionColor;
					Handles.DrawAAPolyLine(5f, preview.Points);
				} else {
					var color = FixtureColor;
					if (dimmed) {
						color.a = DimmedAlpha;
					}
					Handles.color = color;
					Handles.DrawAAPolyLine(dimmed ? 3f : 4f, preview.Points);
				}
				if (preview.Label.text.Length > 0) {
					Handles.Label(preview.Points[0], preview.Label,
						dimmed ? EditorStyles.miniBoldLabel : EditorStyles.boldLabel);
				}
			}
			Handles.color = previousColor;
			Handles.zTest = previousZTest;
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

		private const float PanelWidth = 276f;
		private const float PanelHeightIdle = 98f;
		private const float PanelHeightEditing = 206f;
		private static GUIStyle _panelTitleStyle;
		private static GUIStyle _panelStatusDotStyle;
		private static GUIStyle _panelHintKeyStyle;

		private static GUIStyle PanelTitleStyle => _panelTitleStyle ??= new GUIStyle(EditorStyles.boldLabel) {
			fontSize = 13,
			alignment = TextAnchor.MiddleLeft,
		};

		private static GUIStyle PanelStatusDotStyle => _panelStatusDotStyle ??= new GUIStyle(EditorStyles.boldLabel) {
			normal = { textColor = new Color(0.35f, 0.85f, 0.4f) },
			alignment = TextAnchor.MiddleLeft,
		};

		private static GUIStyle PanelHintKeyStyle => _panelHintKeyStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) {
			alignment = TextAnchor.MiddleLeft,
		};

		private static void DrawEditPanel(WireRailComponent component,
			SplineContainer container)
		{
			var editing = Selection.activeGameObject == container.gameObject
				&& ToolManager.activeContextType == typeof(SplineToolContext);
			var spline = container.Spline;

			Handles.BeginGUI();
			GUILayout.BeginArea(new Rect(55f, 42f, PanelWidth,
				editing ? PanelHeightEditing : PanelHeightIdle), GUIContent.none, GUI.skin.window);

			// Title: icon, name of the thing, name of the object.
			using (new GUILayout.HorizontalScope()) {
				var iconRect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f));
				iconRect.y += 1f;
				GUI.DrawTexture(iconRect, Icons.RampWires(IconSize.Small), ScaleMode.ScaleToFit, true);
				GUILayout.Space(4f);
				GUILayout.Label("Wire Rail", PanelTitleStyle, GUILayout.Height(20f));
				GUILayout.FlexibleSpace();
				GUILayout.Label(component.name, EditorStyles.miniLabel, GUILayout.Height(20f));
			}

			// One line that says what this rail is made of.
			var layoutCount = component.Layouts.Count;
			var fixtureCount = component.Fixtures.Count;
			var summary = $"{component.RailCount} {(component.RailCount == 1 ? "rail" : "rails")}"
				+ $"  ·  {layoutCount} {(layoutCount == 1 ? "layout" : "layouts")}"
				+ $"  ·  {fixtureCount} {(fixtureCount == 1 ? "fixture" : "fixtures")}"
				+ $"  ·  {component.SplineLength:0} units"
				+ (spline != null && spline.Closed ? "  ·  closed" : string.Empty);
			GUILayout.Label(summary, EditorStyles.miniLabel);

			GUILayout.Space(3f);
			var separator = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
			EditorGUI.DrawRect(separator, new Color(0f, 0f, 0f, 0.35f));
			GUILayout.Space(5f);

			using (new GUILayout.HorizontalScope()) {
				if (editing) {
					GUILayout.Label("●", PanelStatusDotStyle, GUILayout.Width(14f), GUILayout.Height(24f));
					GUILayout.Label("Editing spline", EditorStyles.boldLabel, GUILayout.Height(24f));
					GUILayout.FlexibleSpace();
					if (GUILayout.Button(new GUIContent("Done",
							"Leave spline editing and return to the Wire Rail."),
						GUILayout.Width(64f), GUILayout.Height(24f))) {
						FinishSplineEdit(component);
					}
				} else {
					var editIcon = EditorGUIUtility.IconContent("d_editicon.sml").image;
					if (GUILayout.Button(new GUIContent(" Edit Spline", editIcon,
							"Move knots, add knots with a double-click, shape tangents."),
						GUILayout.Height(24f))) {
						WireRailInspector.EditSpline(container);
					}
					GUILayout.Space(4f);
					EditorGUI.BeginChangeCheck();
					var showCollider = GUILayout.Toggle(component.ShowColliderPreview,
						new GUIContent(" Collider", Icons.Physics(),
							"Show the generated ball channel collider."),
						GUI.skin.button, GUILayout.Width(88f), GUILayout.Height(24f));
					if (EditorGUI.EndChangeCheck()) {
						Undo.RecordObject(component, "Toggle Wire Rail Collider Preview");
						component.SetShowColliderPreview(showCollider);
					}
				}
			}

			if (editing) {
				GUILayout.Space(4f);
				var hasGradeRange = WireRailInspector.TryGetGradeSplineRange(container,
					out var startKnotIndex, out var endKnotIndex, out var selectedKnotCount);
				var label = selectedKnotCount == 2
					? " Grade Between Selected Knots" : " Grade Heights First → Last";
				using (new EditorGUI.DisabledScope(!hasGradeRange)) {
					if (GUILayout.Button(new GUIContent(label, Icons.Horizon(),
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
				GUILayout.Space(6f);
				DrawPanelHint("Click knot", "position gizmo");
				DrawPanelHint("Double-click line", "add a knot");
				DrawPanelHint("Double-click knot", "remove it");
			}

			GUILayout.EndArea();
			Handles.EndGUI();
		}

		private static void DrawPanelHint(string key, string action)
		{
			using (new GUILayout.HorizontalScope()) {
				GUILayout.Label(key, PanelHintKeyStyle, GUILayout.Width(118f), GUILayout.Height(15f));
				GUILayout.Label(action, EditorStyles.miniLabel, GUILayout.Height(15f));
			}
		}

		/// <summary>
		/// Leaves the spline tool context and hands the selection back to the Wire Rail
		/// itself, so the author lands on the component inspector rather than the child.
		/// </summary>
		private static void FinishSplineEdit(WireRailComponent component)
		{
			ToolManager.SetActiveContext<GameObjectToolContext>();
			Selection.activeGameObject = component.gameObject;
			SceneView.RepaintAll();
		}

		private static void DrawColliderPreview(WireRailComponent component, Mesh mesh,
			Transform meshTransform)
		{
			if (!mesh || mesh.vertexCount == 0) {
				return;
			}
			EnsureColliderPreviewCache(component, mesh, meshTransform.localToWorldMatrix);
			var previousColor = Handles.color;
			var previousZTest = Handles.zTest;
			Handles.zTest = CompareFunction.LessEqual;
			Handles.color = new Color32(0, 255, 75, 128);
			for (var i = 0; i < _cachedColliderFaces.Length; i += 3) {
				Handles.DrawAAConvexPolygon(
					_cachedColliderFaces[i],
					_cachedColliderFaces[i + 1],
					_cachedColliderFaces[i + 2]);
			}
			Handles.color = new Color(0f, 1f, 75f / 255f, 0.9f);
			for (var i = 0; i < _cachedColliderEdges.Length; i += 2) {
				Handles.DrawLine(_cachedColliderEdges[i], _cachedColliderEdges[i + 1], 2f);
			}
			Handles.color = previousColor;
			Handles.zTest = previousZTest;
		}

		private static void EnsureColliderPreviewCache(WireRailComponent component, Mesh mesh,
			Matrix4x4 localToWorld)
		{
			if (_cachedColliderMesh == mesh
				&& _cachedColliderGeometryVersion == component.ColliderGeometryVersion
				&& _cachedColliderLocalToWorld == localToWorld) {
				return;
			}
			_cachedColliderMesh = mesh;
			_cachedColliderGeometryVersion = component.ColliderGeometryVersion;
			_cachedColliderLocalToWorld = localToWorld;
			var vertices = mesh.vertices;
			var indices = mesh.triangles;
			var edgeKeys = new HashSet<ulong>();
			var faceKeys = new HashSet<(int, int, int)>();
			var faces = new List<Vector3>(indices.Length / 2);
			var edges = new List<Vector3>(indices.Length);
			for (var index = 0; index < indices.Length; index += 3) {
				var first = indices[index];
				var second = indices[index + 1];
				var third = indices[index + 2];
				var sortedFirst = first;
				var sortedSecond = second;
				var sortedThird = third;
				if (sortedFirst > sortedSecond) {
					(sortedFirst, sortedSecond) = (sortedSecond, sortedFirst);
				}
				if (sortedSecond > sortedThird) {
					(sortedSecond, sortedThird) = (sortedThird, sortedSecond);
				}
				if (sortedFirst > sortedSecond) {
					(sortedFirst, sortedSecond) = (sortedSecond, sortedFirst);
				}
				// The physics mesh is two-sided. Cache each geometric triangle once,
				// independently of winding or emission order.
				if (faceKeys.Add((sortedFirst, sortedSecond, sortedThird))) {
					faces.Add(localToWorld.MultiplyPoint3x4(vertices[first]));
					faces.Add(localToWorld.MultiplyPoint3x4(vertices[second]));
					faces.Add(localToWorld.MultiplyPoint3x4(vertices[third]));
				}
				AddEdge(first, second);
				AddEdge(second, third);
				AddEdge(third, first);
			}
			_cachedColliderFaces = faces.ToArray();
			_cachedColliderEdges = edges.ToArray();

			void AddEdge(int first, int second)
			{
				var min = (uint)math.min(first, second);
				var max = (uint)math.max(first, second);
				var key = ((ulong)min << 32) | max;
				if (edgeKeys.Add(key)) {
					edges.Add(localToWorld.MultiplyPoint3x4(vertices[first]));
					edges.Add(localToWorld.MultiplyPoint3x4(vertices[second]));
				}
			}
		}
	}
}
