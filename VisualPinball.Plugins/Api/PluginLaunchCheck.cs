// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VisualPinball.Plugins
{
	/// <summary>Optional factory contract for requirements that must be met before the Player imports a table.</summary>
	/// <remarks>Checks run on the owner thread with committed settings, without constructing plugins or a table scene.
	/// Keep them read-only, honor cancellation, and move filesystem work to a worker. A check that ignores cancellation
	/// prevents further checks of that plugin, for any game, until it finishes. An applicable, enabled plugin with
	/// this contract must also start successfully; optional integrations without this contract retain fault isolation.</remarks>
	public interface IPluginLaunchCheck
	{
		/// <summary>Whether this plugin is required for the selected game. Called before settings or platform validation.</summary>
		bool AppliesTo(GameIdentity game);
		/// <summary>Returns unmet requirements, with setting field IDs where possible; an empty list permits launch.</summary>
		Task<IReadOnlyList<PluginValidationIssue>> CheckLaunchAsync(PluginLaunchContext context, CancellationToken cancellation);
	}

	/// <summary>The selected package and effective committed settings, available before a table scene exists.</summary>
	public sealed class PluginLaunchContext
	{
		public GameIdentity Game { get; }
		public PluginPackageReference Package { get; }
		public PluginSettingsSnapshot Settings { get; }
		public string Platform { get; }
		public PluginLaunchContext(GameIdentity game, PluginPackageReference package, PluginSettingsSnapshot settings, string platform)
		{ Game = game; Package = package; Settings = settings; Platform = platform; }
	}

	/// <summary>Unmet requirements belonging to one plugin, for the Player's settings page.</summary>
	public sealed class PluginLaunchBlocker
	{
		public PluginDescriptor Plugin { get; }
		public IReadOnlyList<PluginValidationIssue> Issues { get; }
		public PluginLaunchBlocker(PluginDescriptor plugin, IEnumerable<PluginValidationIssue> issues)
		{ Plugin = plugin; Issues = Array.AsReadOnly(issues.ToArray()); }
	}

	/// <summary>Stops table activation when a requirement fails, including a race detected during plugin preparation.</summary>
	public sealed class PluginLaunchBlockedException : Exception
	{
		public IReadOnlyList<PluginLaunchBlocker> Blockers { get; }
		public PluginLaunchBlockedException(IEnumerable<PluginLaunchBlocker> blockers) : this(blockers.ToArray()) { }
		private PluginLaunchBlockedException(PluginLaunchBlocker[] blockers)
			: base(string.Join("\n", blockers.SelectMany(blocker => blocker.Issues.Select(issue => blocker.Plugin.Name + ": " + issue.Message))))
		{ Blockers = Array.AsReadOnly(blockers); }
		public PluginLaunchBlockedException(PluginDescriptor plugin, string message, string field = "")
			: this(new[] { new PluginLaunchBlocker(plugin, new[] { new PluginValidationIssue(field, message) }) }) { }
	}
}
