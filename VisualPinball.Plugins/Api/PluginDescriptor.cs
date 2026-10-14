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
	/// The lifecycle steps the <see cref="PluginHost"/> drives a plugin through. Each value names one hook of
	/// <see cref="IPlayerPlugin"/> or of an optional hook interface.
	/// </summary>
	/// <remarks>
	/// Phases serve two purposes. They select the ordering rules in <see cref="PluginDescriptor.PhaseOrder"/>, so a plugin
	/// can, for example, render after another plugin without also starting after it. They are also reported in
	/// <see cref="PluginStatus.Phase"/>, so the Player can say in which step a plugin failed. The host orders the startup
	/// and dispatch phases (<see cref="Initialize"/> to <see cref="Render"/>); cleanup does not use ordering rules, it runs
	/// in reverse start order.
	/// </remarks>
	public enum PluginPhase
	{
		/// <summary><see cref="IPlayerPlugin.InitializeAsync"/>, right after the host created the instance.</summary>
		Initialize,
		/// <summary><see cref="IPlayerPlugin.PrepareGameAsync"/>, where a plugin sets up everything it needs for the game.</summary>
		Prepare,
		/// <summary><see cref="IPlayerPlugin.StartGameAsync"/>, after every plugin has been prepared.</summary>
		Start,
		/// <summary><see cref="IPlayerPlugin.GameReady"/>, when the table's player has started.</summary>
		Ready,
		/// <summary><see cref="IPlayerPlugin.PauseChanged"/> with <c>true</c>, when the game is paused.</summary>
		Pause,
		/// <summary><see cref="IPlayerPlugin.PauseChanged"/> with <c>false</c>, when the game resumes.</summary>
		Resume,
		/// <summary><see cref="IPlayerPlugin.FocusChanged"/>, when the application gains or loses focus.</summary>
		Focus,
		/// <summary><see cref="IPluginTick.Tick"/>, once per frame.</summary>
		Tick,
		/// <summary><see cref="IPluginRender.BeforeRender"/>, once per frame after all frame updates.</summary>
		Render,
		/// <summary>
		/// <see cref="IPlayerPlugin.StopGameAsync"/>. Reported in <see cref="PluginStatus.Phase"/> while a plugin stops; not
		/// used for ordering, since cleanup runs in reverse start order.
		/// </summary>
		Stop,
		/// <summary>
		/// <see cref="IPlayerPlugin.ShutdownAsync"/>. Not used for ordering, since cleanup runs in reverse start order, and
		/// the host does not currently report it in <see cref="PluginStatus.Phase"/>.
		/// </summary>
		Shutdown
	}

	/// <summary>The runtime state of one installed plugin, as kept by the <see cref="PluginHost"/>.</summary>
	/// <remarks>
	/// The Player's plugin settings page reads it from <see cref="PluginHost.Statuses"/> and shows the state and
	/// <see cref="PluginStatus.Message"/> when a plugin is <see cref="Faulted"/>, <see cref="Blocked"/>,
	/// <see cref="Unresponsive"/> or <see cref="Unsupported"/>. A plugin cannot set its own state; the host derives it from
	/// configuration and from how the plugin's hooks complete.
	/// </remarks>
	public enum PluginState
	{
		/// <summary>
		/// Installed and selected, or cleanly stopped. This is the initial state and the state after a game ends normally.
		/// </summary>
		Available,
		/// <summary>Disabled by the user, globally or for the current game, so the host did not create an instance.</summary>
		Disabled,
		/// <summary>
		/// Not created because the current platform is not listed in <see cref="PluginDescriptor.Platforms"/>.
		/// </summary>
		Unsupported,
		/// <summary>
		/// Not started or stopped early because a dependency or required capability is missing or ambiguous, its ordering
		/// rules form a cycle, or the host was still busy with a previous game. <see cref="PluginStatus.Message"/> says which.
		/// </summary>
		Blocked,
		/// <summary>
		/// A startup hook (initialize, prepare or start) is running. <see cref="PluginStatus.Phase"/> says which one.
		/// </summary>
		Starting,
		/// <summary>
		/// <see cref="IPlayerPlugin.StartGameAsync"/> completed. Only running plugins receive ready, pause, focus, tick and
		/// render calls.
		/// </summary>
		Running,
		/// <summary>The host is running the plugin's stop and shutdown hooks.</summary>
		Stopping,
		/// <summary>
		/// A hook threw, or the plugin's settings were invalid at startup. <see cref="PluginStatus.Message"/> holds the error.
		/// The instance has been cleaned up and plugins that depend on it are blocked. The next game selects it again.
		/// </summary>
		Faulted,
		/// <summary>
		/// A hook did not finish before its deadline, or the plugin's workers have not yet quiesced after an emergency stop
		/// (see <see cref="IPluginDeferredCleanup"/>). The old instance keeps its resources until it drains, and new
		/// instances of the plugin are blocked until then; the state returns to <see cref="Available"/> afterwards.
		/// </summary>
		Unresponsive
	}

	/// <summary>Where a setting is stored and where the user can edit it.</summary>
	/// <remarks>
	/// Set per field through <see cref="PluginField.Scope"/>. The <see cref="PluginConfigurationStore"/> keeps one global
	/// <c>settings.json</c> per plugin and one profile per game that holds only explicitly changed values; the scope decides
	/// which of the two a field may live in. For example the B2S plugin makes its window placement
	/// <see cref="GlobalAndGame"/> so a backglass can sit on a different monitor for one table.
	/// </remarks>
	public enum ConfigurationScope
	{
		/// <summary>
		/// Stored only in the global settings. Game profiles never hold or override it, and it is not shown on a game's
		/// settings page. This is the default for <see cref="PluginField.Scope"/>.
		/// </summary>
		GlobalOnly,
		/// <summary>
		/// Stored only in game profiles. It is not shown on the global settings page and cannot be exposed as a global
		/// setting such as a volume mixer slider.
		/// </summary>
		GameOnly,
		/// <summary>
		/// A global value that a game profile may override. On a game's settings page the Player shows an override toggle
		/// for it; the game value wins when present.
		/// </summary>
		GlobalAndGame
	}

	/// <summary>When a changed setting reaches a running plugin.</summary>
	/// <remarks>
	/// Set per field through <see cref="PluginField.Change"/>. When the user commits settings while a game runs, the host
	/// pushes only <see cref="Live"/> fields to the running plugin, through <see cref="IPluginSynchronousReconfiguration"/>
	/// or <see cref="IPluginReconfiguration"/>. Everything else is saved and read when the next game is prepared. PinMAME
	/// and PinUP, for example, make their volume <see cref="Live"/> so the volume mixer takes effect immediately.
	/// </remarks>
	public enum ConfigurationChange
	{
		/// <summary>
		/// Applied to the running plugin as soon as it is committed. The plugin must implement
		/// <see cref="IPluginSynchronousReconfiguration"/> or <see cref="IPluginReconfiguration"/>; otherwise the host refuses
		/// to save a changed live field that affects the running game.
		/// </summary>
		Live,
		/// <summary>
		/// Saved immediately and used from the next game on. This is the default for <see cref="PluginField.Change"/>.
		/// </summary>
		NextGame,
		/// <summary>
		/// Only takes effect after the application restarts. The host currently treats it like <see cref="NextGame"/>: the
		/// value is saved and nothing is pushed to the running plugin.
		/// </summary>
		ApplicationRestart
	}

	/// <summary>Ordering rules for one <see cref="PluginPhase"/>, overriding or extending a descriptor's general rules.</summary>
	/// <remarks>
	/// Stored in <see cref="PluginDescriptor.PhaseOrder"/>. Use it when a plugin must run in a different position in one
	/// phase only, for example to render after another plugin that it otherwise starts before.
	/// <example>
	/// <code>
	/// descriptor.PhaseOrder = new Dictionary&lt;PluginPhase, PluginOrder&gt; {
	///     [PluginPhase.Render] = new PluginOrder { Priority = 50, After = new[] { "org.visualpinball.dmd" } }
	/// };
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginOrder
	{
		/// <summary>
		/// Replaces <see cref="PluginDescriptor.Order"/> for this phase. Lower values run earlier among plugins that no
		/// ordering rule constrains; ties are broken by plugin ID. Defaults to 0, so set it explicitly when the plugin's
		/// general <see cref="PluginDescriptor.Order"/> should still apply in this phase.
		/// </summary>
		public int Priority { get; set; }
		/// <summary>
		/// IDs of plugins this plugin must run before in this phase, in addition to <see cref="PluginDescriptor.Before"/>.
		/// IDs of plugins that are not selected are ignored.
		/// </summary>
		public IReadOnlyList<string> Before { get; set; } = Array.Empty<string>();
		/// <summary>
		/// IDs of plugins this plugin must run after in this phase, in addition to <see cref="PluginDescriptor.After"/>.
		/// IDs of plugins that are not selected are ignored.
		/// </summary>
		public IReadOnlyList<string> After { get; set; } = Array.Empty<string>();
	}

	/// <summary>
	/// Static description of a plugin: its identity, how the Player lists it, whether it is enabled by default, and how it
	/// is ordered relative to other plugins.
	/// </summary>
	/// <remarks>
	/// Every <see cref="IPlayerPluginFactory"/> exposes one descriptor. The host reads it before any plugin instance exists:
	/// to decide whether the plugin is enabled and supported for the game, to check its dependencies and required
	/// capabilities, and to order its hooks against other plugins. The Player's plugin page shows its
	/// <see cref="Name"/>, <see cref="Category"/>, <see cref="Version"/> and <see cref="Description"/>. Set all properties
	/// when the descriptor is created and do not change them afterwards; the host reads them at every game start.
	/// <example>
	/// The PinUP integration describes itself like this:
	/// <code>
	/// public PluginDescriptor Descriptor { get; } = new PluginDescriptor("org.visualpinball.pinup", "PinUp",
	///     "PUP packs, layered video and native screens, configured here instead of inside your tables.")
	///     { Category = "Media", Order = 10 };
	/// </code>
	/// A plugin that needs another plugin's service and only works on Windows:
	/// <code>
	/// new PluginDescriptor("org.example.cabinet-lights", "Cabinet lights", "Drives cabinet LEDs from the game's lamps.") {
	///     Category = "Hardware", Order = 40, Requires = new[] { "vpe.outputs.v1" }, Platforms = new[] { "windows" }
	/// };
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginDescriptor
	{
		/// <summary>
		/// The plugin API version this build of the host implements. <see cref="PlayerPluginRegistry.Register"/> rejects
		/// plugins whose <see cref="ApiVersion"/> differs.
		/// </summary>
		public const int HostApiVersion = 1;
		/// <summary>
		/// Stable, unique plugin ID, such as <c>org.visualpinball.pinup</c>. Only lowercase letters, digits, dots and
		/// hyphens. It keys the registry, the plugin's statuses and ordering references, and names the plugin's configuration
		/// folder, so it must never change between releases.
		/// </summary>
		public string Id { get; }
		/// <summary>Display name shown in the Player's plugin list, which is sorted by it.</summary>
		public string Name { get; }
		/// <summary>One or two sentences shown on the plugin's settings page, telling the user what the plugin does.</summary>
		public string Description { get; }
		/// <summary>
		/// The plugin's own version, shown as <c>v</c> followed by this value on its card in the Player. Informational only;
		/// configuration compatibility is handled by <see cref="PluginSettingsSchema.Version"/>. Defaults to <c>0.1.0</c>.
		/// </summary>
		public string Version { get; set; } = "0.1.0";
		/// <summary>
		/// The plugin API version the plugin was built against. Leave the default: it is <see cref="HostApiVersion"/> as
		/// compiled into the plugin, so a plugin built against another API version is rejected at registration.
		/// </summary>
		public int ApiVersion { get; set; } = HostApiVersion;
		/// <summary>
		/// Whether the plugin runs when the user has not enabled or disabled it, globally or for the game. Defaults to
		/// false, so a new plugin stays off until the user enables it.
		/// </summary>
		public bool EnabledByDefault { get; set; }
		/// <summary>
		/// Whether the user can disable the plugin. Defaults to true. Set it to false for configuration services that an
		/// application-owned engine relies on, such as the PinMAME plugin that adjusts ROM audio: such a plugin is always
		/// enabled, the Player hides its enable toggle, and
		/// <see cref="PluginConfigurationStore.SetEnabled"/> refuses to disable it.
		/// </summary>
		public bool CanDisable { get; set; } = true;
		/// <summary>
		/// Group label shown on the plugin's card in the Player, such as <c>Media</c>, <c>Displays</c> or <c>Gamelogic</c>.
		/// Defaults to <c>Integrations</c>.
		/// </summary>
		public string Category { get; set; } = "Integrations";
		/// <summary>
		/// Default ordering priority in every phase. Lower values run earlier among plugins that no dependency or
		/// <see cref="Before"/>/<see cref="After"/> rule constrains; ties are broken by <see cref="Id"/>. A
		/// <see cref="PhaseOrder"/> entry replaces it for that phase. The bundled plugins use PinMAME 5, PinUP 10, DMD 20 and
		/// B2S 30.
		/// </summary>
		public int Order { get; set; }
		/// <summary>
		/// IDs of plugins that must be selected and healthy for this one to run. They run before this plugin in every
		/// phase. If one is disabled, absent or fails, this plugin is <see cref="PluginState.Blocked"/>.
		/// </summary>
		public IReadOnlyList<string> DependsOn { get; set; } = Array.Empty<string>();
		/// <summary>
		/// IDs of plugins this plugin runs before in every phase. Unlike <see cref="DependsOn"/> it does not require them;
		/// IDs of plugins that are not selected are ignored.
		/// </summary>
		public IReadOnlyList<string> Before { get; set; } = Array.Empty<string>();
		/// <summary>
		/// IDs of plugins this plugin runs after in every phase. Unlike <see cref="DependsOn"/> it does not require them;
		/// IDs of plugins that are not selected are ignored.
		/// </summary>
		public IReadOnlyList<string> After { get; set; } = Array.Empty<string>();
		/// <summary>
		/// Capability IDs this plugin offers to others. Plugins that list one in <see cref="Requires"/> are ordered after this
		/// plugin. Declaring a capability does not publish it: the plugin publishes its service itself through
		/// <see cref="PluginCapabilities.Publish{T}"/>.
		/// </summary>
		public IReadOnlyList<string> Provides { get; set; } = Array.Empty<string>();
		/// <summary>
		/// Capability IDs this plugin needs, such as <c>vpe.outputs.v1</c>. Each must be published by the host when the game
		/// starts or provided by exactly one selected plugin, which then runs first. When none or several selected plugins
		/// provide it, this plugin is <see cref="PluginState.Blocked"/>.
		/// </summary>
		public IReadOnlyList<string> Requires { get; set; } = Array.Empty<string>();
		/// <summary>
		/// Per-phase ordering overrides. An entry's <see cref="PluginOrder.Priority"/> replaces <see cref="Order"/> for that
		/// phase, and its before/after lists are added to <see cref="Before"/> and <see cref="After"/>. Empty by default.
		/// </summary>
		public IReadOnlyDictionary<PluginPhase, PluginOrder> PhaseOrder { get; set; } = new Dictionary<PluginPhase, PluginOrder>();
		/// <summary>
		/// Platforms the plugin supports, compared with <see cref="PluginHost.Platform"/>. The Player uses <c>windows</c>,
		/// <c>macos</c> and <c>linux</c>, and Unity's lowercase platform name elsewhere. Empty, the default, means every
		/// platform; on an unlisted platform the plugin is <see cref="PluginState.Unsupported"/>.
		/// </summary>
		public IReadOnlyList<string> Platforms { get; set; } = Array.Empty<string>();

		/// <summary>Creates a descriptor with the plugin's identity and the texts the Player shows.</summary>
		/// <param name="id">The stable plugin ID; see <see cref="Id"/>.</param>
		/// <param name="name">The display name; see <see cref="Name"/>.</param>
		/// <param name="description">The short description; see <see cref="Description"/>.</param>
		/// <exception cref="ArgumentException">
		/// <paramref name="id"/> is empty, contains characters other than lowercase letters, digits, dots and hyphens, or is
		/// <c>.</c> or <c>..</c>.
		/// </exception>
		public PluginDescriptor(string id, string name, string description)
		{
			if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A plugin needs a stable ID.", nameof(id));
			foreach (char character in id)
				if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '.' && character != '-')
					throw new ArgumentException("Plugin IDs use lowercase letters, digits, dots and hyphens.", nameof(id));
			if (id == "." || id == "..") throw new ArgumentException("Invalid plugin ID.", nameof(id));
			Id = id;
			Name = name;
			Description = description;
		}
	}

	/// <summary>The current state of one installed plugin, updated by the <see cref="PluginHost"/>.</summary>
	/// <remarks>
	/// Read it from <see cref="PluginHost.Statuses"/>. The host changes it on the owner thread, so read it there too. The
	/// Player's plugin settings page uses it to explain why a plugin is not running.
	/// </remarks>
	public sealed class PluginStatus
	{
		/// <summary>The plugin's lifecycle state. Starts as <see cref="PluginState.Available"/>.</summary>
		public PluginState State { get; internal set; } = PluginState.Available;
		/// <summary>
		/// Human-readable detail for <see cref="State"/>, such as the error of a faulted hook or the missing provider of a
		/// blocked plugin. Never null; empty until the host sets it.
		/// </summary>
		public string Message { get; internal set; } = "";
		/// <summary>The phase the state refers to, such as the hook that failed, or null when it is not tied to one.</summary>
		public PluginPhase? Phase { get; internal set; }
	}

	/// <summary>
	/// Identifies the game being played, independently of the table package, as a provider namespace plus a key within it.
	/// </summary>
	/// <remarks>
	/// The Player gets it from the table's gamelogic engine (see <see cref="IGameIdentityProvider"/>) or, failing that,
	/// from the package (see <see cref="IPackagedGameIdentityReader"/>), and hands it to plugins in
	/// <see cref="PluginGameContext.Identity"/>. Per-game plugin settings and enablement are stored under its
	/// <see cref="Id"/>, and plugins use it to find matching media: the DMD plugin picks a colorization by
	/// <see cref="Key"/>, and the B2S plugin matches backglasses by ROM name when <see cref="Provider"/> is
	/// <c>pinmame</c>. Provider and key are trimmed and lowercased, so identities compare case-insensitively.
	/// <example>
	/// <code>
	/// var identity = new GameIdentity("pinmame", "T2_L8");
	/// // identity.Key == "t2_l8", identity.Id == "pinmame:t2_l8"
	/// string rom = identity.Provider == "pinmame" ? identity.Key : "";
	/// </code>
	/// </example>
	/// </remarks>
	public readonly struct GameIdentity : IEquatable<GameIdentity>
	{
		/// <summary>
		/// Namespace of <see cref="Key"/>, lowercased: <c>pinmame</c> for ROM names, <c>package</c> for the content hash the
		/// Player falls back to when nothing else identifies the game.
		/// </summary>
		public string Provider { get; }
		/// <summary>The game within <see cref="Provider"/>, lowercased, such as the ROM name <c>t2_l8</c>.</summary>
		public string Key { get; }
		/// <summary>
		/// <see cref="Provider"/> and <see cref="Key"/> joined by a colon, such as <c>pinmame:t2_l8</c>. Used as the stable
		/// key for per-game settings and as the prefix of the game's controller ID.
		/// </summary>
		public string Id => Provider + ":" + Key;
		/// <summary>Creates an identity, trimming and lowercasing both parts.</summary>
		/// <param name="provider">The namespace of the key, such as <c>pinmame</c>.</param>
		/// <param name="key">The game within that namespace, such as a ROM name.</param>
		/// <exception cref="ArgumentException"><paramref name="provider"/> or <paramref name="key"/> is null or blank.</exception>
		public GameIdentity(string provider, string key)
		{
			if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A game identity needs a provider and key.");
			Provider = provider.Trim().ToLowerInvariant();
			Key = key.Trim().ToLowerInvariant();
		}
		/// <summary>Whether both identities have the same <see cref="Provider"/> and <see cref="Key"/>.</summary>
		/// <param name="other">The identity to compare with.</param>
		public bool Equals(GameIdentity other) => Provider == other.Provider && Key == other.Key;
		/// <summary>Whether <paramref name="other"/> is a <see cref="GameIdentity"/> with the same provider and key.</summary>
		/// <param name="other">The object to compare with.</param>
		public override bool Equals(object? other) => other is GameIdentity identity && Equals(identity);
		/// <summary>Returns a hash of <see cref="Id"/>, consistent with <see cref="Equals(GameIdentity)"/>.</summary>
		public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);
		/// <summary>Returns <see cref="Id"/>, such as <c>pinmame:t2_l8</c>.</summary>
		public override string ToString() => Id;
	}
}
