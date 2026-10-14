// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A movable desktop window that a plugin opens for the Player's screen layout editor. Native output transport only:
	/// the Player owns placement controls and test-pattern rendering.
	/// </summary>
	/// <remarks>
	/// When the user selects a screen in a <see cref="SettingKind.ScreenLayout"/> field, the Player asks the plugin's
	/// <see cref="IUnitySettingsWindowProvider"/> for a window at the stored rectangle, draws a test pattern into a texture
	/// and hands it over with <see cref="SetTexture"/>. While the window is open the Player polls <see cref="Bounds"/>
	/// (about every 80 ms) to pick up the user's drags and resizes. PinUP and the B2S backglass implement it with a
	/// <see cref="NativeWindowSurface"/> in placement mode, so the test window is the same kind of window that shows the
	/// game later. All members are called on the main thread while the Player is running.
	/// <example>
	/// <code>
	/// internal sealed class MySettingsWindow : IUnitySettingsWindow
	/// {
	///     private readonly NativeWindowSurface _window;
	///     private readonly IDisposable _presentation;
	///     internal MySettingsWindow(RectInt bounds)
	///     {
	///         _window = new NativeWindowSurface(bounds.x, bounds.y, bounds.width, bounds.height);
	///         _presentation = NativeWindowHost.Register(_window);
	///         _window.SetPlacementMode(true);
	///         SetBounds(bounds);
	///     }
	///     public RectInt Bounds {
	///         get { var state = _window.Diagnostics; return new RectInt(state.Left, state.Top, state.WindowWidth, state.WindowHeight); }
	///     }
	///     public void SetBounds(RectInt bounds) =&gt; _window.Configure(bounds.x, bounds.y, bounds.width, bounds.height, true, true);
	///     public void SetTexture(Texture texture) =&gt; _window.SetTexture(texture != null ? texture.GetNativeTexturePtr() : IntPtr.Zero);
	///     public void Activate() =&gt; _window.BringToFront();
	///     public void Dispose() { _presentation.Dispose(); _window.Dispose(); }
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IUnitySettingsWindow : IDisposable
	{
		/// <summary>
		/// The window's actual rectangle in signed physical desktop pixels, read back from the window system, so it includes
		/// moves and resizes the user made. Origins left of or above the primary monitor are negative.
		/// </summary>
		RectInt Bounds { get; }
		/// <summary>
		/// Moves and resizes the window to a rectangle in signed physical desktop pixels and shows it. The Player clamps the
		/// size to at least 64 x 32 pixels before calling it.
		/// </summary>
		/// <param name="bounds">The new window rectangle.</param>
		void SetBounds(RectInt bounds);
		/// <summary>
		/// Sets the texture the window shows, or null to show nothing. The texture belongs to the caller and must stay alive
		/// until it is replaced or the window is disposed. The Player passes null and then a new texture whenever the window
		/// size changes.
		/// </summary>
		/// <param name="texture">A created texture with premultiplied RGBA content, or null to detach.</param>
		void SetTexture(Texture texture);
		/// <summary>Brings the window in front of other windows, for example when the user selects its screen again.</summary>
		void Activate();
	}
	/// <summary>
	/// Implemented by a plugin factory whose plugin shows content on its own desktop windows, so the Player's screen layout
	/// editor can open test windows for it.
	/// </summary>
	/// <remarks>
	/// The Player checks the <see cref="IPlayerPluginFactory"/> of the plugin whose settings are shown for this interface.
	/// PinUP and the B2S backglass implement it and report <see cref="NativeWindowSurface.IsSupported"/> as
	/// <see cref="Supported"/>.
	/// <example>
	/// <code>
	/// internal sealed class MyPluginFactory : IPlayerPluginFactory, IUnitySettingsWindowProvider
	/// {
	///     public bool Supported =&gt; NativeWindowSurface.IsSupported;
	///     public IUnitySettingsWindow OpenWindow(RectInt bounds) =&gt; new MySettingsWindow(bounds);
	///     // IPlayerPluginFactory members omitted.
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IUnitySettingsWindowProvider
	{
		/// <summary>
		/// True when windows can be opened on this platform and graphics backend. The Player does not call
		/// <see cref="OpenWindow"/> when it is false and tells the user that placement is unavailable.
		/// </summary>
		bool Supported { get; }
		/// <summary>
		/// Opens a test window at a rectangle in signed physical desktop pixels; the Player calls
		/// <see cref="IUnitySettingsWindow.SetBounds"/> right after. Called on the main thread while the Player is running. The
		/// caller owns and disposes the returned window. Exceptions are shown to the user.
		/// </summary>
		/// <param name="bounds">The stored placement of the screen being edited.</param>
		/// <returns>The new window.</returns>
		IUnitySettingsWindow OpenWindow(RectInt bounds);
	}
	/// <summary>
	/// One image of a settings preview: a texture and its normalized placement on the preview canvas. No UI objects or
	/// styling cross this boundary.
	/// </summary>
	/// <remarks>
	/// The Player draws every layer of <see cref="IUnitySettingsPreview.Layers"/> on its own canvas after each
	/// <see cref="IUnitySettingsPreview.Update"/>. PinUP uses one layer per PUP screen, positioned relative to the
	/// backglass screen; the B2S backglass uses a single full-canvas layer. The plugin owns the layer objects and updates
	/// their properties on the main thread.
	/// <example>
	/// <code>
	/// var layer = new UnitySettingsPreviewLayer("backglass");
	/// layer.Texture = compositor.SceneTexture;
	/// layer.Rectangle = new Rect(0, 0, 1, 1);
	/// layer.Visible = layer.Texture != null;
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class UnitySettingsPreviewLayer
	{
		/// <summary>
		/// Stable key of the layer. The Player keeps one image per ID and removes it when no layer with that ID is listed
		/// any more.
		/// </summary>
		public string Id { get; }
		/// <summary>The texture to show, or null. The plugin owns it; the Player only displays it.</summary>
		public Texture Texture { get; set; }
		/// <summary>
		/// Placement on the canvas, normalized to its size: x and y are the left and top edges from 0 to 1 (top-left origin),
		/// width and height the fractions of the canvas width and height. Defaults to an empty rectangle.
		/// </summary>
		public Rect Rectangle { get; set; }
		/// <summary>Whether the Player shows the layer. False (the default) hides it without removing it.</summary>
		public bool Visible { get; set; }
		/// <summary>Creates a hidden layer with no texture.</summary>
		/// <param name="id">The layer's stable key; see <see cref="Id"/>.</param>
		public UnitySettingsPreviewLayer(string id) { Id = id; }
	}
	/// <summary>
	/// A live preview that a plugin renders inside its settings page, such as a PUP pack or a backglass playing without a
	/// game.
	/// </summary>
	/// <remarks>
	/// Returned by <see cref="IUnitySettingsPreviewProvider.OpenPreviewAsync"/> and owned by the Player, which calls
	/// <see cref="Update"/> about every 16 ms on the main thread, then scales <see cref="CanvasSize"/> to fit its viewport
	/// and draws <see cref="Layers"/> on it. An exception from <see cref="Update"/> stops the preview and is shown to the
	/// user. The Player disposes the preview when the user stops it, starts another one or leaves the page.
	/// </remarks>
	public interface IUnitySettingsPreview : IDisposable
	{
		/// <summary>
		/// Size of the canvas the layers are placed on, in any unit (typically pixels); only its aspect ratio matters to
		/// the Player. Nothing is drawn while either component is zero or less.
		/// </summary>
		Vector2 CanvasSize { get; }
		/// <summary>
		/// The current layers. The Player reads the list after every <see cref="Update"/>; layers may be added and removed
		/// between calls. Layers are stacked in the order their IDs first appear.
		/// </summary>
		IReadOnlyList<UnitySettingsPreviewLayer> Layers { get; }
		/// <summary>
		/// Advances playback by one Player frame and refreshes <see cref="CanvasSize"/> and <see cref="Layers"/>. Called on
		/// the main thread.
		/// </summary>
		void Update();
	}
	/// <summary>
	/// Implemented by a plugin factory that can show a live preview in a <see cref="SettingKind.Preview"/> settings field.
	/// </summary>
	/// <remarks>
	/// When the user presses Start on a preview field, the Player calls <see cref="OpenPreviewAsync"/> with the field's
	/// <see cref="PluginField.Provider"/> and the page's effective settings. PinUP answers <c>pack-preview</c> by
	/// playing the selected pack offscreen, and the B2S backglass answers <c>backglass-preview</c> by rendering the selected
	/// backglass with a slow lamp test.
	/// <example>
	/// <code>
	/// public Task&lt;IUnitySettingsPreview&gt; OpenPreviewAsync(string provider, PluginChoiceRequest request, CancellationToken cancellation)
	///     =&gt; provider == "backglass-preview"
	///         ? MyPreview.OpenAsync(request, cancellation)
	///         : throw new ArgumentException("Unknown preview provider.", nameof(provider));
	/// </code>
	/// </example>
	/// </remarks>
	public interface IUnitySettingsPreviewProvider
	{
		/// <summary>
		/// Called on Unity's owning thread. Return an owned preview; cancellation must release partial startup. Loading may
		/// continue asynchronously; the Player disposes a preview that completes after it was cancelled or replaced.
		/// </summary>
		/// <param name="provider">The <see cref="PluginField.Provider"/> of the preview field, selecting which preview to open.</param>
		/// <param name="request">
		/// The game, the page's effective settings, the platform, the published resources and the selected package.
		/// </param>
		/// <param name="cancellation">Cancelled when the user stops the preview or leaves the page before it is ready.</param>
		/// <returns>The new preview, owned by the caller.</returns>
		/// <exception cref="ArgumentException">Typically thrown for an unknown <paramref name="provider"/>.</exception>
		/// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled during startup.</exception>
		/// <remarks>Any other exception's message is shown to the user as the preview status.</remarks>
		Task<IUnitySettingsPreview> OpenPreviewAsync(string provider, PluginChoiceRequest request, CancellationToken cancellation);
	}
}
