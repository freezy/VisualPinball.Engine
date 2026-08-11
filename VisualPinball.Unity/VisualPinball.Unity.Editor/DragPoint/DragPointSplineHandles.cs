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
	/// Draws the scene view handles for drag-point splines instead of letting the Splines package
	/// draw its default set.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// Drag points have no tangents. <see cref="DragPointSplineConverter"/> derives both tangents of
	/// every knot from the knot positions and the per-point smooth flag, and
	/// <see cref="DragPointSplineConverter.ToDragPoints"/> reads back nothing but positions. A tangent
	/// dragged in the scene view therefore never reaches the generated mesh or the colliders, which
	/// leaves the drawn curve and the actual geometry disagreeing.
	/// </para>
	/// <para>
	/// The tangents still have to be stored as <see cref="TangentMode.Broken"/>, because that is the
	/// only mode under which <c>Spline</c> leaves authored tangent values alone - and reproducing VPX's
	/// centripetal Catmull-Rom shape requires exactly that. Broken is also the mode for which the
	/// package draws tangent handles, so the handles are suppressed here rather than through the
	/// tangent mode.
	/// </para>
	/// <para>
	/// Knots keep the package's own handles so selection, hovering and the transform tools behave
	/// normally; they are merely backed by a larger ring that stays legible on top of a textured
	/// playfield.
	/// </para>
	/// </remarks>
	[InitializeOnLoad]
	internal static class DragPointSplineHandles
	{
		internal const string HandleScaleKey = "VisualPinball.Unity.Editor.DragPointSplineHandleScale";
		internal const float DefaultHandleScale = 2.5f;
		internal const float MinHandleScale = 1f;
		internal const float MaxHandleScale = 6f;

		/// <summary>Radius of an unscaled knot handle, relative to <see cref="HandleUtility.GetHandleSize"/>.</summary>
		private const float KnotRadius = 0.06f;
		private const float RingWidth = 2f;
		private const float OutlineWidth = 5f;

		/// <summary>Screen-space radius, in pixels, for double-click picking.</summary>
		private const float KnotPickDistance = 14f;
		private const float CurvePickDistance = 10f;
		private const int CurveSamples = 32;

		private static readonly Color RingColor = new(0.98f, 0.75f, 0.16f, 1f);
		private static readonly Color OutlineColor = new(0f, 0f, 0f, 0.65f);

		private static readonly List<DragPointSplineComponent> Components = new();

		private static bool _overriding;
		private static float _handleScale = -1f;

		internal static float HandleScale {
			get {
				if (_handleScale < 0f) {
					_handleScale = EditorPrefs.GetFloat(HandleScaleKey, DefaultHandleScale);
				}
				return _handleScale;
			}
			set {
				_handleScale = Mathf.Clamp(value, MinHandleScale, MaxHandleScale);
				EditorPrefs.SetFloat(HandleScaleKey, _handleScale);
			}
		}

		static DragPointSplineHandles()
		{
			SceneView.duringSceneGui += OnSceneGui;
			ToolManager.activeContextChanged += OnEditorStateChanged;
			ToolManager.activeToolChanged += OnEditorStateChanged;
			Selection.selectionChanged += OnEditorStateChanged;
		}

		/// <summary>
		/// Keeps the tool context in sync ahead of the next scene GUI pass, so the default handles are
		/// never drawn for a frame after the selection or the tool context changed.
		/// </summary>
		private static void OnEditorStateChanged() => SyncOverride();

		private static void OnSceneGui(SceneView sceneView)
		{
			if (!SyncOverride()) {
				return;
			}

			// Before the knot handles below claim the mouse down, so a double click on a knot reaches us
			// instead of starting a drag.
			foreach (var component in Components) {
				if (HandleDoubleClick(component)) {
					return;
				}
			}

			foreach (var component in Components) {
				DrawHandles(component, sceneView);
			}
		}

		/// <summary>
		/// Double click adds and removes knots: on a segment it inserts one where you clicked, on an
		/// existing knot it deletes it. The package only offers knot insertion through the draw tool,
		/// which appends rather than inserts and is near impossible to find.
		/// </summary>
		/// <returns>True when the click was consumed.</returns>
		private static bool HandleDoubleClick(DragPointSplineComponent component)
		{
			var evt = Event.current;
			if (evt.type != EventType.MouseDown || evt.button != 0 || evt.clickCount != 2
				|| evt.alt || evt.control || evt.command) {
				return false;
			}

			var container = component.Container;
			var spline = container ? container.Spline : null;
			if (spline == null || spline.Count == 0) {
				return false;
			}

			var localToWorld = container.transform.localToWorldMatrix;
			if (TryPickKnot(spline, localToWorld, evt.mousePosition, out var knotIndex)) {
				RemoveKnot(component, knotIndex);
				evt.Use();
				return true;
			}

			if (TryPickCurve(spline, localToWorld, evt.mousePosition, out var segment, out var position)) {
				InsertKnot(component, segment, position);
				evt.Use();
				return true;
			}
			return false;
		}

		private static bool TryPickKnot(Spline spline, Matrix4x4 localToWorld, Vector2 mouse,
			out int knotIndex)
		{
			knotIndex = -1;
			var closest = KnotPickDistance;
			for (var i = 0; i < spline.Count; i++) {
				var world = localToWorld.MultiplyPoint3x4(ToVector3(spline[i].Position));
				var distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(world), mouse);
				if (distance < closest) {
					closest = distance;
					knotIndex = i;
				}
			}
			return knotIndex >= 0;
		}

		/// <summary>
		/// Finds the segment under the cursor and the point on it, by sampling each curve and measuring
		/// in screen space - so picking matches what is drawn regardless of the viewing angle.
		/// </summary>
		private static bool TryPickCurve(Spline spline, Matrix4x4 localToWorld, Vector2 mouse,
			out int segment, out float3 position)
		{
			segment = -1;
			position = float3.zero;
			var closest = CurvePickDistance;
			var segmentCount = spline.Closed ? spline.Count : spline.Count - 1;
			for (var i = 0; i < segmentCount; i++) {
				var curve = spline.GetCurve(i);
				for (var sample = 1; sample < CurveSamples; sample++) {
					var local = CurveUtility.EvaluatePosition(curve, sample / (float)CurveSamples);
					var world = localToWorld.MultiplyPoint3x4(ToVector3(local));
					var distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(world), mouse);
					if (distance < closest) {
						closest = distance;
						segment = i;
						position = local;
					}
				}
			}
			return segment >= 0;
		}

		private static void InsertKnot(DragPointSplineComponent component, int segment, float3 position)
		{
			var container = component.Container;
			RecordUndo(component, "Add Drag Point");
			// The new knot belongs after the segment's first knot. Tangents are recomputed from the
			// positions by the change handler, so whatever is passed here is discarded.
			container.Spline.Insert(segment + 1, new BezierKnot(position), TangentMode.Broken);
			EditorUtility.SetDirty(container);
			SceneView.RepaintAll();
		}

		private static void RemoveKnot(DragPointSplineComponent component, int knotIndex)
		{
			var container = component.Container;
			var spline = container.Spline;
			// A closed shape needs three knots to stay a shape, an open one needs two to stay a line.
			var minimum = spline.Closed ? 3 : 2;
			if (spline.Count <= minimum) {
				Debug.LogWarning(
					$"Cannot remove drag point: '{component.Owner?.SplineOwner.name}' needs at least " +
					$"{minimum} of them.", component.Owner?.SplineOwner);
				return;
			}

			RecordUndo(component, "Remove Drag Point");
			spline.RemoveAt(knotIndex);
			EditorUtility.SetDirty(container);
			SceneView.RepaintAll();
		}

		private static void RecordUndo(DragPointSplineComponent component, string name)
		{
			var objects = new List<UnityEngine.Object> { component, component.Container };
			var owner = component.Owner;
			if (owner != null) {
				objects.Add(owner.SplineOwner);
			}
			Undo.RecordObjects(objects.ToArray(), name);
		}

		private static Vector3 ToVector3(float3 value) => new(value.x, value.y, value.z);

		/// <summary>
		/// Collects the selected drag-point splines and tells the tool context whether it should keep
		/// its own handles to itself.
		/// </summary>
		/// <returns>True when this class is responsible for drawing.</returns>
		private static bool SyncOverride()
		{
			var shouldOverride = CollectComponents();
			if (shouldOverride) {
				// Re-assert every pass rather than only on change: KnotPlacementTool owns the same
				// global and clears it when it is deactivated, which would otherwise bring the
				// default tangent handles back behind our backs.
				SplineToolContext.useCustomSplineHandles = true;

			} else if (_overriding && !ToolOwnsHandles(ToolManager.activeToolType)) {
				// Only hand the flag back when nobody else is driving it. The knot placement tool sets it
				// on activation, and activeToolChanged reaches us afterwards - clearing it here would
				// undo that and draw the default handles over the tool's own.
				SplineToolContext.useCustomSplineHandles = false;
			}

			if (shouldOverride != _overriding) {
				_overriding = shouldOverride;
				SceneView.RepaintAll();
			}
			return shouldOverride;
		}

		/// <summary>
		/// Fills <see cref="Components"/> with the selected drag-point splines. Bails out as soon as a
		/// spline is selected that VPE does not own, so foreign splines keep their default handles.
		/// </summary>
		private static bool CollectComponents()
		{
			Components.Clear();
			if (ToolManager.activeContextType != typeof(SplineToolContext)) {
				return false;
			}

			// The knot placement tool draws its own handles and manages the same global flag. Leave
			// it alone so knots can still be inserted, and only take over for the transform tools.
			if (ToolOwnsHandles(ToolManager.activeToolType)
				|| !IsTransformTool(ToolManager.activeToolType)) {
				return false;
			}

			foreach (var gameObject in Selection.gameObjects) {
				if (!gameObject.TryGetComponent<SplineContainer>(out _)) {
					continue;
				}
				if (!gameObject.TryGetComponent<DragPointSplineComponent>(out var component)
					|| !component.Container) {
					Components.Clear();
					return false;
				}
				Components.Add(component);
			}
			return Components.Count > 0;
		}

		private static bool IsTransformTool(Type tool)
		{
			return tool == typeof(SplineMoveTool) || tool == typeof(SplineRotateTool)
				|| tool == typeof(SplineScaleTool);
		}

		/// <summary>
		/// True for spline tools that draw their own handles and set
		/// <see cref="SplineToolContext.useCustomSplineHandles"/> themselves - the knot placement tools.
		/// Those must be left to manage the flag on their own.
		/// </summary>
		private static bool ToolOwnsHandles(Type tool)
		{
			return tool != null && typeof(SplineTool).IsAssignableFrom(tool) && !IsTransformTool(tool);
		}

		private static void DrawHandles(DragPointSplineComponent component, SceneView sceneView)
		{
			var container = component.Container;
			var spline = container.Spline;
			if (spline == null || spline.Count == 0) {
				return;
			}

			var splineInfo = new SplineInfo(container, 0);
			using (new SplineHandles.SplineHandleScope()) {
				SplineHandles.DoSegmentsHandles(splineInfo);
				DrawKnotRings(container, sceneView);
				for (var i = 0; i < spline.Count; i++) {
					SplineHandles.DoKnotHandles(new SelectableKnot(splineInfo, i));
				}
			}
		}

		/// <summary>
		/// Draws an always-on-top ring behind every knot handle so it stays visible against a lit,
		/// textured playfield. Purely visual - picking still uses the package's own handle.
		/// </summary>
		private static void DrawKnotRings(SplineContainer container, SceneView sceneView)
		{
			var scale = HandleScale;
			if (Event.current.type != EventType.Repaint || scale <= MinHandleScale) {
				return;
			}

			var spline = container.Spline;
			var matrix = container.transform.localToWorldMatrix;
			var camera = sceneView ? sceneView.camera : null;
			var normal = camera ? camera.transform.forward : Vector3.up;
			var previousColor = Handles.color;
			var previousZTest = Handles.zTest;
			Handles.zTest = CompareFunction.Always;

			for (var i = 0; i < spline.Count; i++) {
				var position = spline[i].Position;
				var world = matrix.MultiplyPoint3x4(new Vector3(position.x, position.y, position.z));
				var radius = HandleUtility.GetHandleSize(world) * KnotRadius * scale;
				Handles.color = OutlineColor;
				Handles.DrawWireDisc(world, normal, radius, OutlineWidth);
				Handles.color = RingColor;
				Handles.DrawWireDisc(world, normal, radius, RingWidth);
			}

			Handles.zTest = previousZTest;
			Handles.color = previousColor;
		}
	}
}
