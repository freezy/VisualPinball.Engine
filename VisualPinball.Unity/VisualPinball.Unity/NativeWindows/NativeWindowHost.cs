// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Submits one pair of native rendering events per frame after all rendering, for every registered
	/// <see cref="NativeWindowSurface"/>, and keeps doing so until deferred GPU releases have drained.
	/// </summary>
	/// <remarks>
	/// A single hidden instance is shared by all users (PinUP, B2S, ...). It is created on the first
	/// <see cref="Register"/> and removes itself once no surface is registered and no GPU resource is pending.
	/// <para>
	/// Native windows draw and present on Unity's render thread, triggered by <see cref="NativeWindowSurface.DrawEventId"/>
	/// and <see cref="NativeWindowSurface.PresentEventId"/>. Both events operate on all windows, so they must be issued once
	/// per frame in total, not once per window; this component does that from <c>LateUpdate</c> (execution order 10000)
	/// and also calls <see cref="NativeWindowSurface.Pump"/>. A plugin only registers its surface and keeps the returned
	/// lease. In play mode the presenter survives scene loads; it runs <c>LateUpdate</c> only while the application is
	/// playing.
	/// </para>
	/// <para>
	/// When the presenter is destroyed (for example when play mode ends), it disposes every surface still registered.
	/// </para>
	/// <example>
	/// <code>
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
	[DefaultExecutionOrder(10000)]
	public sealed class NativeWindowHost : MonoBehaviour
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static NativeWindowHost _instance;
		private readonly HashSet<NativeWindowSurface> _surfaces = new HashSet<NativeWindowSurface>();

		/// <summary>
		/// Registers a surface for render-thread presentation. Dispose the returned lease before disposing the surface.
		/// </summary>
		/// <remarks>
		/// Call on the main thread; the first call creates the hidden presenter object. Registering the same surface twice
		/// has no further effect, and one lease removes it. Disposing the lease more than once is harmless. After the last
		/// lease ends the presenter keeps issuing events until <see cref="NativeWindowSurface.PendingResourceCount"/> is zero,
		/// then removes itself.
		/// </remarks>
		/// <param name="surface">The window to present. The caller keeps ownership and disposes it after the lease.</param>
		/// <returns>A lease that unregisters the surface when disposed.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="surface"/> is null.</exception>
		public static IDisposable Register(NativeWindowSurface surface)
		{
			if (surface == null) {
				throw new ArgumentNullException(nameof(surface));
			}
			if (_instance == null) {
				var owner = new GameObject("Native Window Presenter") { hideFlags = HideFlags.HideAndDontSave };
				if (Application.isPlaying) {
					DontDestroyOnLoad(owner);
				}
				_instance = owner.AddComponent<NativeWindowHost>();
			}
			_instance._surfaces.Add(surface);
			return new PresentationLease(surface);
		}

		private static void Unregister(NativeWindowSurface surface)
		{
			if (_instance != null) {
				_instance._surfaces.Remove(surface);
			}
		}

		private void LateUpdate()
		{
			NativeWindowSurface.Pump();
			if (_surfaces.Count == 0 && NativeWindowSurface.PendingResourceCount == 0) {
				// Detach first, so a Register() before the deferred destruction creates a fresh presenter.
				if (_instance == this) {
					_instance = null;
				}
				Destroy(gameObject);
				return;
			}
			var callback = NativeWindowSurface.RenderEventFunction;
			GL.IssuePluginEvent(callback, NativeWindowSurface.DrawEventId);
			GL.IssuePluginEvent(callback, NativeWindowSurface.PresentEventId);
		}

		private void OnDestroy()
		{
			foreach (var surface in _surfaces) {
				try {
					surface.Dispose();
				} catch (Exception e) {
					Logger.Error(e, "Could not destroy native window.");
				}
			}
			_surfaces.Clear();
			if (_instance == this) {
				_instance = null;
			}
		}

		private sealed class PresentationLease : IDisposable
		{
			private NativeWindowSurface _surface;

			internal PresentationLease(NativeWindowSurface surface)
			{
				_surface = surface;
			}

			public void Dispose()
			{
				if (_surface != null) {
					Unregister(_surface);
				}
				_surface = null;
			}
		}
	}
}
