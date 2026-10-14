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
	/// Exclusive leases on physical output destinations, so two plugins never open the same device, such as a real DMD on a
	/// USB or serial port.
	/// </summary>
	/// <remarks>
	/// The Player publishes one instance as the <c>vpe.outputs.v1</c> capability (<c>UnityPluginCapability.Outputs</c>). A
	/// plugin acquires a lease for each device before opening it and keeps the lease until the device is closed. Leases
	/// compare destination IDs as plain strings, so agree on stable IDs: the DMD plugin uses <c>usb:zedmd:auto</c>,
	/// <c>usb:pin2dmd:auto</c> or <c>serial:COM3</c> (port upper-cased). Thread-safe.
	/// <example>
	/// The DMD plugin leases its configured devices while preparing a game and releases them once its native worker stopped:
	/// <code>
	/// var outputs = game.Capabilities.Require&lt;PluginOutputLeases&gt;(UnityPluginCapability.Outputs);
	/// foreach (var output in OutputIds(settings)) _nativeResources.Own(outputs.Acquire(output, _application.Plugin.Id));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginOutputLeases
	{
		private readonly Dictionary<string, string> _owners = new Dictionary<string, string>(StringComparer.Ordinal);
		/// <summary>
		/// Takes exclusive ownership of a destination until the returned lease is disposed. Fails instead of waiting when
		/// another plugin holds it; a failed acquisition in a prepare hook faults the plugin with the message naming the owner.
		/// </summary>
		/// <param name="destination">Stable device ID, compared ordinally.</param>
		/// <param name="pluginId">ID of the acquiring plugin, reported by <see cref="Owner"/> and in conflict messages.</param>
		/// <returns>The lease. Dispose it after the device is closed, from any thread; disposing twice does nothing.</returns>
		/// <exception cref="ArgumentException"><paramref name="destination"/> is null, empty or whitespace.</exception>
		/// <exception cref="InvalidOperationException">The destination is already leased.</exception>
		public IDisposable Acquire(string destination, string pluginId)
		{
			if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("A destination needs a stable ID.", nameof(destination));
			lock (_owners) {
				if (_owners.TryGetValue(destination, out var owner)) throw new InvalidOperationException(destination + " is already owned by " + owner + ".");
				_owners.Add(destination, pluginId);
			}
			return new Subscription(() => { lock (_owners) _owners.Remove(destination); });
		}
		/// <summary>Returns the ID of the plugin holding a destination, or null when it is free.</summary>
		/// <param name="destination">Stable device ID, compared ordinally.</param>
		public string? Owner(string destination) { lock (_owners) return _owners.TryGetValue(destination, out var owner) ? owner : null; }
	}
}
