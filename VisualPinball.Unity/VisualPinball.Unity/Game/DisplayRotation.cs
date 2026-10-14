// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VisualPinball.Unity
{
	public enum DisplayRotationMode
	{
		/// <summary>The image is shown as rendered.</summary>
		None = 0,
		/// <summary>The image is turned 90° clockwise: its top sits on the screen's right edge.</summary>
		Clockwise = 1,
		/// <summary>The image is turned 90° counter-clockwise: its top sits on the screen's left edge.</summary>
		CounterClockwise = 2,
	}

	/// <summary>
	/// Turns the game output by a quarter, for landscape screens mounted on their side while the OS stays in
	/// landscape (the usual cabinet setup).
	/// </summary>
	///
	/// <remarks>
	/// Outside of rendering, cameras stay <i>logical</i>: their transform is unrolled, their field of view is
	/// the authored one and their aspect is the one of the turned (portrait) image, so camera scripts keep
	/// working unchanged. Only while a camera renders to the screen is it rolled about its view axis and its
	/// lens turned, which keeps the projection a regular frustum the render pipelines fully support, at no
	/// extra cost. Objects parented to such a camera roll with it during rendering.
	///
	/// Screen-space input such as the mouse is in physical screen pixels. Convert it with
	/// <see cref="ToLogicalDelta"/>, <see cref="ToLogicalViewport"/> or <see cref="ScreenPointToRay"/>.
	/// </remarks>
	public static class DisplayRotation
	{
		public static DisplayRotationMode Mode { get; private set; }

		public static bool IsRotated => Mode != DisplayRotationMode.None;

		private static readonly Dictionary<Camera, CameraState> Applied = new();
		private static readonly HashSet<Camera> Touched = new();
		private static int _suspended;
		private static bool _hooked;

		public static void SetMode(DisplayRotationMode mode)
		{
			Mode = mode;
			if (IsRotated) {
				Hook();
			} else {
				ResetCameras();
			}
		}

		/// <summary>
		/// Renders cameras unturned until the returned scope is disposed, e.g. for a capture into a texture
		/// with the logical (turned) size.
		/// </summary>
		public static SuspendScope Suspend() => new(true);

		public readonly struct SuspendScope : System.IDisposable
		{
			private readonly bool _active;
			internal SuspendScope(bool active)
			{
				_active = active;
				if (active) {
					_suspended++;
				}
			}
			public void Dispose()
			{
				if (_active) {
					_suspended--;
				}
			}
		}

		/// <summary>The size of the logical (turned) image for a screen of the given size.</summary>
		public static Vector2Int LogicalSize(int screenWidth, int screenHeight)
			=> IsRotated ? new Vector2Int(screenHeight, screenWidth) : new Vector2Int(screenWidth, screenHeight);

		/// <summary>Turns a screen-space movement (pixels, y up) into the logical image's frame.</summary>
		public static Vector2 ToLogicalDelta(Vector2 screenDelta) => Mode switch {
			DisplayRotationMode.Clockwise => new Vector2(-screenDelta.y, screenDelta.x),
			DisplayRotationMode.CounterClockwise => new Vector2(screenDelta.y, -screenDelta.x),
			_ => screenDelta,
		};

		/// <summary>Turns a screen viewport position (0..1, y up) into the logical image's viewport.</summary>
		public static Vector2 ToLogicalViewport(Vector2 viewport) => Mode switch {
			DisplayRotationMode.Clockwise => new Vector2(1f - viewport.y, viewport.x),
			DisplayRotationMode.CounterClockwise => new Vector2(viewport.y, 1f - viewport.x),
			_ => viewport,
		};

		/// <summary>Like <see cref="Camera.ScreenPointToRay(Vector3)"/>, for a position in screen pixels.</summary>
		public static Ray ScreenPointToRay(Camera camera, Vector2 screenPosition)
		{
			if (!IsRotated || !Touched.Contains(camera)) {
				return camera.ScreenPointToRay(screenPosition);
			}
			var rect = camera.pixelRect;
			var viewport = new Vector2((screenPosition.x - rect.x) / rect.width, (screenPosition.y - rect.y) / rect.height);
			return camera.ViewportPointToRay(ToLogicalViewport(viewport));
		}

		private static void Hook()
		{
			if (_hooked) {
				return;
			}
			RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
			RenderPipelineManager.endContextRendering += OnEndContextRendering;
			_hooked = true;
		}

		private static void ResetCameras()
		{
			foreach (var (camera, state) in Applied) {
				if (camera != null) {
					state.Restore(camera);
				}
			}
			Applied.Clear();
			foreach (var camera in Touched) {
				if (camera != null) {
					camera.ResetAspect();
				}
			}
			Touched.Clear();
			if (_hooked) {
				RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
				RenderPipelineManager.endContextRendering -= OnEndContextRendering;
				_hooked = false;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			SetMode(DisplayRotationMode.None);
			_suspended = 0;
		}

		// Only the game cameras drawing on the main screen are turned; the scene view, reflection probes and
		// cameras rendering into textures keep their image.
		private static bool IsScreenCamera(Camera camera)
			=> camera.cameraType == CameraType.Game && camera.targetTexture == null && camera.targetDisplay == 0;

		// Per frame rather than per camera: HDRP derives a camera's view, projection and culling before it
		// raises beginCameraRendering, so the camera must be turned before the pipeline starts on it.
		private static void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
		{
			foreach (var camera in cameras) {
				if (camera != null && IsScreenCamera(camera)) {
					Turn(camera);
				}
			}
		}

		private static void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
		{
			foreach (var camera in cameras) {
				if (camera != null && Applied.Remove(camera, out var state)) {
					state.Restore(camera);
				}
			}
		}

		private static void Turn(Camera camera)
		{
			if (Applied.Remove(camera, out var missed)) {
				missed.Restore(camera);
			}
			Touched.Add(camera);
			if (_suspended > 0) {
				camera.aspect = LogicalAspect(camera);
				return;
			}

			var state = CameraState.Capture(camera);
			Applied[camera] = state;
			camera.ResetAspect();
			var screenAspect = camera.aspect;

			// The screen's horizontal axis shows the image's vertical one and vice versa.
			if (camera.usePhysicalProperties) {
				camera.sensorSize = new Vector2(state.SensorSize.y, state.SensorSize.x);
				camera.lensShift = ToScreenFrame(state.LensShift);
				camera.gateFit = state.GateFit switch {
					Camera.GateFitMode.Vertical => Camera.GateFitMode.Horizontal,
					Camera.GateFitMode.Horizontal => Camera.GateFitMode.Vertical,
					_ => state.GateFit,
				};
			} else if (camera.orthographic) {
				camera.orthographicSize = state.OrthographicSize / screenAspect;
			} else {
				var tanHalf = Mathf.Tan(state.FieldOfView * 0.5f * Mathf.Deg2Rad);
				camera.fieldOfView = 2f * Mathf.Atan(tanHalf / screenAspect) * Mathf.Rad2Deg;
			}
			// Roll the transform rather than override the view matrix: parts of the pipelines (e.g. HDRP's
			// ray tracing culling) read the camera's axes from its transform.
			var transform = camera.transform;
			transform.localRotation = state.LocalRotation * Quaternion.Euler(0f, 0f, Mode == DisplayRotationMode.Clockwise ? 90f : -90f);
		}

		private static float LogicalAspect(Camera camera)
		{
			var rect = camera.pixelRect;
			return rect.width > 0f ? rect.height / rect.width : 1f;
		}

		private static Vector2 ToScreenFrame(Vector2 v) => Mode switch {
			DisplayRotationMode.Clockwise => new Vector2(v.y, -v.x),
			DisplayRotationMode.CounterClockwise => new Vector2(-v.y, v.x),
			_ => v,
		};

		private readonly struct CameraState
		{
			public readonly float FieldOfView;
			public readonly float OrthographicSize;
			public readonly Vector2 SensorSize;
			public readonly Vector2 LensShift;
			public readonly Camera.GateFitMode GateFit;
			public readonly Quaternion LocalRotation;

			private CameraState(Camera camera)
			{
				LocalRotation = camera.transform.localRotation;
				FieldOfView = camera.fieldOfView;
				OrthographicSize = camera.orthographicSize;
				SensorSize = camera.sensorSize;
				LensShift = camera.lensShift;
				GateFit = camera.gateFit;
			}

			public static CameraState Capture(Camera camera) => new(camera);

			// Back to the logical camera the scripts see between frames.
			public void Restore(Camera camera)
			{
				if (camera.usePhysicalProperties) {
					camera.sensorSize = SensorSize;
					camera.lensShift = LensShift;
					camera.gateFit = GateFit;
				} else if (camera.orthographic) {
					camera.orthographicSize = OrthographicSize;
				} else {
					camera.fieldOfView = FieldOfView;
				}
				camera.transform.localRotation = LocalRotation;
				camera.aspect = LogicalAspect(camera);
			}
		}
	}
}
