// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Owns a set of disposables and releases them together, in reverse order of acquisition, continuing past failures.
	/// </summary>
	/// <remarks>
	/// The host gives each plugin instance two scopes: <see cref="PluginGameContext.Lifetime"/>, released right after
	/// <see cref="IPlayerPlugin.StopGameAsync"/>, and <see cref="PluginApplicationContext.Lifetime"/>, released after
	/// <see cref="IPlayerPlugin.ShutdownAsync"/>. A plugin that stops responding keeps both until its pending work finishes.
	/// Handing subscriptions, bindings, publications and GameObjects to a scope instead of tracking them by hand means a
	/// failed or abandoned plugin still cleans up. Plugins and the Player also create
	/// their own scopes for narrower lifetimes, such as PinUP's scope per player screen or the DMD plugin's scope for native
	/// device leases. Not thread-safe: use one scope from one thread.
	/// <example>
	/// <code>
	/// var dmd = game.Lifetime.Own(game.Resources.Bind&lt;IPluginDisplaySource&gt;(selection));
	/// game.Lifetime.Own(dmd.SubscribeFrames(frame =&gt; Render(frame)));
	/// game.Lifetime.Own(new Subscription(() =&gt; { if (_root != null) UnityEngine.Object.Destroy(_root); }));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class ResourceScope : IDisposable
	{
		private readonly List<IDisposable> _resources = new List<IDisposable>();
		private bool _disposed;
		/// <summary>
		/// Adds a resource to release when the scope is disposed, and returns it so the call can wrap a constructor or
		/// <c>Publish</c> call inline. If the scope is already disposed, the resource is disposed at once and the call throws,
		/// so late work cannot leak.
		/// </summary>
		/// <typeparam name="T">The resource type, kept so the result needs no cast.</typeparam>
		/// <param name="resource">The resource to own.</param>
		/// <returns><paramref name="resource"/>.</returns>
		/// <exception cref="ObjectDisposedException">The scope has already been disposed.</exception>
		public T Own<T>(T resource) where T : IDisposable
		{
			if (_disposed) { resource.Dispose(); throw new ObjectDisposedException(nameof(ResourceScope)); }
			_resources.Add(resource);
			return resource;
		}
		/// <summary>
		/// Disposes every owned resource, last owned first. Every resource is disposed even when one throws; afterwards the
		/// collected failures are rethrown together. Later calls do nothing.
		/// </summary>
		/// <exception cref="AggregateException">One or more resources threw while being disposed.</exception>
		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			List<Exception>? errors = null;
			for (int i = _resources.Count - 1; i >= 0; i--)
				try { _resources[i].Dispose(); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
			_resources.Clear();
			if (errors != null) throw new AggregateException("Some plugin resources could not be released.", errors);
		}
	}

	/// <summary>
	/// An <see cref="IDisposable"/> that runs a release action once. Returned by most plugin APIs as the handle that undoes a
	/// subscription, publication or lease, and handy for turning any cleanup into something a <see cref="ResourceScope"/> can own.
	/// </summary>
	/// <remarks>
	/// <example>
	/// PinUP unhooks its screen events when the game ends:
	/// <code>
	/// game.Lifetime.Own(new Subscription(() =&gt; { _player.ScreenCreated -= ScreenCreated; _player.ScreenDestroyed -= ScreenDestroyed; }));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class Subscription : IDisposable
	{
		private Action? _release;
		/// <summary>Wraps a release action.</summary>
		/// <param name="release">Runs on the first <see cref="Dispose"/>, on the disposing thread.</param>
		public Subscription(Action release) { _release = release; }
		/// <summary>
		/// Runs the release action on the first call; later calls, including concurrent ones, do nothing. An exception from the
		/// action propagates to the caller and the action is not retried.
		/// </summary>
		public void Dispose() => System.Threading.Interlocked.Exchange(ref _release, null)?.Invoke();
	}
}
