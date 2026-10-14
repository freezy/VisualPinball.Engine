// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Tells integration packages whether a player application manages them through the plugin host. Player applications
	/// claim integration ownership before automatic native bridges can start.
	/// </summary>
	/// <remarks>
	/// Some integrations start themselves when VPE runs a table in the editor or in a standalone build. The DMD package,
	/// for example, adds a bridge to every table <see cref="Player"/> it finds so DMD frames reach physical displays. In
	/// the VPE Player the same work is done by plugins under the user's settings, so the Player calls <see cref="Claim"/>
	/// and the automatic bridges stand down by checking <see cref="HostManaged"/>.
	/// <para>
	/// Unity's load order makes this work: the state is reset at <c>SubsystemRegistration</c>, the Player claims at
	/// <c>AfterAssembliesLoaded</c>, and bridges start at <c>BeforeSceneLoad</c>.
	/// </para>
	/// <example>
	/// An integration that starts itself checks the mode first:
	/// <code>
	/// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	/// private static void StartWatcher()
	/// {
	///     if (PlayerIntegrationMode.HostManaged) return; // the Player's plugin does this job
	///     var gameObject = new GameObject("My bridge");
	///     Object.DontDestroyOnLoad(gameObject);
	///     gameObject.AddComponent&lt;MyBridgeWatcher&gt;();
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public static class PlayerIntegrationMode
	{
		/// <summary>
		/// True when a player application has called <see cref="Claim"/> in this run. Integrations must then not start
		/// their own bridges. False by default, in the editor and in builds without a plugin host.
		/// </summary>
		public static bool HostManaged { get; private set; }
		/// <summary>
		/// Marks integrations as managed by the host for the rest of the run. Call it on the main thread from a
		/// <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]</c> method, so it runs after
		/// the reset and before integrations start. Only player applications call this; plugins have no reason to.
		/// </summary>
		/// <remarks>
		/// <example>
		/// <code>
		/// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		/// private static void ClaimIntegrations() =&gt; PlayerIntegrationMode.Claim();
		/// </code>
		/// </example>
		/// </remarks>
		public static void Claim() => HostManaged = true;
		/// <summary>
		/// Clears the claim and the plugin and identity-reader registries at the start of every run, including each play mode
		/// entry in the editor with domain reload disabled, so state from a previous play session does not linger.
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Reset()
		{
			HostManaged = false;
			PlayerPluginRegistry.Reset();
			PackagedGameIdentityReaders.Reset();
		}
	}
}
