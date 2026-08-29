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
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Adds VPE's double-click knot workflow to the native spline editing tools.
	/// </summary>
	[InitializeOnLoad]
	internal static class WireRailSplineHandles
	{
		private const float KnotPickDistance = 14f;
		private const float CurvePickDistance = 10f;
		private const float HandleHoverDistance = 15f;
		private const float KnotRadius = 0.085f;
		private const float TangentRadius = 0.065f;
		private const int CurveSamples = 48;
		private const int KnotControlHint = 0x57524b00;
		private const int TangentInControlHint = 0x57525400;
		private const int TangentOutControlHint = 0x57525500;
		private static readonly Color KnotColor = new(0.08f, 0.76f, 1f, 1f);
		private static readonly Color SelectedColor = new(1f, 0.72f, 0.06f, 1f);
		private static readonly Color HoverColor = new(1f, 0.95f, 0.58f, 1f);
		private static readonly Color TangentColor = new(0.08f, 0.84f, 1f, 1f);
		private static readonly Color OutlineColor = new(0.015f, 0.02f, 0.025f, 0.95f);
		private static readonly List<SelectableKnot> SelectedKnots = new();
		private static readonly List<SelectableTangent> SelectedTangents = new();
		private static bool _overriding;
		private static bool _toolVisibilityCaptured;
		private static bool _toolsWereHidden;

		static WireRailSplineHandles()
		{
			// Run before the native handles can claim the second mouse-down on a knot.
			SceneView.beforeSceneGui += OnSceneGUI;
			SceneView.duringSceneGui += DrawHandleVisuals;
			ToolManager.activeContextChanged += OnEditorStateChanged;
			ToolManager.activeToolChanged += OnEditorStateChanged;
			Selection.selectionChanged += OnEditorStateChanged;
			AssemblyReloadEvents.beforeAssemblyReload += RestoreToolVisibility;
			EditorApplication.quitting += RestoreToolVisibility;
		}

		private static void OnEditorStateChanged() => SyncOverride(out _, out _, out _);

		private static void OnSceneGUI(SceneView sceneView)
		{
			if (!SyncOverride(out var component, out var container, out var spline)) {
				return;
			}

			var evt = Event.current;
			if (evt.type != EventType.MouseDown || evt.button != 0 || evt.clickCount != 2
				|| evt.alt || evt.control || evt.command) {
				return;
			}

			var localToWorld = container.transform.localToWorldMatrix;
			if (TryPickKnot(spline, localToWorld, evt.mousePosition, out var knotIndex)) {
				if (!RemoveKnot(component, knotIndex)) {
					var minimum = spline.Closed ? 3 : 2;
					sceneView.ShowNotification(new GUIContent(
						$"A wire rail spline needs at least {minimum} knots."));
				}
				evt.Use();
				return;
			}
			if (TryPickCurve(spline, localToWorld, evt.mousePosition,
					out var segmentIndex, out var curveT)) {
				InsertKnot(component, segmentIndex, curveT);
				evt.Use();
			}
		}

		private static bool TryGetActiveSpline(out WireRailComponent component,
			out SplineContainer container, out Spline spline)
		{
			component = null;
			container = null;
			spline = null;
			if (ToolManager.activeContextType != typeof(SplineToolContext)
				|| !IsTransformTool(ToolManager.activeToolType)) {
				return false;
			}

			var selected = Selection.activeGameObject;
			container = selected ? selected.GetComponent<SplineContainer>() : null;
			component = selected ? selected.GetComponentInParent<WireRailComponent>() : null;
			if (!component || !container || component.SplineContainer != container) {
				return false;
			}
			spline = container.Spline;
			return spline != null && spline.Count >= 2;
		}

		private static void DrawHandleVisuals(SceneView sceneView)
		{
			if (!SyncOverride(out var component, out var container, out var spline)) {
				return;
			}

			var splineInfo = new SplineInfo(container, 0);
			CollectSelection(splineInfo);
			using (new SplineHandles.SplineHandleScope()) {
				SplineHandles.DoSegmentsHandles(splineInfo);
			}
			DrawHandleControls(component, spline, splineInfo);

			if (Event.current.type != EventType.Repaint) {
				return;
			}
			CollectSelection(splineInfo);
			DrawVisualOverlay(sceneView, spline, splineInfo);
		}

		private static void DrawHandleControls(WireRailComponent component, Spline spline,
			SplineInfo splineInfo)
		{
			var canMoveKnots = ToolManager.activeToolType == typeof(SplineMoveTool);
			for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++) {
				var knot = new SelectableKnot(splineInfo, knotIndex);
				var controlId = GUIUtility.GetControlID(KnotControlHint + knotIndex,
					FocusType.Passive);
				var position = ToVector3(knot.Position);
				var size = HandleUtility.GetHandleSize(position) * KnotRadius * 1.35f;
				if (canMoveKnots) {
					var moved = DoFreeMoveControl(component, controlId, position, size,
						knot, "Move Wire Rail Knot");
					if ((moved - position).sqrMagnitude > 1e-10f) {
						knot.Position = new float3(moved.x, moved.y, moved.z);
						Apply(component);
					}
				} else {
					DoSelectionControl(controlId, position, size, knot);
				}
			}

			for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++) {
				var knot = new SelectableKnot(splineInfo, knotIndex);
				if (!IsKnotSelected(knotIndex, knot)
					|| !HasEditableTangents(spline.GetTangentMode(knotIndex))) {
					continue;
				}
				if (spline.Closed || knotIndex != 0) {
					DoTangentControl(component, knot.TangentIn,
						TangentInControlHint + knotIndex);
				}
				if (spline.Closed || knotIndex + 1 != spline.Count) {
					DoTangentControl(component, knot.TangentOut,
						TangentOutControlHint + knotIndex);
				}
			}
		}

		private static void DoTangentControl(WireRailComponent component,
			SelectableTangent tangent, int controlHint)
		{
			var controlId = GUIUtility.GetControlID(controlHint, FocusType.Passive);
			var position = ToVector3(tangent.Position);
			var size = HandleUtility.GetHandleSize(position) * TangentRadius * 1.5f;
			var moved = DoFreeMoveControl(component, controlId, position, size, tangent,
				"Move Wire Rail Tangent");
			if ((moved - position).sqrMagnitude <= 1e-10f) {
				return;
			}

			ApplyTangentPosition(tangent, new float3(moved.x, moved.y, moved.z));
			Apply(component);
		}

		private static Vector3 DoFreeMoveControl<T>(WireRailComponent component,
			int controlId, Vector3 position, float size, T element, string undoName)
			where T : struct, ISelectableElement
		{
			var evt = Event.current;
			if (evt.GetTypeForControl(controlId) == EventType.MouseDown && evt.button == 0
				&& !evt.alt && HandleUtility.nearestControl == controlId) {
				RecordUndo(component, undoName);
				SplineSelection.Set(element);
			}
			return Handles.FreeMoveHandle(controlId, position, size, Vector3.zero,
				InvisibleGripCap);
		}

		private static void DoSelectionControl<T>(int controlId, Vector3 position,
			float size, T element) where T : struct, ISelectableElement
		{
			var evt = Event.current;
			switch (evt.GetTypeForControl(controlId)) {
				case EventType.Layout:
				case EventType.MouseMove:
					InvisibleGripCap(controlId, position, Quaternion.identity, size,
						evt.type);
					break;
				case EventType.MouseDown:
					if (evt.button == 0 && !evt.alt
						&& HandleUtility.nearestControl == controlId) {
						GUIUtility.hotControl = controlId;
						SplineSelection.Set(element);
						evt.Use();
					}
					break;
				case EventType.MouseUp:
					if (evt.button == 0 && GUIUtility.hotControl == controlId) {
						GUIUtility.hotControl = 0;
						evt.Use();
					}
					break;
			}
		}

		private static void InvisibleGripCap(int controlId, Vector3 position,
			Quaternion rotation, float size, EventType eventType)
		{
			if ((eventType == EventType.Layout || eventType == EventType.MouseMove)
				&& !Tools.viewToolActive && Tools.current != Tool.View && !Event.current.alt) {
				HandleUtility.AddControl(controlId,
					HandleUtility.DistanceToCircle(position, size));
			}
		}

		private static void ApplyTangentPosition(SelectableTangent tangent,
			float3 targetPosition)
		{
			var knot = tangent.Owner;
			if (knot.Mode == TangentMode.Broken) {
				tangent.Position = targetPosition;
				return;
			}

			var splineTrs = knot.SplineInfo.LocalToWorld;
			var splineTrsInv = math.inverse(splineTrs);
			var splinePosition = splineTrs.c3.xyz;
			var splineRotation = new quaternion(splineTrs);
			var unscaledTarget = splinePosition + math.rotate(splineRotation,
				math.transform(splineTrsInv, targetPosition));
			var unscaledKnot = splinePosition + math.rotate(splineRotation,
				math.transform(splineTrsInv, knot.Position));
			var knotRotationInv = math.inverse(knot.Rotation);
			var forward = (tangent.TangentIndex == (int)BezierTangent.In ? -1f : 1f)
				* math.normalizesafe(unscaledTarget - unscaledKnot);
			var up = math.mul(knot.Rotation, math.up());
			var targetRotation = quaternion.LookRotationSafe(forward, up);
			var rotationDelta = math.mul(targetRotation, knotRotationInv);
			var targetLocalDirection = math.rotate(knotRotationInv,
				unscaledTarget - unscaledKnot);
			var magnitudeDelta = math.length(targetLocalDirection)
				- math.length(tangent.LocalDirection);

			knot.Rotation = math.mul(rotationDelta, knot.Rotation);
			var localDirection = tangent.LocalDirection;
			var fallbackDirection = tangent.TangentIndex == (int)BezierTangent.In
				? new float3(0f, 0f, -1f) : new float3(0f, 0f, 1f);
			var direction = math.normalizesafe(localDirection, fallbackDirection);
			tangent.LocalDirection = direction
				* math.max(0f, math.length(localDirection) + magnitudeDelta);
		}

		private static void CollectSelection(SplineInfo splineInfo)
		{
			SelectedKnots.Clear();
			SelectedTangents.Clear();
			SplineSelection.GetElements(splineInfo, SelectedKnots);
			SplineSelection.GetElements(splineInfo, SelectedTangents);
		}

		private static void DrawVisualOverlay(SceneView sceneView, Spline spline,
			SplineInfo splineInfo)
		{
			var mouse = Event.current.mousePosition;
			var camera = sceneView ? sceneView.camera : null;
			var normal = camera ? camera.transform.forward : Vector3.up;
			var previousColor = Handles.color;
			var previousZTest = Handles.zTest;
			Handles.zTest = CompareFunction.Always;
			string tooltip = null;

			for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++) {
				var knot = new SelectableKnot(splineInfo, knotIndex);
				if (!IsKnotSelected(knotIndex, knot)
					|| !HasEditableTangents(spline.GetTangentMode(knotIndex))) {
					continue;
				}
				if (spline.Closed || knotIndex != 0) {
					DrawTangentVisual(knot.Position, knot.TangentIn, "Incoming tangent",
						normal, mouse, ref tooltip);
				}
				if (spline.Closed || knotIndex + 1 != spline.Count) {
					DrawTangentVisual(knot.Position, knot.TangentOut, "Outgoing tangent",
						normal, mouse, ref tooltip);
				}
			}

			for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++) {
				var knot = new SelectableKnot(splineInfo, knotIndex);
				var position = ToVector3(knot.Position);
				var hovered = IsHovered(position, mouse);
				var selected = IsKnotSelected(knotIndex, knot);
				var radius = HandleUtility.GetHandleSize(position) * KnotRadius;
				DrawGrip(position, normal, radius, selected, hovered, KnotColor);
				if (hovered) {
					tooltip = $"Knot {knotIndex + 1} · double-click to remove";
				}
			}

			Handles.zTest = previousZTest;
			Handles.color = previousColor;
			if (!string.IsNullOrEmpty(tooltip)) {
				DrawTooltip(mouse, tooltip);
			}
		}

		private static bool SyncOverride(out WireRailComponent component,
			out SplineContainer container, out Spline spline)
		{
			var shouldOverride = TryGetActiveSpline(out component, out container, out spline);
			if (shouldOverride) {
				SplineToolContext.useCustomSplineHandles = true;
			} else if (_overriding && !ToolOwnsHandles(ToolManager.activeToolType)
				&& !SelectionUsesDragPointHandles()) {
				SplineToolContext.useCustomSplineHandles = false;
			}
			SetToolVisibility(shouldOverride
				&& ToolManager.activeToolType == typeof(SplineMoveTool));

			if (shouldOverride != _overriding) {
				_overriding = shouldOverride;
				SceneView.RepaintAll();
			}
			return shouldOverride;
		}

		private static void SetToolVisibility(bool hide)
		{
			if (!hide) {
				RestoreToolVisibility();
				return;
			}
			if (!_toolVisibilityCaptured) {
				_toolsWereHidden = Tools.hidden;
				_toolVisibilityCaptured = true;
			}
			Tools.hidden = true;
		}

		private static void RestoreToolVisibility()
		{
			if (!_toolVisibilityCaptured) {
				return;
			}
			Tools.hidden = _toolsWereHidden;
			_toolVisibilityCaptured = false;
		}

		private static bool SelectionUsesDragPointHandles()
		{
			foreach (var gameObject in Selection.gameObjects) {
				if (gameObject.TryGetComponent<DragPointSplineComponent>(out _)) {
					return true;
				}
			}
			return false;
		}

		private static bool ToolOwnsHandles(Type tool)
			=> tool != null && typeof(SplineTool).IsAssignableFrom(tool)
				&& !IsTransformTool(tool);

		private static bool IsKnotSelected(int knotIndex, SelectableKnot knot)
		{
			if (SelectedKnots.Contains(knot)) {
				return true;
			}
			for (var i = 0; i < SelectedTangents.Count; i++) {
				if (SelectedTangents[i].KnotIndex == knotIndex) {
					return true;
				}
			}
			return false;
		}

		private static bool HasEditableTangents(TangentMode mode)
			=> mode != TangentMode.Linear && mode != TangentMode.AutoSmooth;

		private static void DrawTangentVisual(float3 knotPosition,
			SelectableTangent tangent, string label, Vector3 normal, Vector2 mouse,
			ref string tooltip)
		{
			var start = ToVector3(knotPosition);
			var end = ToVector3(tangent.Position);
			if ((end - start).sqrMagnitude <= 1e-8f) {
				return;
			}

			var selected = SelectedTangents.Contains(tangent);
			var hovered = IsHovered(end, mouse);
			Handles.color = OutlineColor;
			Handles.DrawAAPolyLine(7f, start, end);
			Handles.color = selected ? SelectedColor : TangentColor;
			Handles.DrawAAPolyLine(selected ? 4f : 3.5f, start, end);
			var radius = HandleUtility.GetHandleSize(end) * TangentRadius;
			DrawGrip(end, normal, radius, selected, hovered, TangentColor);
			if (hovered) {
				tooltip = $"{label} · drag to shape the curve";
			}
		}

		private static void DrawGrip(Vector3 position, Vector3 normal, float radius,
			bool selected, bool hovered, Color idleColor)
		{
			Handles.color = OutlineColor;
			Handles.DrawSolidDisc(position, normal, radius * 1.35f);
			Handles.color = hovered ? HoverColor : selected ? SelectedColor : idleColor;
			Handles.DrawSolidDisc(position, normal, radius);
			Handles.color = Color.white;
			Handles.DrawWireDisc(position, normal, radius, 2f);
			Handles.color = OutlineColor;
			Handles.DrawSolidDisc(position, normal, radius * 0.22f);
		}

		private static bool IsHovered(Vector3 worldPosition, Vector2 mouse)
			=> Vector2.Distance(HandleUtility.WorldToGUIPoint(worldPosition), mouse)
				<= HandleHoverDistance;

		private static void DrawTooltip(Vector2 mouse, string text)
		{
			Handles.BeginGUI();
			var content = new GUIContent(text);
			var size = EditorStyles.helpBox.CalcSize(content);
			GUI.Label(new Rect(mouse.x + 16f, mouse.y + 18f, size.x + 10f,
				size.y + 6f), content, EditorStyles.helpBox);
			Handles.EndGUI();
		}

		private static bool IsTransformTool(Type tool)
			=> tool == typeof(SplineMoveTool) || tool == typeof(SplineRotateTool)
				|| tool == typeof(SplineScaleTool);

		private static bool TryPickKnot(Spline spline, Matrix4x4 localToWorld,
			Vector2 mouse, out int knotIndex)
		{
			knotIndex = -1;
			var closest = KnotPickDistance;
			for (var candidate = 0; candidate < spline.Count; candidate++) {
				var world = localToWorld.MultiplyPoint3x4(ToVector3(
					spline[candidate].Position));
				var distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(world), mouse);
				if (distance >= closest) {
					continue;
				}
				closest = distance;
				knotIndex = candidate;
			}
			return knotIndex >= 0;
		}

		private static bool TryPickCurve(Spline spline, Matrix4x4 localToWorld,
			Vector2 mouse, out int segmentIndex, out float curveT)
		{
			segmentIndex = -1;
			curveT = 0f;
			var closest = CurvePickDistance;
			var segmentCount = spline.Closed ? spline.Count : spline.Count - 1;
			for (var candidateSegment = 0; candidateSegment < segmentCount;
				candidateSegment++) {
				var curve = spline.GetCurve(candidateSegment);
				var previousT = 0f;
				var previous = ToGuiPoint(localToWorld, curve.P0);
				for (var sample = 1; sample <= CurveSamples; sample++) {
					var currentT = sample / (float)CurveSamples;
					var current = ToGuiPoint(localToWorld,
						CurveUtility.EvaluatePosition(curve, currentT));
					var line = current - previous;
					var amount = line.sqrMagnitude <= 1e-8f ? 0f
						: math.saturate(Vector2.Dot(mouse - previous, line) / line.sqrMagnitude);
					var distance = Vector2.Distance(mouse, previous + line * amount);
					if (distance < closest) {
						closest = distance;
						segmentIndex = candidateSegment;
						curveT = math.lerp(previousT, currentT, amount);
					}
					previous = current;
					previousT = currentT;
				}
			}
			return segmentIndex >= 0;
		}

		private static Vector2 ToGuiPoint(Matrix4x4 localToWorld, float3 local)
			=> HandleUtility.WorldToGUIPoint(localToWorld.MultiplyPoint3x4(ToVector3(local)));

		private static Vector3 ToVector3(float3 value) => new(value.x, value.y, value.z);

		private static bool InsertKnot(WireRailComponent component, int segmentIndex,
			float curveT)
		{
			var container = component.SplineContainer;
			var spline = container.Spline;
			var segmentCount = spline.Closed ? spline.Count : spline.Count - 1;
			if (segmentIndex < 0 || segmentIndex >= segmentCount) {
				return false;
			}

			curveT = math.saturate(curveT);
			var curve = spline.GetCurve(segmentIndex);
			var position = CurveUtility.EvaluatePosition(curve, curveT);
			var knotT = segmentIndex + curveT;
			var normalizedT = SplineUtility.GetNormalizedInterpolation(spline, knotT,
				PathIndexUnit.Knot);
			spline.Evaluate(normalizedT, out _, out var tangent, out var up);
			tangent = math.normalizesafe(tangent, new float3(0f, 1f, 0f));
			up -= tangent * math.dot(up, tangent);
			up = math.normalizesafe(up, new float3(0f, 0f, 1f));
			var rotation = quaternion.LookRotationSafe(tangent, up);
			var knotIndex = spline.Closed && segmentIndex == spline.Count - 1
				? 0 : segmentIndex + 1;

			RecordUndo(component, "Add Wire Rail Knot");
			spline.Insert(knotIndex, new BezierKnot(position) { Rotation = rotation },
				TangentMode.AutoSmooth);
			Apply(component);
			SplineSelection.Set(new SelectableKnot(new SplineInfo(container, 0),
				knotIndex));
			return true;
		}

		private static bool RemoveKnot(WireRailComponent component, int knotIndex)
		{
			var container = component.SplineContainer;
			var spline = container.Spline;
			if (knotIndex < 0 || knotIndex >= spline.Count) {
				return false;
			}
			var minimum = spline.Closed ? 3 : 2;
			if (spline.Count <= minimum) {
				return false;
			}

			RecordUndo(component, "Remove Wire Rail Knot");
			spline.RemoveAt(knotIndex);
			Apply(component);
			SplineSelection.Set(new SelectableKnot(new SplineInfo(container, 0),
				math.min(knotIndex, spline.Count - 1)));
			return true;
		}

		private static void RecordUndo(WireRailComponent component, string name)
		{
			Undo.RecordObjects(new UnityEngine.Object[] { component, component.SplineContainer },
				name);
		}

		private static void Apply(WireRailComponent component)
		{
			EditorUtility.SetDirty(component.SplineContainer);
			EditorUtility.SetDirty(component);
			PrefabUtility.RecordPrefabInstancePropertyModifications(component.SplineContainer);
			PrefabUtility.RecordPrefabInstancePropertyModifications(component);
			SceneView.RepaintAll();
		}
	}
}
