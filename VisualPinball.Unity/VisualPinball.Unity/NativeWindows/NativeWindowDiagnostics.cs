// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System.Runtime.InteropServices;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Snapshot of a native window's state, matching the native <c>VPNW_Diagnostics</c> layout (80 bytes).
	/// </summary>
	/// <remarks>
	/// Coordinates and sizes are physical desktop pixels. Boolean fields are 0 or 1.
	/// <para>
	/// Returned by <see cref="NativeWindowSurface.Diagnostics"/>, which crosses into the window system, so query it for
	/// troubleshooting or occasional polling, not every frame. Besides logging, the settings windows of PinUP and the B2S
	/// backglass read the actual position and size from it after the user dragged or resized a window in placement mode.
	/// </para>
	/// <example>
	/// <code>
	/// var state = surface.Diagnostics;
	/// var bounds = new RectInt(state.Left, state.Top, state.WindowWidth, state.WindowHeight);
	/// Debug.Log($"{state.PresentedFrames} frames presented, {state.SkippedFrames} skipped, last result 0x{state.LastResult:x8}");
	/// </code>
	/// </example>
	/// </remarks>
	[StructLayout(LayoutKind.Sequential)]
	public struct NativeWindowDiagnostics
	{
		/// <summary>
		/// Size of this structure in bytes. <see cref="NativeWindowSurface.Diagnostics"/> fills it in before the native call,
		/// which rejects a size other than 80.
		/// </summary>
		public uint StructBytes;
		/// <summary>UnityGfxRenderer value seen by the native plugin.</summary>
		public int GraphicsRenderer;
		/// <summary>1 when the native plugin has a graphics device to present with, otherwise 0.</summary>
		public int DeviceReady;
		/// <summary>1 while the native window can present; 0 on Linux after a presentation failure stopped it.</summary>
		public int Alive;
		/// <summary>1 when the window is shown, otherwise 0.</summary>
		public int Visible;
		/// <summary>1 when the window stays above normal windows, otherwise 0.</summary>
		public int Topmost;
		/// <summary>Left edge of the window in physical desktop pixels; negative left of the primary monitor.</summary>
		public int Left;
		/// <summary>Top edge of the window in physical desktop pixels; negative above the primary monitor.</summary>
		public int Top;
		/// <summary>Actual window width in physical pixels.</summary>
		public int WindowWidth;
		/// <summary>Actual window height in physical pixels.</summary>
		public int WindowHeight;
		/// <summary>DPI of the monitor the window is on (96 is 100 % scaling).</summary>
		public uint Dpi;
		/// <summary>Width of the source texture in pixels, or 0 when no texture is assigned.</summary>
		public uint SourceWidth;
		/// <summary>Height of the source texture in pixels, or 0 when no texture is assigned.</summary>
		public uint SourceHeight;
		/// <summary>Platform texture format: DXGI_FORMAT, MTLPixelFormat or VkFormat.</summary>
		public uint SourceFormat;
		/// <summary>Number of frames presented to the window since it was created.</summary>
		public ulong PresentedFrames;
		/// <summary>
		/// Number of present events that did not reach the window, for example because the display was not ready yet.
		/// Presentation never waits for the display, so a count that keeps rising while the window is visible and has a source
		/// means it cannot keep up with Unity's frame rate.
		/// </summary>
		public ulong SkippedFrames;
		/// <summary>Windows: last presentation HRESULT. macOS/Linux: 0, or negative after a presentation failure.</summary>
		public int LastResult;
		/// <summary>1 when the window composes its content with premultiplied alpha, otherwise 0.</summary>
		public int PremultipliedAlpha;
	}
}
