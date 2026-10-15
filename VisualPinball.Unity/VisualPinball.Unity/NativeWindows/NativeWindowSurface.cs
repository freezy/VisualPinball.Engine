// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A borderless desktop window presenting a Unity texture on the GPU: Direct3D11/12 through DirectComposition on
	/// Windows, Metal on macOS, and Vulkan on X11/XWayland on Linux.
	/// </summary>
	/// <remarks>
	/// Coordinates are signed physical desktop pixels; monitors left of or above the primary monitor have negative
	/// origins. A new window is hidden until <see cref="Configure"/> shows it. Register the surface with
	/// <see cref="NativeWindowHost"/>, which issues <see cref="DrawEventId"/> and <see cref="PresentEventId"/> once
	/// per frame for all surfaces. Dispose the surface before releasing its RenderTexture. The wrapper has explicit
	/// ownership and no finalizer.
	/// <para>
	/// Plugins use it to put a premultiplied RGBA RenderTexture on a second or third monitor, with transparent areas
	/// showing the desktop behind: PinUP opens one per PUP screen, the B2S plugin one for the backglass and one for the
	/// B2S DMD, and both open one in placement mode for the Player's screen layout editor (see
	/// <see cref="IUnitySettingsWindow"/>). The texture never leaves the GPU. Use the instance from the main thread, as
	/// all VPE users do. Instance members throw <see cref="ObjectDisposedException"/> after <see cref="Dispose"/>, and
	/// <see cref="InvalidOperationException"/> carrying <see cref="LastError"/> when the native call fails.
	/// </para>
	/// <example>
	/// <code>
	/// if (!NativeWindowSurface.IsSupported) {
	///     return; // Missing library or unsupported graphics backend; never throws.
	/// }
	/// var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
	/// texture.Create();
	/// var surface = new NativeWindowSurface(x, y, width, height); // hidden
	/// surface.SetTexture(texture.GetNativeTexturePtr());
	/// surface.Configure(x, y, width, height, visible: true, topmost: false);
	/// var lease = NativeWindowHost.Register(surface);
	///
	/// // Shutdown: unregister, destroy the window, then release the texture.
	/// lease.Dispose();
	/// surface.Dispose();
	/// texture.Release();
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class NativeWindowSurface : IDisposable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal const string LibraryName = "VisualPinballNativeWindows";

		// UnityGfxRenderer values of the supported graphics backends.
		private const int RendererD3D11 = 2;
		private const int RendererMetal = 16;
		private const int RendererD3D12 = 18;
		private const int RendererVulkan = 21;

		/// <summary>Native registry ID; zero after disposal. IDs are never reused while the plugin is loaded.</summary>
		public int Id { get; private set; }

		private bool _configured, _visible, _topmost;
		private int _x, _y, _width, _height;

		private static bool? _libraryAvailable;

		#region Static API

		/// <summary>
		/// True when the native library is present and Unity runs a graphics backend the platform's native windows
		/// support: Direct3D11/12 on Windows, Metal on macOS, Vulkan with an X11 display on Linux.
		/// </summary>
		/// <remarks>Never throws; a missing native library reports false.</remarks>
		public static bool IsSupported
		{
			get {
				if (IntPtr.Size != 8 || !LibraryAvailable || (Capabilities & NativeWindowCapabilities.Windows) == 0) {
					return false;
				}
				var renderer = GraphicsRenderer;
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
					return renderer == RendererD3D11 || renderer == RendererD3D12;
				}
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
					return renderer == RendererMetal;
				}
				return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && renderer == RendererVulkan;
			}
		}

		/// <summary>
		/// Unity's UnityGfxRenderer as seen by the native plugin, or -1 when the library is unavailable. The supported values
		/// are 2 (Direct3D11), 16 (Metal), 18 (Direct3D12) and 21 (Vulkan). Useful in log messages when
		/// <see cref="IsSupported"/> is false.
		/// </summary>
		public static int GraphicsRenderer => LibraryAvailable ? Native.GetGraphicsRenderer() : -1;

		/// <summary>
		/// Window features of the current platform and session, or <see cref="NativeWindowCapabilities.None"/> when the
		/// library is unavailable.
		/// </summary>
		public static NativeWindowCapabilities Capabilities => LibraryAvailable
			? (NativeWindowCapabilities)Native.GetCapabilities()
			: NativeWindowCapabilities.None;

		/// <summary>
		/// Unity rendering callback for <c>GL.IssuePluginEvent</c>, or <see cref="IntPtr.Zero"/> when the library is
		/// unavailable. Issue <see cref="DrawEventId"/> and then <see cref="PresentEventId"/> with it. Plugins do not need it:
		/// <see cref="NativeWindowHost"/> issues the events.
		/// </summary>
		public static IntPtr RenderEventFunction => LibraryAvailable ? Native.GetRenderEventFunc() : IntPtr.Zero;

		/// <summary>
		/// Event ID of the per-frame draw step for all windows. Submit after the frame's GPU work, followed immediately by
		/// <see cref="PresentEventId"/>. Operates on all surfaces. 0 when the library is unavailable.
		/// </summary>
		public static int DrawEventId => LibraryAvailable ? Native.GetDrawEventId() : 0;

		/// <summary>
		/// Event ID that presents all surfaces; submit right after <see cref="DrawEventId"/>, once per frame. 0 when the
		/// library is unavailable.
		/// </summary>
		public static int PresentEventId => LibraryAvailable ? Native.GetPresentEventId() : 0;

		/// <summary>
		/// Retired GPU source and presentation objects awaiting later render events. Keep issuing event pairs until zero.
		/// <see cref="NativeWindowHost"/> does this after its last surface is unregistered. 0 when the library is unavailable.
		/// </summary>
		public static int PendingResourceCount => LibraryAvailable ? Native.GetPendingResourceCount() : 0;

		/// <summary>
		/// Processes platform window events (Linux placement dragging). Call once per frame from the main thread. A no-op on
		/// Windows and macOS and when the library is unavailable. <see cref="NativeWindowHost"/> calls it every frame.
		/// </summary>
		public static void Pump()
		{
			if (LibraryAvailable) {
				Native.Pump();
			}
		}

		/// <summary>Destroys all native windows, for shutdown or domain-reload cleanup by the host.</summary>
		/// <remarks>
		/// This affects the windows of every plugin. Existing <see cref="NativeWindowSurface"/> instances keep their
		/// <see cref="Id"/>, so their other members then fail; <see cref="Dispose"/> stays safe because native destruction is
		/// idempotent. GPU resources still retire through later render events. Plugins should dispose their own surfaces
		/// instead.
		/// </remarks>
		public static void DestroyAll()
		{
			if (LibraryAvailable) {
				Native.DestroyAll();
			}
		}

		/// <summary>
		/// The last native error message (UTF-8), for diagnostics, or a message naming the missing library when it is
		/// unavailable. The native library keeps one message for all windows, so read it right after a failure.
		/// </summary>
		public static string LastError
		{
			get {
				if (!LibraryAvailable) {
					return $"The native library {LibraryName} is not available.";
				}
				var bytes = new byte[System.Math.Max(1, Native.GetLastError(null, 0))];
				Native.GetLastError(bytes, bytes.Length);
				var length = Array.IndexOf(bytes, (byte)0);
				return Encoding.UTF8.GetString(bytes, 0, length < 0 ? bytes.Length : length);
			}
		}

		private static bool LibraryAvailable
		{
			get {
				if (_libraryAvailable.HasValue) {
					return _libraryAvailable.Value;
				}
				try {
					Native.GetCapabilities();
					_libraryAvailable = true;

				} catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException) {
					_libraryAvailable = false;
					Logger.Warn(e, "Native window library {Library:l} is not available, native display windows are disabled.", LibraryName);
				}
				return _libraryAvailable.Value;
			}
		}

		#endregion

		/// <summary>Creates an initially hidden window, using signed physical desktop pixel coordinates.</summary>
		/// <remarks>
		/// Check <see cref="IsSupported"/> first: the constructor only verifies that the library loads, not that Unity's
		/// graphics backend can present. Then assign a texture, call <see cref="Configure"/> to show the window and register
		/// it with <see cref="NativeWindowHost"/>.
		/// </remarks>
		/// <param name="x">Left edge in physical desktop pixels; negative left of the primary monitor.</param>
		/// <param name="y">Top edge in physical desktop pixels; negative above the primary monitor.</param>
		/// <param name="width">Width in physical pixels.</param>
		/// <param name="height">Height in physical pixels.</param>
		/// <exception cref="PlatformNotSupportedException">The native library is missing or the player is not 64-bit.</exception>
		/// <exception cref="InvalidOperationException">The native window could not be created; see <see cref="LastError"/>.</exception>
		public NativeWindowSurface(int x, int y, int width, int height)
		{
			if (IntPtr.Size != 8) {
				throw new PlatformNotSupportedException("Native windows require a 64-bit player.");
			}
			if (!LibraryAvailable) {
				throw new PlatformNotSupportedException($"The native library {LibraryName} is not available on this platform.");
			}
			Id = Native.CreateWindow(x, y, width, height);
			if (Id == 0) {
				throw Failure("Create native window");
			}
		}

		/// <summary>Sets the physical desktop rectangle, visibility and topmost state. Identical calls are cached and skipped.</summary>
		/// <remarks>
		/// The cache makes calling it every frame cheap. It compares with the last values passed here (and the topmost state
		/// set by <see cref="SetTopmost"/>), not with the window's current geometry, so repeating the previous call does not
		/// undo a move the user made in placement mode.
		/// </remarks>
		/// <param name="x">Left edge in physical desktop pixels; negative left of the primary monitor.</param>
		/// <param name="y">Top edge in physical desktop pixels; negative above the primary monitor.</param>
		/// <param name="width">Width in physical pixels.</param>
		/// <param name="height">Height in physical pixels.</param>
		/// <param name="visible">True shows the window, false hides it.</param>
		/// <param name="topmost">True keeps the window above normal windows.</param>
		/// <exception cref="ObjectDisposedException">The surface was disposed.</exception>
		/// <exception cref="InvalidOperationException">The native call failed; the message includes <see cref="LastError"/>.</exception>
		public void Configure(int x, int y, int width, int height, bool visible, bool topmost)
		{
			RequireAlive();
			if (_configured && x == _x && y == _y && width == _width && height == _height && visible == _visible && topmost == _topmost) {
				return;
			}
			if (Native.ConfigureWindow(Id, x, y, width, height, visible ? 1 : 0, topmost ? 1 : 0) == 0) {
				throw Failure("Configure native window");
			}
			_configured = true;
			_x = x;
			_y = y;
			_width = width;
			_height = height;
			_visible = visible;
			_topmost = topmost;
		}

		/// <summary>
		/// Assigns the source texture, typically <c>RenderTexture.GetNativeTexturePtr()</c>, containing premultiplied RGBA
		/// in the desired SDR encoding.
		/// </summary>
		/// <remarks>
		/// IntPtr.Zero detaches the source. Resubmit the pointer whenever Unity recreates or resizes the texture. The pointer
		/// is an <c>ID3D11Texture2D</c>, <c>ID3D12Resource</c>, <c>MTLTexture</c> or <c>VkImage*</c> depending on the
		/// backend; the texture must be created and stay alive until it is detached or the surface is disposed. It is scaled
		/// to the window size on the GPU.
		/// </remarks>
		/// <param name="texture">The native texture pointer, or <see cref="IntPtr.Zero"/> to show nothing.</param>
		/// <param name="flipY">True flips the image vertically when it is presented. VPE's own callers pass false.</param>
		/// <exception cref="ObjectDisposedException">The surface was disposed.</exception>
		/// <exception cref="InvalidOperationException">The native call failed; the message includes <see cref="LastError"/>.</exception>
		public void SetTexture(IntPtr texture, bool flipY = false)
		{
			RequireAlive();
			if (Native.SetTexture(Id, texture, flipY ? 1 : 0) == 0) {
				throw Failure("Assign native window texture");
			}
		}

		/// <summary>Allows mouse dragging and edge resizing while the user places the window. Playback windows stay fixed.</summary>
		/// <remarks>
		/// Read the result from <see cref="Diagnostics"/>. Only functional when <see cref="Capabilities"/> includes
		/// <see cref="NativeWindowCapabilities.Placement"/>; on Linux the drags are processed by <see cref="Pump"/>.
		/// </remarks>
		/// <param name="enabled">True lets the user move and resize the window; false fixes it again.</param>
		/// <exception cref="ObjectDisposedException">The surface was disposed.</exception>
		/// <exception cref="InvalidOperationException">The native call failed; the message includes <see cref="LastError"/>.</exception>
		public void SetPlacementMode(bool enabled)
		{
			RequireAlive();
			if (Native.SetPlacementMode(Id, enabled ? 1 : 0) == 0) {
				throw Failure("Set native placement mode");
			}
		}

		/// <summary>Moves the window behind other windows without moving, resizing or activating it.</summary>
		/// <exception cref="ObjectDisposedException">The surface was disposed.</exception>
		/// <exception cref="InvalidOperationException">The native call failed; the message includes <see cref="LastError"/>.</exception>
		public void SendToBack() => SetZOrder(0);

		/// <summary>Moves the window in front of other windows without moving, resizing or activating it.</summary>
		/// <exception cref="ObjectDisposedException">The surface was disposed.</exception>
		/// <exception cref="InvalidOperationException">The native call failed; the message includes <see cref="LastError"/>.</exception>
		public void BringToFront() => SetZOrder(1);

		/// <summary>Sets or clears the topmost state without moving, resizing or activating the window.</summary>
		/// <remarks>The new state also updates the cache of <see cref="Configure"/>.</remarks>
		/// <param name="topmost">True keeps the window above normal windows; false makes it a normal window again.</param>
		/// <exception cref="ObjectDisposedException">The surface was disposed.</exception>
		/// <exception cref="InvalidOperationException">The native call failed; the message includes <see cref="LastError"/>.</exception>
		public void SetTopmost(bool topmost) => SetZOrder(topmost ? 2 : 3);

		/// <summary>Actual window geometry, DPI, source and presentation counters. Query for troubleshooting, not every frame.</summary>
		/// <remarks>
		/// Each read crosses into the window system. The settings windows of PinUP and the B2S plugin also poll it to pick up
		/// the rectangle after the user moved or resized the window in placement mode.
		/// </remarks>
		/// <exception cref="ObjectDisposedException">The surface was disposed.</exception>
		/// <exception cref="InvalidOperationException">The native call failed; the message includes <see cref="LastError"/>.</exception>
		public NativeWindowDiagnostics Diagnostics
		{
			get {
				RequireAlive();
				var result = new NativeWindowDiagnostics { StructBytes = (uint)Marshal.SizeOf<NativeWindowDiagnostics>() };
				if (Native.GetDiagnostics(Id, ref result) == 0) {
					throw Failure("Read native window diagnostics");
				}
				return result;
			}
		}

		/// <summary>Destroys the window. GPU resources are released by later render events; see <see cref="PendingResourceCount"/>.</summary>
		/// <remarks>
		/// Dispose the <see cref="NativeWindowHost"/> lease first and release the source texture afterwards. Calling it again
		/// does nothing. <see cref="Id"/> is zero afterwards even when the native call fails.
		/// </remarks>
		/// <exception cref="InvalidOperationException">The native window could not be destroyed; see <see cref="LastError"/>.</exception>
		public void Dispose()
		{
			if (Id == 0) {
				return;
			}
			var id = Id;
			Id = 0;
			if (Native.DestroyWindow(id) == 0) {
				throw Failure("Destroy native window");
			}
		}

		private void SetZOrder(int order)
		{
			RequireAlive();
			if (Native.SetZOrder(Id, order) == 0) {
				throw Failure("Set native window z-order");
			}
			if (order >= 2) {
				_topmost = order == 2;
			}
		}

		private void RequireAlive()
		{
			if (Id == 0) {
				throw new ObjectDisposedException(nameof(NativeWindowSurface));
			}
		}

		private static InvalidOperationException Failure(string operation) => new InvalidOperationException(operation + ": " + LastError);

		private static class Native
		{
			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_CreateWindow")]
			internal static extern int CreateWindow(int x, int y, int width, int height);

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_ConfigureWindow")]
			internal static extern int ConfigureWindow(int id, int x, int y, int width, int height, int visible, int topmost);

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_SetZOrder")]
			internal static extern int SetZOrder(int id, int order);

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_SetPlacementMode")]
			internal static extern int SetPlacementMode(int id, int enabled);

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_SetTexture")]
			internal static extern int SetTexture(int id, IntPtr texture, int flipY);

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_DestroyWindow")]
			internal static extern int DestroyWindow(int id);

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_DestroyAll")]
			internal static extern void DestroyAll();

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_Pump")]
			internal static extern void Pump();

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_GetRenderEventFunc")]
			internal static extern IntPtr GetRenderEventFunc();

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_GetGraphicsRenderer")]
			internal static extern int GetGraphicsRenderer();

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_GetDrawEventId")]
			internal static extern int GetDrawEventId();

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_GetPresentEventId")]
			internal static extern int GetPresentEventId();

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_GetPendingResourceCount")]
			internal static extern int GetPendingResourceCount();

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_GetDiagnostics")]
			internal static extern int GetDiagnostics(int id, ref NativeWindowDiagnostics diagnostics);

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_GetLastError")]
			internal static extern int GetLastError([Out] byte[] destination, int capacity);

			[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VPNW_GetCapabilities")]
			internal static extern uint GetCapabilities();
		}
	}
}
