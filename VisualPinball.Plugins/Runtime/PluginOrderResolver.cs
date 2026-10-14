// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Thrown by <see cref="PluginOrderResolver.Resolve"/> when a set of plugins cannot be ordered: a dependency is missing, a
	/// required capability has no provider or more than one, or the ordering constraints form a cycle.
	/// </summary>
	/// <remarks>
	/// The host catches it while selecting plugins for a game and marks every plugin in <see cref="Plugins"/> as
	/// <see cref="PluginState.Blocked"/> with the exception's message, then resolves the rest again. Plugin authors see the
	/// message in the Player's plugin status, such as <c>my.plugin: vpe.example.v1 has multiple providers; select one.</c>
	/// </remarks>
	public sealed class PluginOrderException : InvalidOperationException
	{
		/// <summary>
		/// IDs of the plugins at fault: the consumer whose dependency or capability is unsatisfied, or every plugin in the
		/// cycle, in cycle order.
		/// </summary>
		public IReadOnlyList<string> Plugins { get; }
		/// <summary>Creates the exception with a user-facing message and the plugins to block.</summary>
		/// <param name="message">Explanation shown in the plugin's status.</param>
		/// <param name="plugins">IDs of the plugins at fault.</param>
		public PluginOrderException(string message, params string[] plugins) : base(message) { Plugins = plugins; }
	}

	/// <summary>
	/// Computes the order in which plugins run one lifecycle phase: dependencies and capability providers run before the
	/// plugins that need them, explicit before/after constraints are honored, and priority then plugin ID break ties.
	/// </summary>
	/// <remarks>
	/// The host resolves an order for every phase from the descriptors' declarations, so plugin authors influence ordering
	/// only through <see cref="PluginDescriptor"/>: <see cref="PluginDescriptor.DependsOn"/>,
	/// <see cref="PluginDescriptor.Requires"/>/<see cref="PluginDescriptor.Provides"/>, <see cref="PluginDescriptor.Before"/>,
	/// <see cref="PluginDescriptor.After"/>, <see cref="PluginDescriptor.Order"/> and per-phase
	/// <see cref="PluginDescriptor.PhaseOrder"/>. Stopping runs in reverse start order.
	/// <example>
	/// A plugin that runs after the DMD plugin in every phase, and early in the render phase:
	/// <code>
	/// new PluginDescriptor("my.overlay", "Overlay", "Draws on top of the DMD") {
	///     After = new[] { "org.visualpinball.dmd" },
	///     PhaseOrder = new Dictionary&lt;PluginPhase, PluginOrder&gt; { [PluginPhase.Render] = new PluginOrder { Priority = -10 } }
	/// };
	/// </code>
	/// </example>
	/// </remarks>
	public static class PluginOrderResolver
	{
		/// <summary>
		/// Orders <paramref name="plugins"/> for one <paramref name="phase"/> and returns them in execution order.
		/// </summary>
		/// <remarks>
		/// Rules, applied together:
		/// <list type="bullet">
		/// <item>Every <see cref="PluginDescriptor.DependsOn"/> entry must be in the set and runs first.</item>
		/// <item>
		/// Each <see cref="PluginDescriptor.Requires"/> entry not offered by the host and not provided by the plugin itself needs
		/// exactly one other plugin in the set that provides it, which then runs first. <paramref name="preferredProviders"/>
		/// narrows several providers down to the selected one.
		/// </item>
		/// <item>
		/// <see cref="PluginDescriptor.Before"/> and <see cref="PluginDescriptor.After"/>, plus the phase's
		/// <see cref="PluginOrder.Before"/> and <see cref="PluginOrder.After"/>, add constraints; IDs not in the set are ignored.
		/// </item>
		/// <item>
		/// Among plugins free to run, the lowest <see cref="PluginOrder.Priority"/> of the phase goes first, or
		/// <see cref="PluginDescriptor.Order"/> when the plugin has no entry for the phase; equal values go by ID (ordinal).
		/// </item>
		/// </list>
		/// </remarks>
		/// <param name="plugins">The enabled plugins to order. IDs must be unique.</param>
		/// <param name="phase">The phase whose <see cref="PluginDescriptor.PhaseOrder"/> entry applies.</param>
		/// <param name="hostCapabilities">
		/// Capability IDs the application already offers (see <see cref="PluginCapabilities.Ids"/>); null for none.
		/// </param>
		/// <param name="preferredProviders">
		/// Capability ID to the ID of the plugin chosen to provide it, when several could; null for none.
		/// </param>
		/// <returns>The same descriptors, in the order the phase should run them.</returns>
		/// <exception cref="PluginOrderException">
		/// A dependency or required capability is unsatisfied or ambiguous, or the constraints form a cycle.
		/// </exception>
		/// <exception cref="ArgumentException">Two descriptors share an ID.</exception>
		public static IReadOnlyList<PluginDescriptor> Resolve(IEnumerable<PluginDescriptor> plugins, PluginPhase phase,
			IEnumerable<string>? hostCapabilities = null, IReadOnlyDictionary<string, string>? preferredProviders = null)
		{
			var nodes = plugins.ToDictionary(plugin => plugin.Id, StringComparer.Ordinal);
			var host = new HashSet<string>(hostCapabilities ?? Array.Empty<string>(), StringComparer.Ordinal);
			var incoming = nodes.Keys.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal));
			foreach (var plugin in nodes.Values) {
				foreach (string dependency in plugin.DependsOn) {
					if (!nodes.ContainsKey(dependency)) throw new PluginOrderException(plugin.Id + " requires disabled or absent plugin " + dependency + ".", plugin.Id);
					incoming[plugin.Id].Add(dependency);
				}
				foreach (string capability in plugin.Requires) {
					if (host.Contains(capability) || plugin.Provides.Contains(capability)) continue;
					var candidates = nodes.Values.Where(provider => provider.Id != plugin.Id && provider.Provides.Contains(capability)).ToList();
					if (preferredProviders != null && preferredProviders.TryGetValue(capability, out var selected))
						candidates.RemoveAll(candidate => candidate.Id != selected);
					if (candidates.Count != 1) throw new PluginOrderException(plugin.Id + ": " + capability + (candidates.Count == 0 ? " is unavailable." : " has multiple providers; select one."), plugin.Id);
					incoming[plugin.Id].Add(candidates[0].Id);
				}
				plugin.PhaseOrder.TryGetValue(phase, out var order);
				foreach (string before in plugin.Before.Concat(order?.Before ?? Array.Empty<string>()))
					if (nodes.ContainsKey(before)) incoming[before].Add(plugin.Id);
				foreach (string after in plugin.After.Concat(order?.After ?? Array.Empty<string>()))
					if (nodes.ContainsKey(after)) incoming[plugin.Id].Add(after);
			}
			var resolved = new List<PluginDescriptor>(nodes.Count);
			while (nodes.Count > 0) {
				var next = nodes.Values.Where(plugin => incoming[plugin.Id].Count == 0)
					.OrderBy(plugin => plugin.PhaseOrder.TryGetValue(phase, out var order) ? order.Priority : plugin.Order)
					.ThenBy(plugin => plugin.Id, StringComparer.Ordinal).FirstOrDefault();
				if (next == null) {
					var cycle = Cycle(incoming, nodes.Keys.First());
					throw new PluginOrderException("Plugin ordering cycle: " + string.Join(" -> ", cycle.Concat(new[] { cycle[0] })) + ".", cycle);
				}
				resolved.Add(next);
				nodes.Remove(next.Id);
				foreach (var dependencies in incoming.Values) dependencies.Remove(next.Id);
			}
			return resolved;
		}

		private static string[] Cycle(Dictionary<string, HashSet<string>> incoming, string first)
		{
			var path = new List<string>();
			string current = first;
			while (!path.Contains(current)) { path.Add(current); current = incoming[current].First(); }
			return path.Skip(path.IndexOf(current)).ToArray();
		}
	}
}
