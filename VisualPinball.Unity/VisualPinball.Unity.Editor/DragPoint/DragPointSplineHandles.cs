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

			foreach (var component in Components) {
				DrawHandles(component, sceneView);
			}
		}

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

			} else if (_overriding) {
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
			var tool = ToolManager.activeToolType;
			if (tool != typeof(SplineMoveTool) && tool != typeof(SplineRotateTool)
				&& tool != typeof(SplineScaleTool)) {
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
