// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Plugins
{
	public sealed class PluginOrderException : InvalidOperationException
	{
		public IReadOnlyList<string> Plugins { get; }
		public PluginOrderException(string message, params string[] plugins) : base(message) { Plugins = plugins; }
	}

	/// <summary>Dependencies and selected capability providers precede consumers; priority breaks ties.</summary>
	public static class PluginOrderResolver
	{
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
