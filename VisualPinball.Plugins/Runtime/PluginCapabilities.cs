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
	/// Directory of shared services, each published under a versioned string ID, through which the Player and plugins hand
	/// each other objects. Concrete display, audio and controller streams go through <see cref="PluginResourceRegistry"/>
	/// instead.
	/// </summary>
	/// <remarks>
	/// A capability ID names a contract and its version, such as <c>vpe.unity.game-scene.v1</c>, so a provider can publish a
	/// new version beside an old one. The Player publishes <c>vpe.events.v1</c> (<see cref="PluginEvents"/>) and
	/// <c>vpe.outputs.v1</c> (<see cref="PluginOutputLeases"/>) for its lifetime, and the Unity game scene for each game
	/// before plugins are prepared. Plugins declare what they consume in <see cref="PluginDescriptor.Requires"/> and what they
	/// offer in <see cref="PluginDescriptor.Provides"/>; the host uses those lists and the IDs published here to order and
	/// block plugins.
	/// <para>Not thread-safe: publish and look up on the owner thread.</para>
	/// <example>
	/// The DMD plugin takes the output leases while preparing a game:
	/// <code>
	/// var scene = game.Capabilities.Require&lt;IUnityGameScene&gt;(UnityPluginCapability.GameScene);
	/// var outputs = game.Capabilities.Require&lt;PluginOutputLeases&gt;(UnityPluginCapability.Outputs);
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginCapabilities
	{
		private readonly Dictionary<string, object> _services = new Dictionary<string, object>(StringComparer.Ordinal);
		/// <summary>
		/// IDs of the currently published capabilities. The host reads them when a game starts to tell which
		/// <see cref="PluginDescriptor.Requires"/> entries the application already satisfies.
		/// </summary>
		public IEnumerable<string> Ids => _services.Keys;
		/// <summary>
		/// Publishes a service under an ID until the returned handle is disposed. Disposing removes the service only if it is
		/// still the one registered under that ID; disposing twice does nothing.
		/// </summary>
		/// <typeparam name="T">The contract type consumers look the service up by.</typeparam>
		/// <param name="id">Versioned capability ID, compared ordinally.</param>
		/// <param name="service">The service instance shared with every consumer.</param>
		/// <returns>A handle that withdraws the service; put it in a <see cref="ResourceScope"/> tied to the service's lifetime.</returns>
		/// <exception cref="InvalidOperationException">A service is already published under <paramref name="id"/>.</exception>
		/// <remarks>
		/// <example>
		/// The Player publishes the game scene for one game:
		/// <code>
		/// _gameScope.Own(Host.Capabilities.Publish&lt;IUnityGameScene&gt;(UnityPluginCapability.GameScene, new UnityGameScene(table, engine)));
		/// </code>
		/// </example>
		/// </remarks>
		public IDisposable Publish<T>(string id, T service) where T : class
		{
			if (_services.ContainsKey(id)) throw new InvalidOperationException("Capability already provided: " + id);
			_services.Add(id, service);
			return new Subscription(() => { if (_services.TryGetValue(id, out var current) && ReferenceEquals(current, service)) _services.Remove(id); });
		}
		/// <summary>
		/// Returns the service published under <paramref name="id"/>, or null when nothing is published there or the service
		/// does not implement <typeparamref name="T"/>. Use it for optional capabilities.
		/// </summary>
		/// <typeparam name="T">The contract type to cast the service to.</typeparam>
		/// <param name="id">Versioned capability ID.</param>
		public T? Find<T>(string id) where T : class => _services.TryGetValue(id, out var value) ? value as T : null;
		/// <summary>
		/// Returns the service published under <paramref name="id"/>, for capabilities a plugin cannot work without. List the
		/// ID in <see cref="PluginDescriptor.Requires"/> as well, so the host blocks the plugin up front when nothing provides it.
		/// </summary>
		/// <typeparam name="T">The contract type to cast the service to.</typeparam>
		/// <param name="id">Versioned capability ID.</param>
		/// <exception cref="InvalidOperationException">
		/// Nothing is published under <paramref name="id"/>, or the service does not implement <typeparamref name="T"/>.
		/// </exception>
		public T Require<T>(string id) where T : class => Find<T>(id) ?? throw new InvalidOperationException("Required capability is unavailable: " + id);
	}
}
