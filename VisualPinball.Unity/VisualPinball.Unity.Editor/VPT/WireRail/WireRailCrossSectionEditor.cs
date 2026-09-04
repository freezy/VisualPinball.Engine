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

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	internal sealed class WireRailCrossSectionEditor
	{
		private const int ControlHint = 0x57495245;
		private const float CanvasHeight = 170f;
		private const float LineHeight = 20f;
		public const float Height = CanvasHeight + LineHeight * 2f + 8f;
		private const float GridStep = 10f;
		private static readonly Color CanvasColor = new(0.105f, 0.115f, 0.13f, 1f);
		private static readonly Color GridColor = new(1f, 1f, 1f, 0.07f);
		private static readonly Color AxisColor = new(1f, 1f, 1f, 0.28f);
		private static readonly Color SelectedColor = new(1f, 0.78f, 0.08f, 1f);
		private static readonly Color InactiveWireColor = new(0.43f, 0.45f, 0.48f, 1f);
		private static readonly Color[] WireColors = {
			new(0.05f, 0.75f, 1f, 1f),
			new(1f, 0.55f, 0.05f, 1f),
			new(0.45f, 1f, 0.2f, 1f),
			new(1f, 0.2f, 0.65f, 1f),
			new(0.65f, 0.35f, 1f, 1f),
		};

		private readonly Dictionary<int, HashSet<int>> _selectedWires = new();
		private readonly List<int> _dragIndices = new();
		private readonly List<Vector2> _dragOffsets = new();
		private Vector2 _dragStartMouse;
		private int _dragSegmentIndex = -1;
		private int _dragUndoGroup = -1;
		private static Texture2D _circleTexture;
		private static GUIStyle _wireLabelStyle;

		public void Draw(Rect rect, WireRailComponent component, int segmentIndex)
		{
			var segment = component.Segments[segmentIndex];
			var selected = GetSelection(segmentIndex, segment.RailCount);
			var canvasRect = new Rect(rect.x, rect.y, rect.width, CanvasHeight);
			var view = CrossSectionView.Create(canvasRect, segment);
			var controlId = GUIUtility.GetControlID(ControlHint + segmentIndex * 397,
				FocusType.Passive, canvasRect);
			HandleInput(component, segmentIndex, segment, selected, view, controlId);
			DrawCanvas(segment, selected, view);
			GUI.Label(canvasRect, new GUIContent(string.Empty,
				"Click to select · Shift/Ctrl/Cmd-click for multiple · Drag to move"));
			DrawSelectedWireControls(new Rect(rect.x, canvasRect.yMax + 4f, rect.width,
				LineHeight * 2f + 4f), component, segmentIndex, segment, selected);
		}

		private HashSet<int> GetSelection(int segmentIndex, int railCount)
		{
			if (!_selectedWires.TryGetValue(segmentIndex, out var selected)) {
				selected = new HashSet<int>();
				_selectedWires.Add(segmentIndex, selected);
			}
			selected.RemoveWhere(index => index < 0 || index >= railCount);
			return selected;
		}

		private void HandleInput(WireRailComponent component, int segmentIndex,
			WireRailSegment segment, HashSet<int> selected, CrossSectionView view,
			int controlId)
		{
			var evt = Event.current;
			switch (evt.GetTypeForControl(controlId)) {
				case EventType.MouseDown when evt.button == 0 && view.Rect.Contains(evt.mousePosition):
					var hit = PickWire(segment, view, evt.mousePosition);
					if (hit < 0) {
						if (!HasSelectionModifier(evt)) {
							selected.Clear();
						}
						evt.Use();
						GUI.changed = true;
						return;
					}

					UpdateSelection(selected, hit, evt);
					if (!selected.Contains(hit)) {
						evt.Use();
						GUI.changed = true;
						return;
					}
					GUIUtility.hotControl = controlId;
					GUIUtility.keyboardControl = controlId;
					_dragStartMouse = evt.mousePosition;
					_dragSegmentIndex = segmentIndex;
					_dragUndoGroup = -1;
					CaptureDragValues(segment, selected);
					evt.Use();
					GUI.changed = true;
					break;

				case EventType.MouseDrag when GUIUtility.hotControl == controlId
					&& _dragSegmentIndex == segmentIndex:
					if (_dragUndoGroup < 0) {
						_dragUndoGroup = BeginDragUndo(component);
					}
					var delta = view.ToVpxDelta(evt.mousePosition - _dragStartMouse);
					var movedOffsets = new Vector2[_dragOffsets.Count];
					for (var i = 0; i < movedOffsets.Length; i++) {
						movedOffsets[i] = _dragOffsets[i] + delta;
					}
					component.SetWireProperties(segmentIndex, _dragIndices,
						movedOffsets);
					Apply(component);
					evt.Use();
					GUI.changed = true;
					break;

				case EventType.MouseUp when GUIUtility.hotControl == controlId:
					EndDragUndo(_dragUndoGroup);
					GUIUtility.hotControl = 0;
					_dragSegmentIndex = -1;
					_dragUndoGroup = -1;
					evt.Use();
					GUI.changed = true;
					break;
			}
		}

		private static int BeginDragUndo(WireRailComponent component)
		{
			Undo.IncrementCurrentGroup();
			var undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Move Wire Rail Wires");
			Undo.RegisterCompleteObjectUndo(component, "Move Wire Rail Wires");
			return undoGroup;
		}

		private static void EndDragUndo(int undoGroup)
		{
			if (undoGroup >= 0) {
				Undo.CollapseUndoOperations(undoGroup);
			}
		}

		private static void UpdateSelection(HashSet<int> selected, int hit, Event evt)
		{
			if (evt.control || evt.command) {
				if (!selected.Add(hit)) {
					selected.Remove(hit);
				}
				return;
			}
			if (evt.shift) {
				selected.Add(hit);
				return;
			}
			if (selected.Count == 1 && selected.Contains(hit)) {
				return;
			}
			if (!selected.Contains(hit)) {
				selected.Clear();
				selected.Add(hit);
			}
		}

		private static bool HasSelectionModifier(Event evt)
			=> evt.shift || evt.control || evt.command;

		private void CaptureDragValues(WireRailSegment segment, HashSet<int> selected)
		{
			_dragIndices.Clear();
			_dragOffsets.Clear();
			foreach (var railIndex in selected) {
				_dragIndices.Add(railIndex);
			}
			_dragIndices.Sort();
			foreach (var railIndex in _dragIndices) {
				_dragOffsets.Add(segment.GetRailOffset(railIndex));
			}
		}

		private static int PickWire(WireRailSegment segment, CrossSectionView view,
			Vector2 mousePosition)
		{
			var hit = -1;
			var closest = float.MaxValue;
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				var center = view.ToScreen(segment.GetRailOffset(railIndex));
				var radius = math.max(8f,
					segment.GetWireDiameter(railIndex) * 0.5f * view.Scale + 3f);
				var distance = Vector2.Distance(mousePosition, center);
				if (distance <= radius && distance < closest) {
					closest = distance;
					hit = railIndex;
				}
			}
			return hit;
		}

		private static void DrawCanvas(WireRailSegment segment, HashSet<int> selected,
			CrossSectionView view)
		{
			EditorGUI.DrawRect(view.Rect, CanvasColor);
			DrawGrid(view);
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				DrawWire(segment, railIndex, selected.Contains(railIndex), view);
			}
			GUI.Label(new Rect(view.Rect.x + 6f, view.Rect.y + 4f, 30f, 18f), "Z ↑",
				EditorStyles.miniLabel);
			GUI.Label(new Rect(view.Rect.xMax - 34f, view.Rect.yMax - 20f, 30f, 18f),
				"X →", EditorStyles.miniLabel);
		}

		private static void DrawGrid(CrossSectionView view)
		{
			for (var x = math.ceil(view.Min.x / GridStep) * GridStep;
				x <= view.Max.x; x += GridStep) {
				var screen = view.ToScreen(new Vector2(x, 0f));
				EditorGUI.DrawRect(new Rect(screen.x, view.Rect.y, 1f, view.Rect.height),
					math.abs(x) < 0.01f ? AxisColor : GridColor);
			}
			for (var z = math.ceil(view.Min.y / GridStep) * GridStep;
				z <= view.Max.y; z += GridStep) {
				var screen = view.ToScreen(new Vector2(0f, z));
				EditorGUI.DrawRect(new Rect(view.Rect.x, screen.y, view.Rect.width, 1f),
					math.abs(z) < 0.01f ? AxisColor : GridColor);
			}
		}

		private static void DrawWire(WireRailSegment segment, int railIndex, bool selected,
			CrossSectionView view)
		{
			var offset = segment.GetRailOffset(railIndex);
			var diameter = segment.GetWireDiameter(railIndex);
			var center = view.ToScreen(offset);
			var radius = math.max(6f, diameter * 0.5f * view.Scale);
			DrawCircle(center, radius + (selected ? 4f : 2f),
				selected ? SelectedColor : new Color(0f, 0f, 0f, 0.75f));
			DrawCircle(center, radius, segment.IsRailActive(railIndex)
				? WireColors[railIndex % WireColors.Length] : InactiveWireColor);
			var labelRect = new Rect(center.x - radius, center.y - 9f, radius * 2f, 18f);
			GUI.Label(labelRect, (railIndex + 1).ToString(), WireLabelStyle);
			GUI.Label(new Rect(center.x - radius - 4f, center.y - radius - 4f,
					radius * 2f + 8f, radius * 2f + 8f),
				new GUIContent(string.Empty, $"Wire {railIndex + 1}: X {offset.x:0.##}, "
					+ $"Z {offset.y:0.##}, diameter {diameter:0.##}, "
					+ (segment.IsRailActive(railIndex) ? "active" : "inactive")));
		}

		private static void DrawCircle(Vector2 center, float radius, Color color)
		{
			var previous = GUI.color;
			GUI.color = color;
			GUI.DrawTexture(new Rect(center.x - radius, center.y - radius,
				radius * 2f, radius * 2f), CircleTexture, ScaleMode.StretchToFill, true);
			GUI.color = previous;
		}

		private static void DrawSelectedWireControls(Rect rect, WireRailComponent component,
			int segmentIndex, WireRailSegment segment, HashSet<int> selected)
		{
			var selectionRect = new Rect(rect.x, rect.y, rect.width, LineHeight);
			var noneRect = new Rect(selectionRect.xMax - 44f, selectionRect.y, 44f,
				LineHeight);
			var allRect = new Rect(noneRect.x - 38f, selectionRect.y, 38f, LineHeight);
			var labelRect = new Rect(selectionRect.x, selectionRect.y,
				math.max(0f, allRect.x - selectionRect.x - 4f), LineHeight);
			EditorGUI.LabelField(labelRect, selected.Count == 0 ? "No wires selected"
				: selected.Count == 1 ? "1 wire selected" : $"{selected.Count} wires selected",
				EditorStyles.boldLabel);
			if (GUI.Button(allRect, "All", EditorStyles.miniButtonLeft)) {
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					selected.Add(railIndex);
				}
			}
			if (GUI.Button(noneRect, "None", EditorStyles.miniButtonRight)) {
				selected.Clear();
			}

			using (new EditorGUI.DisabledScope(selected.Count == 0)) {
				var indices = new List<int>(selected);
				indices.Sort();
				var firstIndex = indices.Count > 0 ? indices[0] : 0;
				var firstOffset = indices.Count > 0
					? segment.GetRailOffset(firstIndex) : Vector2.zero;
				var mixedX = false;
				var mixedZ = false;
				var firstActive = indices.Count > 0 && segment.IsRailActive(firstIndex);
				var mixedActive = false;
				for (var i = 1; i < indices.Count; i++) {
					var offset = segment.GetRailOffset(indices[i]);
					mixedX |= !Mathf.Approximately(offset.x, firstOffset.x);
					mixedZ |= !Mathf.Approximately(offset.y, firstOffset.y);
					mixedActive |= segment.IsRailActive(indices[i]) != firstActive;
				}

				var controlsRect = new Rect(rect.x, selectionRect.yMax + 4f, rect.width,
					LineHeight);
				const float resetWidth = 54f;
				const float applyToAllWidth = 78f;
				const float activeWidth = 18f;
				const float positionLabelWidth = 50f;
				const float axisLabelWidth = 14f;
				const float spacing = 4f;
				var numericFieldWidth = math.max(20f, (controlsRect.width - activeWidth
					- positionLabelWidth - axisLabelWidth * 2f - resetWidth - applyToAllWidth
					- spacing * 7f)
					* 0.5f);
				var axisFieldWidth = axisLabelWidth + numericFieldWidth;
				var x = controlsRect.x;
				EditorGUI.BeginChangeCheck();
				EditorGUI.showMixedValue = mixedActive;
				var active = EditorGUI.Toggle(new Rect(x, controlsRect.y, activeWidth,
					LineHeight), new GUIContent(string.Empty,
					"Enable the selected wires for this layout span."), firstActive);
				var activeChanged = EditorGUI.EndChangeCheck();
				EditorGUI.showMixedValue = false;
				x += activeWidth + spacing;
				EditorGUI.LabelField(new Rect(x, controlsRect.y, positionLabelWidth,
					LineHeight), "Position");
				x += positionLabelWidth + spacing;
				var previousLabelWidth = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = axisLabelWidth;
				EditorGUI.BeginChangeCheck();
				EditorGUI.showMixedValue = mixedX;
				var editedX = EditorGUI.FloatField(new Rect(x, controlsRect.y, axisFieldWidth,
					LineHeight), new GUIContent("X",
					"Lateral centerline position in VPX units."), firstOffset.x);
				var xChanged = EditorGUI.EndChangeCheck();
				x += axisFieldWidth + spacing;
				EditorGUI.BeginChangeCheck();
				EditorGUI.showMixedValue = mixedZ;
				var editedZ = EditorGUI.FloatField(new Rect(x, controlsRect.y, axisFieldWidth,
					LineHeight), new GUIContent("Z",
					"Vertical centerline position in VPX units."), firstOffset.y);
				var zChanged = EditorGUI.EndChangeCheck();
				EditorGUI.showMixedValue = false;
				EditorGUIUtility.labelWidth = previousLabelWidth;

				if (indices.Count > 0 && activeChanged) {
					Undo.RegisterCompleteObjectUndo(component, "Toggle Wire Rail Wires");
					component.SetRailsActive(segmentIndex, indices, active);
					Apply(component);
				}

				if (indices.Count > 0 && (xChanged || zChanged)) {
					var offsets = new Vector2[indices.Count];
					for (var i = 0; i < indices.Count; i++) {
						var currentOffset = segment.GetRailOffset(indices[i]);
						offsets[i] = new Vector2(xChanged ? editedX : currentOffset.x,
							zChanged ? editedZ : currentOffset.y);
					}
					Undo.RegisterCompleteObjectUndo(component, "Edit Wire Rail Wires");
					component.SetWireProperties(segmentIndex, indices, offsets);
					Apply(component);
				}
			}
			const float buttonSpacing = 4f;
			const float resetButtonWidth = 54f;
			const float applyToAllButtonWidth = 78f;
			var applyToAllRect = new Rect(rect.xMax - applyToAllButtonWidth,
				selectionRect.yMax + 4f, applyToAllButtonWidth, LineHeight);
			var resetRect = new Rect(applyToAllRect.x - buttonSpacing - resetButtonWidth,
				applyToAllRect.y, resetButtonWidth, LineHeight);
			using (new EditorGUI.DisabledScope(selected.Count == 0)) {
				if (GUI.Button(resetRect, "Reset")) {
					Undo.RegisterCompleteObjectUndo(component, "Reset Wire Rail Layout");
					component.ResetSegmentLayout(segmentIndex);
					Apply(component);
				}
				if (GUI.Button(applyToAllRect, new GUIContent("Apply to All",
					"Copy the selected wires' X and Z positions to every layout."))) {
					var indices = new List<int>(selected);
					indices.Sort();
					Undo.RegisterCompleteObjectUndo(component,
						"Apply Wire Positions to All Layouts");
					component.ApplyWirePositionsToAllLayouts(segmentIndex, indices);
					Apply(component);
				}
			}
		}

		private static void Apply(WireRailComponent component)
		{
			EditorUtility.SetDirty(component);
			PrefabUtility.RecordPrefabInstancePropertyModifications(component);
			SceneView.RepaintAll();
		}

		private static Texture2D CircleTexture {
			get {
				if (_circleTexture) {
					return _circleTexture;
				}
				const int size = 64;
				_circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
					name = "Wire Rail Cross-Section Circle",
					hideFlags = HideFlags.HideAndDontSave,
					filterMode = FilterMode.Bilinear,
					wrapMode = TextureWrapMode.Clamp,
				};
				var pixels = new Color32[size * size];
				for (var y = 0; y < size; y++) {
					for (var x = 0; x < size; x++) {
						var normalized = new float2((x + 0.5f) / size * 2f - 1f,
							(y + 0.5f) / size * 2f - 1f);
						var alpha = (byte)math.round(math.saturate((1f - math.length(normalized))
							* size) * 255f);
						pixels[y * size + x] = new Color32(255, 255, 255, alpha);
					}
				}
				_circleTexture.SetPixels32(pixels);
				_circleTexture.Apply(false, true);
				return _circleTexture;
			}
		}

		private static GUIStyle WireLabelStyle {
			get {
				if (_wireLabelStyle != null) {
					return _wireLabelStyle;
				}
				_wireLabelStyle = new GUIStyle(EditorStyles.boldLabel) {
					alignment = TextAnchor.MiddleCenter,
					fontSize = 10,
					normal = { textColor = Color.white },
				};
				return _wireLabelStyle;
			}
		}

		private readonly struct CrossSectionView
		{
			public readonly Rect Rect;
			public readonly Vector2 Min;
			public readonly Vector2 Max;
			public readonly float Scale;

			private CrossSectionView(Rect rect, Vector2 min, Vector2 max, float scale)
			{
				Rect = rect;
				Min = min;
				Max = max;
				Scale = scale;
			}

			public static CrossSectionView Create(Rect rect, WireRailSegment segment)
			{
				var min = new Vector2(-35f, -12f);
				var max = new Vector2(35f, 68f);
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					var offset = segment.GetRailOffset(railIndex);
					var padding = segment.GetWireDiameter(railIndex) * 0.5f + 8f;
					min = Vector2.Min(min, offset - Vector2.one * padding);
					max = Vector2.Max(max, offset + Vector2.one * padding);
				}
				var size = max - min;
				var scale = math.max(0.01f, math.min((rect.width - 20f) / size.x,
					(rect.height - 20f) / size.y));
				var fittedSize = new Vector2((rect.width - 20f) / scale,
					(rect.height - 20f) / scale);
				var center = (min + max) * 0.5f;
				min = center - fittedSize * 0.5f;
				max = center + fittedSize * 0.5f;
				return new CrossSectionView(rect, min, max, scale);
			}

			public Vector2 ToScreen(Vector2 vpx)
				=> new(Rect.x + 10f + (vpx.x - Min.x) * Scale,
					Rect.yMax - 10f - (vpx.y - Min.y) * Scale);

			public Vector2 ToVpxDelta(Vector2 pixels)
				=> new(pixels.x / Scale, -pixels.y / Scale);
		}
	}

	internal sealed class WireRailBracePreviewEditor
	{
		private const float FullTurn = math.PI * 2f;
		public const float Height = 170f;
		private static readonly Color CanvasColor = new(0.105f, 0.115f, 0.13f, 1f);
		private static readonly Color GridColor = new(1f, 1f, 1f, 0.07f);
		private static readonly Color AxisColor = new(1f, 1f, 1f, 0.28f);
		private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.8f);
		private static readonly Color BraceColor = new(1f, 0.67f, 0.12f, 1f);

		public void Draw(Rect rect, WireRailComponent component, int fixtureIndex,
			WireRailBraceFixture brace)
		{
			EditorGUI.DrawRect(rect, CanvasColor);
			if (!component.TryGetBraceCrossSection(fixtureIndex, out var crossSection)) {
				EditorGUI.LabelField(rect, "Brace preview unavailable",
					EditorStyles.centeredGreyMiniLabel);
				return;
			}
			if (!brace.TryGetVisibleArc(out var startAngle, out var sweepAngle,
					out _)) {
				EditorGUI.LabelField(rect, "Brace fully cut out",
					EditorStyles.centeredGreyMiniLabel);
				return;
			}

			var angles = BuildAngles(brace, startAngle, sweepAngle);
			var offsets = new List<Vector2>(angles.Count);
			foreach (var angle in angles) {
				var centerline = brace.EvaluateCenterlineOffset(angle, crossSection.Radius);
				offsets.Add(crossSection.CenterOffset
					+ new Vector2(centerline.x, centerline.y));
			}
			var view = BracePreviewView.Create(rect, offsets, brace.Diameter * 0.5f);
			DrawGrid(view);

			var points = new Vector3[offsets.Count];
			for (var index = 0; index < offsets.Count; index++) {
				points[index] = view.ToScreen(offsets[index]);
			}
			var width = math.clamp(brace.Diameter * view.Scale, 3f, 16f);
			Handles.BeginGUI();
			var previousColor = Handles.color;
			Handles.color = OutlineColor;
			Handles.DrawAAPolyLine(width + 3f, points);
			Handles.color = BraceColor;
			Handles.DrawAAPolyLine(width, points);
			Handles.color = previousColor;
			Handles.EndGUI();

			GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 30f, 18f), "Z ↑",
				EditorStyles.miniLabel);
			GUI.Label(new Rect(rect.xMax - 34f, rect.yMax - 20f, 30f, 18f), "X →",
				EditorStyles.miniLabel);
			GUI.Label(rect, new GUIContent(string.Empty,
				"Brace cross-section at its route position"));
		}

		private static List<float> BuildAngles(WireRailBraceFixture brace,
			float startAngle, float sweepAngle)
		{
			var segmentCount = math.max(2,
				(int)math.ceil(brace.RingDensity * sweepAngle / FullTurn));
			var angles = new List<float>(segmentCount + 3);
			for (var index = 0; index <= segmentCount; index++) {
				angles.Add(startAngle + sweepAngle * index / segmentCount);
			}
			if (brace.TryGetStraightSection(out var straightStart, out var straightSweep)) {
				AddBoundary(straightStart);
				AddBoundary(straightStart + straightSweep);
			}
			angles.Sort();
			for (var index = angles.Count - 1; index > 0; index--) {
				if (math.abs(angles[index] - angles[index - 1]) < 1e-5f) {
					angles.RemoveAt(index);
				}
			}
			return angles;

			void AddBoundary(float boundary)
			{
				for (var turn = -2; turn <= 2; turn++) {
					var unwrapped = boundary + turn * FullTurn;
					if (unwrapped >= startAngle - 1e-5f
						&& unwrapped <= startAngle + sweepAngle + 1e-5f) {
						angles.Add(unwrapped);
					}
				}
			}
		}

		private static void DrawGrid(BracePreviewView view)
		{
			var span = math.max(view.Max.x - view.Min.x, view.Max.y - view.Min.y);
			var gridStep = span > 400f ? 100f : span > 200f ? 50f : span > 100f ? 20f : 10f;
			for (var x = math.ceil(view.Min.x / gridStep) * gridStep;
				x <= view.Max.x; x += gridStep) {
				var screen = view.ToScreen(new Vector2(x, 0f));
				EditorGUI.DrawRect(new Rect(screen.x, view.Rect.y, 1f, view.Rect.height),
					math.abs(x) < 0.01f ? AxisColor : GridColor);
			}
			for (var z = math.ceil(view.Min.y / gridStep) * gridStep;
				z <= view.Max.y; z += gridStep) {
				var screen = view.ToScreen(new Vector2(0f, z));
				EditorGUI.DrawRect(new Rect(view.Rect.x, screen.y, view.Rect.width, 1f),
					math.abs(z) < 0.01f ? AxisColor : GridColor);
			}
		}

		private readonly struct BracePreviewView
		{
			public readonly Rect Rect;
			public readonly Vector2 Min;
			public readonly Vector2 Max;
			public readonly float Scale;

			private BracePreviewView(Rect rect, Vector2 min, Vector2 max, float scale)
			{
				Rect = rect;
				Min = min;
				Max = max;
				Scale = scale;
			}

			public static BracePreviewView Create(Rect rect, IReadOnlyList<Vector2> offsets,
				float tubeRadius)
			{
				var min = Vector2.zero;
				var max = Vector2.zero;
				var padding = math.max(8f, tubeRadius + 8f);
				foreach (var offset in offsets) {
					min = Vector2.Min(min, offset - Vector2.one * padding);
					max = Vector2.Max(max, offset + Vector2.one * padding);
				}
				var size = Vector2.Max(max - min, new Vector2(1f, 1f));
				var scale = math.max(0.01f, math.min((rect.width - 20f) / size.x,
					(rect.height - 20f) / size.y));
				var fittedSize = new Vector2((rect.width - 20f) / scale,
					(rect.height - 20f) / scale);
				var center = (min + max) * 0.5f;
				min = center - fittedSize * 0.5f;
				max = center + fittedSize * 0.5f;
				return new BracePreviewView(rect, min, max, scale);
			}

			public Vector3 ToScreen(Vector2 vpx)
				=> new(Rect.x + 10f + (vpx.x - Min.x) * Scale,
					Rect.yMax - 10f - (vpx.y - Min.y) * Scale, 0f);
		}
	}

	internal sealed class WireRailVBracePreviewEditor
	{
		private const int CircleSegments = 48;
		public const float Height = 190f;
		private static readonly Color CanvasColor = new(0.105f, 0.115f, 0.13f, 1f);
		private static readonly Color GridColor = new(1f, 1f, 1f, 0.07f);
		private static readonly Color AxisColor = new(1f, 1f, 1f, 0.28f);
		private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.8f);
		private static readonly Color RailColor = new(0.55f, 0.58f, 0.62f, 1f);
		private static readonly Color BraceColor = new(1f, 0.67f, 0.12f, 1f);

		public void Draw(Rect rect, WireRailComponent component, int fixtureIndex,
			WireRailVBraceFixture vBrace)
		{
			EditorGUI.DrawRect(rect, CanvasColor);
			if (!component.TryGetVBracePreview(fixtureIndex, out var preview)) {
				EditorGUI.LabelField(rect,
					"Cross-wire-with-arms preview unavailable at this position",
					EditorStyles.centeredGreyMiniLabel);
				return;
			}

			var view = VBracePreviewView.Create(rect, preview, vBrace.Diameter * 0.5f);
			DrawGrid(view);
			var railWidth = math.clamp(vBrace.Diameter * view.Scale, 2f, 12f);
			var braceWidth = math.clamp(vBrace.Diameter * view.Scale, 3f, 16f);
			Handles.BeginGUI();
			var previousColor = Handles.color;
			for (var railIndex = 0; railIndex < preview.RailOffsets.Count; railIndex++) {
				DrawRail(view, preview.RailOffsets[railIndex],
					preview.RailRadii[railIndex], railWidth);
			}
			var points = new Vector3[preview.CenterlinePoints.Count];
			for (var pointIndex = 0; pointIndex < points.Length; pointIndex++) {
				points[pointIndex] = view.ToScreen(preview.CenterlinePoints[pointIndex]);
			}
			Handles.color = OutlineColor;
			Handles.DrawAAPolyLine(braceWidth + 3f, points);
			Handles.color = BraceColor;
			Handles.DrawAAPolyLine(braceWidth, points);
			Handles.color = previousColor;
			Handles.EndGUI();

			GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 30f, 18f), "Z ↑",
				EditorStyles.miniLabel);
			GUI.Label(new Rect(rect.xMax - 34f, rect.yMax - 20f, 30f, 18f), "X →",
				EditorStyles.miniLabel);
			GUI.Label(rect, new GUIContent(string.Empty,
				"Cross wire with optional arms at its route position"));
		}

		private static void DrawRail(VBracePreviewView view, Vector2 center,
			float radius, float width)
		{
			var points = new Vector3[CircleSegments + 1];
			for (var index = 0; index <= CircleSegments; index++) {
				var angle = math.PI * 2f * index / CircleSegments;
				points[index] = view.ToScreen(center
					+ new Vector2(math.cos(angle), math.sin(angle)) * radius);
			}
			Handles.color = OutlineColor;
			Handles.DrawAAPolyLine(width + 2f, points);
			Handles.color = RailColor;
			Handles.DrawAAPolyLine(width, points);
		}

		private static void DrawGrid(VBracePreviewView view)
		{
			var span = math.max(view.Max.x - view.Min.x, view.Max.y - view.Min.y);
			var gridStep = span > 400f ? 100f : span > 200f ? 50f : span > 100f ? 20f : 10f;
			for (var x = math.ceil(view.Min.x / gridStep) * gridStep;
				x <= view.Max.x; x += gridStep) {
				var screen = view.ToScreen(new Vector2(x, 0f));
				EditorGUI.DrawRect(new Rect(screen.x, view.Rect.y, 1f, view.Rect.height),
					math.abs(x) < 0.01f ? AxisColor : GridColor);
			}
			for (var z = math.ceil(view.Min.y / gridStep) * gridStep;
				z <= view.Max.y; z += gridStep) {
				var screen = view.ToScreen(new Vector2(0f, z));
				EditorGUI.DrawRect(new Rect(view.Rect.x, screen.y, view.Rect.width, 1f),
					math.abs(z) < 0.01f ? AxisColor : GridColor);
			}
		}

		private readonly struct VBracePreviewView
		{
			public readonly Rect Rect;
			public readonly Vector2 Min;
			public readonly Vector2 Max;
			public readonly float Scale;

			private VBracePreviewView(Rect rect, Vector2 min, Vector2 max, float scale)
			{
				Rect = rect;
				Min = min;
				Max = max;
				Scale = scale;
			}

			public static VBracePreviewView Create(Rect rect, WireRailVBracePreview preview,
				float tubeRadius)
			{
				var padding = math.max(8f, tubeRadius + 8f);
				var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
				var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
				for (var railIndex = 0; railIndex < preview.RailOffsets.Count; railIndex++) {
					Include(preview.RailOffsets[railIndex],
						preview.RailRadii[railIndex] + padding);
				}
				foreach (var point in preview.CenterlinePoints) {
					Include(point, padding);
				}
				var size = Vector2.Max(max - min, new Vector2(1f, 1f));
				var scale = math.max(0.01f, math.min((rect.width - 20f) / size.x,
					(rect.height - 20f) / size.y));
				var fittedSize = new Vector2((rect.width - 20f) / scale,
					(rect.height - 20f) / scale);
				var center = (min + max) * 0.5f;
				return new VBracePreviewView(rect, center - fittedSize * 0.5f,
					center + fittedSize * 0.5f, scale);

				void Include(Vector2 point, float radius)
				{
					min = Vector2.Min(min, point - Vector2.one * radius);
					max = Vector2.Max(max, point + Vector2.one * radius);
				}
			}

			public Vector3 ToScreen(Vector2 vpx)
				=> new(Rect.x + 10f + (vpx.x - Min.x) * Scale,
					Rect.yMax - 10f - (vpx.y - Min.y) * Scale, 0f);
		}
	}

	internal sealed class WireRailCrossWirePreviewEditor
	{
		private const int CircleSegments = 48;
		public const float Height = 170f;
		private static readonly Color CanvasColor = new(0.105f, 0.115f, 0.13f, 1f);
		private static readonly Color GridColor = new(1f, 1f, 1f, 0.07f);
		private static readonly Color AxisColor = new(1f, 1f, 1f, 0.28f);
		private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.8f);
		private static readonly Color RailColor = new(0.55f, 0.58f, 0.62f, 1f);
		private static readonly Color CrossWireColor = new(1f, 0.67f, 0.12f, 1f);

		public void Draw(Rect rect, WireRailComponent component, int fixtureIndex,
			WireRailCrossWireFixture crossWire)
		{
			EditorGUI.DrawRect(rect, CanvasColor);
			if (!component.TryGetCrossWireCrossSection(fixtureIndex, out var crossSection)) {
				EditorGUI.LabelField(rect,
					"Cross-wire preview unavailable at this position",
					EditorStyles.centeredGreyMiniLabel);
				return;
			}

			var view = CrossWirePreviewView.Create(rect, crossSection,
				crossWire.Diameter * 0.5f);
			DrawGrid(view);
			var railWidth = math.clamp(crossWire.Diameter * view.Scale, 2f, 12f);
			var crossWireWidth = math.clamp(crossWire.Diameter * view.Scale, 3f, 16f);
			var start = view.ToScreen(crossSection.StartOffset);
			var end = view.ToScreen(crossSection.EndOffset);
			Handles.BeginGUI();
			var previousColor = Handles.color;
			DrawRail(view, crossSection.StartRailOffset,
				crossSection.StartRailRadius, railWidth);
			DrawRail(view, crossSection.EndRailOffset,
				crossSection.EndRailRadius, railWidth);
			Handles.color = OutlineColor;
			Handles.DrawAAPolyLine(crossWireWidth + 3f, start, end);
			Handles.color = CrossWireColor;
			Handles.DrawAAPolyLine(crossWireWidth, start, end);
			Handles.color = previousColor;
			Handles.EndGUI();

			GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 30f, 18f), "Z ↑",
				EditorStyles.miniLabel);
			GUI.Label(new Rect(rect.xMax - 34f, rect.yMax - 20f, 30f, 18f), "X →",
				EditorStyles.miniLabel);
			GUI.Label(rect, new GUIContent(string.Empty,
				"Cross-wire section at its route position"));
		}

		private static void DrawRail(CrossWirePreviewView view, Vector2 center,
			float radius, float width)
		{
			var points = new Vector3[CircleSegments + 1];
			for (var index = 0; index <= CircleSegments; index++) {
				var angle = math.PI * 2f * index / CircleSegments;
				points[index] = view.ToScreen(center
					+ new Vector2(math.cos(angle), math.sin(angle)) * radius);
			}
			Handles.color = OutlineColor;
			Handles.DrawAAPolyLine(width + 2f, points);
			Handles.color = RailColor;
			Handles.DrawAAPolyLine(width, points);
		}

		private static void DrawGrid(CrossWirePreviewView view)
		{
			var span = math.max(view.Max.x - view.Min.x, view.Max.y - view.Min.y);
			var gridStep = span > 400f ? 100f : span > 200f ? 50f : span > 100f ? 20f : 10f;
			for (var x = math.ceil(view.Min.x / gridStep) * gridStep;
				x <= view.Max.x; x += gridStep) {
				var screen = view.ToScreen(new Vector2(x, 0f));
				EditorGUI.DrawRect(new Rect(screen.x, view.Rect.y, 1f, view.Rect.height),
					math.abs(x) < 0.01f ? AxisColor : GridColor);
			}
			for (var z = math.ceil(view.Min.y / gridStep) * gridStep;
				z <= view.Max.y; z += gridStep) {
				var screen = view.ToScreen(new Vector2(0f, z));
				EditorGUI.DrawRect(new Rect(view.Rect.x, screen.y, view.Rect.width, 1f),
					math.abs(z) < 0.01f ? AxisColor : GridColor);
			}
		}

		private readonly struct CrossWirePreviewView
		{
			public readonly Rect Rect;
			public readonly Vector2 Min;
			public readonly Vector2 Max;
			public readonly float Scale;

			private CrossWirePreviewView(Rect rect, Vector2 min, Vector2 max, float scale)
			{
				Rect = rect;
				Min = min;
				Max = max;
				Scale = scale;
			}

			public static CrossWirePreviewView Create(Rect rect,
				WireRailCrossWireCrossSection crossSection, float tubeRadius)
			{
				var padding = math.max(8f, tubeRadius + 8f);
				var min = Vector2.Min(crossSection.StartOffset, crossSection.EndOffset)
					- Vector2.one * padding;
				var max = Vector2.Max(crossSection.StartOffset, crossSection.EndOffset)
					+ Vector2.one * padding;
				min = Vector2.Min(min, crossSection.StartRailOffset
					- Vector2.one * (crossSection.StartRailRadius + padding));
				max = Vector2.Max(max, crossSection.StartRailOffset
					+ Vector2.one * (crossSection.StartRailRadius + padding));
				min = Vector2.Min(min, crossSection.EndRailOffset
					- Vector2.one * (crossSection.EndRailRadius + padding));
				max = Vector2.Max(max, crossSection.EndRailOffset
					+ Vector2.one * (crossSection.EndRailRadius + padding));
				var size = Vector2.Max(max - min, new Vector2(1f, 1f));
				var scale = math.max(0.01f, math.min((rect.width - 20f) / size.x,
					(rect.height - 20f) / size.y));
				var fittedSize = new Vector2((rect.width - 20f) / scale,
					(rect.height - 20f) / scale);
				var center = (min + max) * 0.5f;
				min = center - fittedSize * 0.5f;
				max = center + fittedSize * 0.5f;
				return new CrossWirePreviewView(rect, min, max, scale);
			}

			public Vector3 ToScreen(Vector2 vpx)
				=> new(Rect.x + 10f + (vpx.x - Min.x) * Scale,
					Rect.yMax - 10f - (vpx.y - Min.y) * Scale, 0f);
		}
	}

	internal sealed class WireRailLegPreviewEditor
	{
		private const int CircleSegments = 48;
		public const float Height = 190f;
		private static readonly Color CanvasColor = new(0.105f, 0.115f, 0.13f, 1f);
		private static readonly Color GridColor = new(1f, 1f, 1f, 0.07f);
		private static readonly Color AxisColor = new(1f, 1f, 1f, 0.28f);
		private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.8f);
		private static readonly Color RailColor = new(0.55f, 0.58f, 0.62f, 1f);
		private static readonly Color LegColor = new(1f, 0.67f, 0.12f, 1f);

		public void Draw(Rect rect, WireRailComponent component, int fixtureIndex,
			WireRailLegFixture leg)
		{
			EditorGUI.DrawRect(rect, CanvasColor);
			if (!component.TryGetLegPreview(fixtureIndex, out var preview)) {
				EditorGUI.LabelField(rect,
					"Leg and foot preview unavailable at this position",
					EditorStyles.centeredGreyMiniLabel);
				return;
			}

			var view = LegPreviewView.Create(rect, preview, leg.Diameter * 0.5f);
			DrawGrid(view);
			var railWidth = math.clamp(leg.Diameter * view.Scale, 2f, 12f);
			var tubeWidth = math.clamp(leg.Diameter * view.Scale, 3f, 16f);
			Handles.BeginGUI();
			var previousColor = Handles.color;
			DrawRail(view, preview.StartRailOffset, preview.StartRailRadius, railWidth);
			DrawRail(view, preview.EndRailOffset, preview.EndRailRadius, railWidth);
			DrawPath(view, preview.CenterlinePoints, tubeWidth, LegColor);
			Handles.color = previousColor;
			Handles.EndGUI();

			GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 88f, 18f),
				"X / Y / Z", EditorStyles.miniLabel);
			GUI.Label(rect, new GUIContent(string.Empty,
				"Projected route-local view of the rounded leg and U-hook centerline."));
		}

		private static void DrawRail(LegPreviewView view, Vector3 center,
			float radius, float width)
		{
			var points = new Vector3[CircleSegments + 1];
			for (var index = 0; index <= CircleSegments; index++) {
				var angle = math.PI * 2f * index / CircleSegments;
				points[index] = view.ToScreen(center
					+ new Vector3(math.cos(angle) * radius, 0f,
						math.sin(angle) * radius));
			}
			Handles.color = OutlineColor;
			Handles.DrawAAPolyLine(width + 2f, points);
			Handles.color = RailColor;
			Handles.DrawAAPolyLine(width, points);
		}

		private static void DrawPath(LegPreviewView view,
			IReadOnlyList<Vector3> source, float width, Color color)
		{
			if (source == null || source.Count < 2) {
				return;
			}
			var points = new Vector3[source.Count];
			for (var pointIndex = 0; pointIndex < source.Count; pointIndex++) {
				points[pointIndex] = view.ToScreen(source[pointIndex]);
			}
			Handles.color = OutlineColor;
			Handles.DrawAAPolyLine(width + 3f, points);
			Handles.color = color;
			Handles.DrawAAPolyLine(width, points);
		}

		private static void DrawGrid(LegPreviewView view)
		{
			var span = math.max(view.Max.x - view.Min.x, view.Max.y - view.Min.y);
			var gridStep = span > 400f ? 100f : span > 200f ? 50f : span > 100f ? 20f : 10f;
			for (var x = math.ceil(view.Min.x / gridStep) * gridStep;
				x <= view.Max.x; x += gridStep) {
				var screen = view.ToScreenProjected(new Vector2(x, 0f));
				EditorGUI.DrawRect(new Rect(screen.x, view.Rect.y, 1f, view.Rect.height),
					math.abs(x) < 0.01f ? AxisColor : GridColor);
			}
			for (var y = math.ceil(view.Min.y / gridStep) * gridStep;
				y <= view.Max.y; y += gridStep) {
				var screen = view.ToScreenProjected(new Vector2(0f, y));
				EditorGUI.DrawRect(new Rect(view.Rect.x, screen.y, view.Rect.width, 1f),
					math.abs(y) < 0.01f ? AxisColor : GridColor);
			}
		}

		private readonly struct LegPreviewView
		{
			public readonly Rect Rect;
			public readonly Vector2 Min;
			public readonly Vector2 Max;
			public readonly float Scale;

			private LegPreviewView(Rect rect, Vector2 min, Vector2 max, float scale)
			{
				Rect = rect;
				Min = min;
				Max = max;
				Scale = scale;
			}

			public static LegPreviewView Create(Rect rect, WireRailLegPreview preview,
				float tubeRadius)
			{
				var padding = math.max(8f, tubeRadius + 8f);
				var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
				var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
				Include(preview.StartRailOffset, preview.StartRailRadius + padding);
				Include(preview.EndRailOffset, preview.EndRailRadius + padding);
				foreach (var point in preview.CenterlinePoints) {
					Include(point, padding);
				}
				var size = Vector2.Max(max - min, new Vector2(1f, 1f));
				var scale = math.max(0.01f, math.min((rect.width - 20f) / size.x,
					(rect.height - 20f) / size.y));
				var fittedSize = new Vector2((rect.width - 20f) / scale,
					(rect.height - 20f) / scale);
				var center = (min + max) * 0.5f;
				return new LegPreviewView(rect, center - fittedSize * 0.5f,
					center + fittedSize * 0.5f, scale);

				void Include(Vector3 point, float radius)
				{
					var projected = Project(point);
					min = Vector2.Min(min, projected - Vector2.one * radius);
					max = Vector2.Max(max, projected + Vector2.one * radius);
				}
			}

			public Vector3 ToScreen(Vector3 vpx) => ToScreenProjected(Project(vpx));

			public Vector3 ToScreenProjected(Vector2 projected)
				=> new(Rect.x + 10f + (projected.x - Min.x) * Scale,
					Rect.yMax - 10f - (projected.y - Min.y) * Scale, 0f);

			private static Vector2 Project(Vector3 vpx)
				=> new(vpx.x + vpx.y * 0.35f, vpx.z + vpx.y * 0.22f);
		}
	}

	internal sealed class WireRailDropPreviewEditor
	{
		public const float Height = WireRailPathPreview.Height;

		public void Draw(Rect rect, WireRailComponent component, int fixtureIndex,
			WireRailDropFixture drop)
		{
			if (!component.TryGetDropPreview(fixtureIndex, out var preview)) {
				WireRailPathPreview.DrawUnavailable(rect,
					"Drop preview unavailable at this position");
				return;
			}
			WireRailPathPreview.Draw(rect, drop.Diameter,
				"Projected route-local view of the two dropping rails.",
				new[] { preview.FirstRailPoints, preview.SecondRailPoints });
		}
	}

	internal sealed class WireRailDropLoopPreviewEditor
	{
		public const float Height = WireRailPathPreview.Height;

		public void Draw(Rect rect, WireRailComponent component, int fixtureIndex,
			WireRailDropLoopFixture dropLoop)
		{
			if (!component.TryGetDropLoopPreview(fixtureIndex, out var preview)) {
				WireRailPathPreview.DrawUnavailable(rect,
					"Drop Loop preview unavailable at this position");
				return;
			}
			WireRailPathPreview.Draw(rect, dropLoop.Diameter,
				"Top-down route-local view of the drop loop centerline.",
				new[] { preview.CenterlinePoints }, WireRailPreviewProjection.Top);
		}
	}

	// How the shared path preview flattens route-local geometry onto the 2D canvas.
	internal enum WireRailPreviewProjection
	{
		// Skewed 3D look (lateral × height, with a slight along-spline shear).
		Isometric,
		// Straight top-down view (lateral × along-spline), looking down the route's up axis.
		Top,
	}

	// Shared projected route-local preview for the endpoint fittings whose geometry is one or
	// more 3D centerline paths (Drop, Drop Loop), matching the leg preview's isometric look.
	internal static class WireRailPathPreview
	{
		public const float Height = 190f;
		private static readonly Color CanvasColor = new(0.105f, 0.115f, 0.13f, 1f);
		private static readonly Color GridColor = new(1f, 1f, 1f, 0.07f);
		private static readonly Color AxisColor = new(1f, 1f, 1f, 0.28f);
		private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.8f);
		private static readonly Color PathColor = new(1f, 0.67f, 0.12f, 1f);

		public static void DrawUnavailable(Rect rect, string label)
		{
			EditorGUI.DrawRect(rect, CanvasColor);
			EditorGUI.LabelField(rect, label, EditorStyles.centeredGreyMiniLabel);
		}

		public static void Draw(Rect rect, float diameter, string tooltip,
			IReadOnlyList<IReadOnlyList<Vector3>> paths,
			WireRailPreviewProjection projection = WireRailPreviewProjection.Isometric)
		{
			EditorGUI.DrawRect(rect, CanvasColor);
			var view = PathPreviewView.Create(rect, paths, diameter * 0.5f, projection);
			DrawGrid(view);
			var tubeWidth = math.clamp(diameter * view.Scale, 3f, 16f);
			Handles.BeginGUI();
			var previousColor = Handles.color;
			foreach (var path in paths) {
				DrawPath(view, path, tubeWidth);
			}
			Handles.color = previousColor;
			Handles.EndGUI();

			var axisLabel = projection == WireRailPreviewProjection.Top
				? "Top (X / Y)" : "X / Y / Z";
			GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 88f, 18f), axisLabel,
				EditorStyles.miniLabel);
			GUI.Label(rect, new GUIContent(string.Empty, tooltip));
		}

		private static void DrawPath(PathPreviewView view, IReadOnlyList<Vector3> source,
			float width)
		{
			if (source == null || source.Count < 2) {
				return;
			}
			var points = new Vector3[source.Count];
			for (var pointIndex = 0; pointIndex < source.Count; pointIndex++) {
				points[pointIndex] = view.ToScreen(source[pointIndex]);
			}
			Handles.color = OutlineColor;
			Handles.DrawAAPolyLine(width + 3f, points);
			Handles.color = PathColor;
			Handles.DrawAAPolyLine(width, points);
		}

		private static void DrawGrid(PathPreviewView view)
		{
			var span = math.max(view.Max.x - view.Min.x, view.Max.y - view.Min.y);
			var gridStep = span > 400f ? 100f : span > 200f ? 50f : span > 100f ? 20f : 10f;
			for (var x = math.ceil(view.Min.x / gridStep) * gridStep;
				x <= view.Max.x; x += gridStep) {
				var screen = view.ToScreenProjected(new Vector2(x, 0f));
				EditorGUI.DrawRect(new Rect(screen.x, view.Rect.y, 1f, view.Rect.height),
					math.abs(x) < 0.01f ? AxisColor : GridColor);
			}
			for (var y = math.ceil(view.Min.y / gridStep) * gridStep;
				y <= view.Max.y; y += gridStep) {
				var screen = view.ToScreenProjected(new Vector2(0f, y));
				EditorGUI.DrawRect(new Rect(view.Rect.x, screen.y, view.Rect.width, 1f),
					math.abs(y) < 0.01f ? AxisColor : GridColor);
			}
		}

		private readonly struct PathPreviewView
		{
			public readonly Rect Rect;
			public readonly Vector2 Min;
			public readonly Vector2 Max;
			public readonly float Scale;
			private readonly WireRailPreviewProjection _projection;

			private PathPreviewView(Rect rect, Vector2 min, Vector2 max, float scale,
				WireRailPreviewProjection projection)
			{
				Rect = rect;
				Min = min;
				Max = max;
				Scale = scale;
				_projection = projection;
			}

			public static PathPreviewView Create(Rect rect,
				IReadOnlyList<IReadOnlyList<Vector3>> paths, float tubeRadius,
				WireRailPreviewProjection projection)
			{
				var padding = math.max(8f, tubeRadius + 8f);
				var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
				var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
				foreach (var path in paths) {
					if (path == null) {
						continue;
					}
					foreach (var point in path) {
						var projected = Project(point, projection);
						min = Vector2.Min(min, projected - Vector2.one * padding);
						max = Vector2.Max(max, projected + Vector2.one * padding);
					}
				}
				if (min.x > max.x) {
					min = Vector2.zero;
					max = Vector2.one;
				}
				var size = Vector2.Max(max - min, new Vector2(1f, 1f));
				var scale = math.max(0.01f, math.min((rect.width - 20f) / size.x,
					(rect.height - 20f) / size.y));
				var fittedSize = new Vector2((rect.width - 20f) / scale,
					(rect.height - 20f) / scale);
				var center = (min + max) * 0.5f;
				return new PathPreviewView(rect, center - fittedSize * 0.5f,
					center + fittedSize * 0.5f, scale, projection);
			}

			public Vector3 ToScreen(Vector3 vpx) => ToScreenProjected(Project(vpx, _projection));

			public Vector3 ToScreenProjected(Vector2 projected)
				=> new(Rect.x + 10f + (projected.x - Min.x) * Scale,
					Rect.yMax - 10f - (projected.y - Min.y) * Scale, 0f);

			// vpx is route-local: x = lateral (right), y = along-spline (tangent), z = height (up).
			private static Vector2 Project(Vector3 vpx, WireRailPreviewProjection projection)
				=> projection == WireRailPreviewProjection.Top
					? new Vector2(vpx.x, vpx.y)
					: new Vector2(vpx.x + vpx.y * 0.35f, vpx.z + vpx.y * 0.22f);
		}
	}
}
