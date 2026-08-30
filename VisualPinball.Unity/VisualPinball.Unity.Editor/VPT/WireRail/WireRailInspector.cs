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
		private static GUIContent _alignAngleRangeContent;
		private const float LayoutLineHeight = 20f;
		private const float LayoutPadding = 7f;
		private const float FixtureScaleMinimum = 0.1f;
		private const float FixtureScaleMaximum = 4f;
		private static SplineContainer _pendingSplineEdit;
		private readonly WireRailCrossSectionEditor _crossSectionEditor = new();
		private readonly WireRailBracePreviewEditor _bracePreviewEditor = new();
		[SerializeField] private bool _showRenderGeometry = true;
		[SerializeField] private bool _showFixtures = true;
		[SerializeField] private bool _showWireLayouts = true;
		private readonly List<int> _fixtureOrder = new();
		private readonly List<int> _layoutOrder = new();
		private ReorderableList _fixtureOrderList;
		private ReorderableList _layoutOrderList;

		private void OnEnable()
		{
			_fixtureOrderList = CreateFixtureOrderList();
			_layoutOrderList = CreateLayoutOrderList();
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

			if (container.Splines.Count > 1) {
				EditorGUILayout.HelpBox(
					"This first wire-rail slice uses the first spline only. Remove additional splines "
					+ "from the container before authoring wire layouts.", MessageType.Warning);
			}

			EditorGUILayout.Space(8f);
			_showRenderGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(
				_showRenderGeometry, "Render Geometry");
			if (_showRenderGeometry) {
				DrawGenerationSettings(component);
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
			SynchronizeOrder(_layoutOrder, component.Segments.Count);
			_layoutOrderList.DoLayoutList();
			if (GUILayout.Button("Add Wire Layout")) {
				Edit(component, "Add Wire Rail Layout",
					() => component.AddLayout(component.SplineLength * 0.5f));
				SynchronizeOrder(_layoutOrder, component.Segments.Count, true);
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
			using (new EditorGUI.DisabledScope(splineLength <= 0f)) {
				if (GUILayout.Button("Add Brace")) {
					Edit(component, "Add Wire Rail Brace",
						() => component.AddBraceFixture(splineLength * 0.5f));
					SynchronizeOrder(_fixtureOrder, component.Fixtures.Count, true);
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
			list.elementHeightCallback = index => GetFixtureElementHeight();
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
					SynchronizeOrder(_layoutOrder, component.Segments.Count, true);
				}
			};
			return list;
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

		private static float GetFixtureElementHeight()
			=> LayoutPadding * 2f + LayoutLineHeight * 7f
				+ WireRailBracePreviewEditor.Height + 25f;

		private void DrawFixtureElement(Rect rect, WireRailComponent component,
			int fixtureIndex)
		{
			rect.y -= 1f;
			rect.height -= 1f;
			var content = new Rect(rect.x + LayoutPadding, rect.y + LayoutPadding,
				rect.width - LayoutPadding * 2f, rect.height - LayoutPadding * 2f);
			if (component.Fixtures[fixtureIndex] is not WireRailBraceFixture brace) {
				EditorGUI.HelpBox(content, $"Fixture {fixtureIndex + 1} has an unsupported type.",
					MessageType.Warning);
				return;
			}

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
						brace.StraightEndAngle, 0f, 0f, brace.Scale));
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
							lateralOffset, verticalOffset, scale);
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
						lateralOffset, verticalOffset, scale));
			}
		}

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
			const float fieldWidth = 42f;
			const float alignButtonWidth = LayoutLineHeight;
			const float spacing = 4f;
			enabled = EditorGUI.ToggleLeft(new Rect(rect.x, rect.y, toggleWidth,
				LayoutLineHeight), new GUIContent(label, tooltip), enabled);
			if (!enabled) {
				return false;
			}

			var startRect = new Rect(rect.x + toggleWidth + spacing, rect.y, fieldWidth,
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
			if (GUI.Button(alignRect, AlignAngleRangeContent, EditorStyles.miniButton)) {
				var aligned = WireRailBraceFixture.AlignAngleRangeHorizontally(start, end);
				start = aligned.x;
				end = aligned.y;
			}
			return true;
		}

		private static GUIContent AlignAngleRangeContent
			=> _alignAngleRangeContent ??= new GUIContent(Icons.Horizon(),
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

		private void DrawGenerationSettings(WireRailComponent component)
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

			EditorGUILayout.Space(4f);
			EditorGUILayout.LabelField("Ball Channel Collider", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_referenceBallDiameter"),
				new GUIContent("Ball Diameter", "Reference ball diameter in VPX units."));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_colliderSamplesPerSegment"),
				new GUIContent("Samples Per Layout Span"));
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

			if (!string.IsNullOrEmpty(component.GenerationError)) {
				EditorGUILayout.HelpBox(component.GenerationError, MessageType.Error);
			}
			var renderMesh = component.RenderMesh;
			var colliderMesh = component.ColliderMesh;
			if (renderMesh && colliderMesh) {
				EditorGUILayout.LabelField("Generated",
					$"{renderMesh.vertexCount} render vertices, "
					+ $"{colliderMesh.triangles.Length / 3} collider triangles");
			}
			if (GUILayout.Button("Rebuild Geometry")) {
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
			EditorGUI.LabelField(row, $"Layout {layoutIndex + 1}", EditorStyles.boldLabel);
			var trashRect = new Rect(row.xMax - LayoutLineHeight, row.y, LayoutLineHeight,
				LayoutLineHeight);
			var trash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) {
				tooltip = "Remove this wire layout",
			};
			var canRemoveLayout = component.Segments.Count > 1;
			if (canRemoveLayout) {
				EditorGUIUtility.AddCursorRect(trashRect, MouseCursor.Link);
			}
			using (new EditorGUI.DisabledScope(!canRemoveLayout)) {
				if (GUI.Button(trashRect, trash, GUIStyle.none)) {
					Edit(component, "Remove Wire Rail Layout",
						() => component.RemoveLayout(layoutIndex));
					SynchronizeOrder(_layoutOrder, component.Segments.Count, true);
					GUIUtility.ExitGUI();
				}
			}

			row.y = content.y + LayoutLineHeight + 3f;
			var positionRect = new Rect(row.x, row.y, row.width, LayoutLineHeight);
			float position;
			using (new EditorGUI.DisabledScope(layoutIndex == 0)) {
				var previousLabelWidth = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = 52f;
				position = EditorGUI.DelayedFloatField(positionRect, new GUIContent("Position",
					"Distance along the complete spline in VPX units."), layout.Distance);
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
			return LayoutPadding * 2f + LayoutLineHeight * 2f + 3f
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
			EditorGUI.LabelField(row, $"Transition to Layout {nextLayoutIndex + 1}",
				EditorStyles.boldLabel);

			row.y += LayoutLineHeight + 3f;
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

	[InitializeOnLoad]
	internal static class WireRailScenePreview
	{
		private const int SamplesPerSegment = 24;
		private const int FixturePreviewSegments = 48;
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
			DrawEditPanel(container);
			var spline = container.Spline;
			if (spline == null || spline.Count < 2) {
				return;
			}
			var editing = Selection.activeGameObject == container.gameObject
				&& ToolManager.activeContextType == typeof(SplineToolContext);
			var evaluationContext = new WireRailPathEvaluationContext();

			for (var segmentIndex = 0; segmentIndex < component.Segments.Count; segmentIndex++) {
				var segment = component.Segments[segmentIndex];
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
					Handles.color = RailColors[railIndex % RailColors.Length];
					Handles.DrawAAPolyLine(3f, points);
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
				Handles.DrawAAPolyLine(editing ? 11f : 8f, spinePoints);
				Handles.color = editing
					? new Color(1f, 0.78f, 0.05f, 1f)
					: new Color(0.9f, 0.95f, 1f, 1f);
				Handles.DrawAAPolyLine(editing ? 5f : 4f, spinePoints);
				Handles.zTest = previousZTest;

				Handles.color = Color.white;
				var labelPosition = EvaluateWorldPosition(container, spline, component.Segments,
					segmentIndex, 0f);
				var activeRailCount = CountActiveRails(segment);
				Handles.Label(labelPosition, $"Layout {segmentIndex + 1}: {activeRailCount}/"
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

		private static void DrawFixturePreviews(WireRailComponent component,
			SplineContainer container, Spline spline)
		{
			for (var fixtureIndex = 0; fixtureIndex < component.Fixtures.Count; fixtureIndex++) {
				if (component.Fixtures[fixtureIndex] is not WireRailBraceFixture brace
					|| !brace.TryGetVisibleArc(out var startAngle, out var sweepAngle, out _)
					|| !WireRailSplineGeometry.TryEvaluateBrace(spline, component.Segments,
						brace, out var center, out _, out var right, out var up,
						out var radius)) {
					continue;
				}
				var points = new Vector3[FixturePreviewSegments + 1];
				for (var pointIndex = 0; pointIndex <= FixturePreviewSegments; pointIndex++) {
					var angle = startAngle + sweepAngle * pointIndex / FixturePreviewSegments;
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

		private static void DrawEditPanel(SplineContainer container)
		{
			var editing = Selection.activeGameObject == container.gameObject
				&& ToolManager.activeContextType == typeof(SplineToolContext);
			Handles.BeginGUI();
			GUILayout.BeginArea(new Rect(55f, 42f, 250f, editing ? 86f : 58f),
				GUIContent.none, GUI.skin.window);
			using (new EditorGUI.DisabledScope(editing)) {
				if (GUILayout.Button(editing ? "Editing Wire Rail Spline"
						: "Edit Wire Rail Spline", GUILayout.Height(26f))) {
					WireRailInspector.EditSpline(container);
				}
			}
			if (editing) {
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
			Handles.color = new Color(1f, 0.9f, 0.05f, 0.9f);
			for (var i = 0; i < indices.Length; i += 3) {
				DrawEdge(indices[i], indices[i + 1]);
				DrawEdge(indices[i + 1], indices[i + 2]);
				DrawEdge(indices[i + 2], indices[i]);
			}

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
