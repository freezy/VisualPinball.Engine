// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Entry point of a Player plugin: describes the plugin and its settings, and creates plugin instances when a game
	/// starts.
	/// </summary>
	/// <remarks>
	/// An integration package registers one factory with <see cref="PlayerPluginRegistry"/> at startup. The
	/// <see cref="PluginHost"/> reads <see cref="Descriptor"/> and <see cref="Schema"/> without creating the plugin, for
	/// example to show the Player's plugin settings page while no game runs. When a game starts and the plugin is enabled,
	/// supported and has its dependencies, the host validates the settings and calls <see cref="Create"/>. PinUP, the B2S
	/// backglass, DMD and PinMAME packages each ship one factory.
	/// <example>
	/// A minimal factory with one live setting, registered before the first scene loads:
	/// <code>
	/// internal sealed class VolumePluginFactory : IPlayerPluginFactory
	/// {
	///     public PluginDescriptor Descriptor { get; } =
	///         new PluginDescriptor("org.example.volume", "Volume", "Sets the ROM volume.") { EnabledByDefault = true };
	///     public PluginSettingsSchema Schema { get; } = new PluginSettingsSchema(1, new PluginSettingsGroup("Audio", "",
	///         new PluginField("Volume", "Volume", SettingKind.Integer) {
	///             Default = 100, Minimum = 0, Maximum = 100, Change = ConfigurationChange.Live
	///         }));
	///     public IPluginSettingsProvider? SettingsProvider =&gt; null;
	///     public IPlayerPlugin Create(PluginApplicationContext context) =&gt; new VolumePlugin(context);
	///
	///     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	///     private static void Register() =&gt; PlayerPluginRegistry.Register(new VolumePluginFactory());
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IPlayerPluginFactory
	{
		/// <summary>
		/// The plugin's identity, presentation and ordering rules. Must return the same instance every time; the host reads it
		/// whenever it selects or orders plugins.
		/// </summary>
		PluginDescriptor Descriptor { get; }
		/// <summary>
		/// The plugin's settings: fields, defaults, scopes and migrations. The Player builds the settings page from it, the
		/// <see cref="PluginConfigurationStore"/> stores values by it, and the host validates the effective settings against
		/// it before creating the plugin. Must return the same instance every time.
		/// </summary>
		PluginSettingsSchema Schema { get; }
		/// <summary>
		/// Optional source of dynamic choices, actions and extra validation for the settings page, or null when the schema is
		/// self-contained. The host also calls its <see cref="IPluginSettingsProvider.Validate"/> before creating the plugin;
		/// any issue faults the plugin for that game.
		/// </summary>
		IPluginSettingsProvider? SettingsProvider { get; }
		/// <summary>
		/// Creates a plugin instance. The host calls it on the owner thread once per game, for each enabled and selected
		/// plugin, and then drives the instance through its <see cref="IPlayerPlugin"/> lifecycle. Keep it cheap and do
		/// asynchronous work in <see cref="IPlayerPlugin.InitializeAsync"/>. An exception faults the plugin for that game.
		/// </summary>
		/// <param name="context">
		/// Host services for the instance, valid until it is disposed. Keep it; the game hooks do not receive it again.
		/// </param>
		IPlayerPlugin Create(PluginApplicationContext context);
	}

	/// <summary>A plugin instance, driven by the <see cref="PluginHost"/> through the lifecycle of one game.</summary>
	/// <remarks>
	/// The host creates a new instance for every game through <see cref="IPlayerPluginFactory.Create"/> and calls, in
	/// order: <see cref="InitializeAsync"/>, <see cref="PrepareGameAsync"/>, <see cref="StartGameAsync"/>, then
	/// <see cref="GameReady"/>, <see cref="PauseChanged"/>, <see cref="FocusChanged"/> and the optional
	/// <see cref="IPluginTick"/> and <see cref="IPluginRender"/> hooks while the game runs, and finally
	/// <see cref="StopGameAsync"/>, <see cref="ShutdownAsync"/> and <see cref="IDisposable.Dispose"/>. Plugins are ordered by
	/// their <see cref="PluginDescriptor"/> in each startup and dispatch phase; cleanup runs in reverse start order.
	/// <para>
	/// Every hook is called on the owner thread (Unity's main thread in the Player). An asynchronous hook that awaits
	/// worker tasks must switch back with <c>await context.Scheduler.Switch()</c> before touching Unity or other
	/// owner-thread state. Startup hooks get a token that is cancelled when the game is cancelled or after
	/// <see cref="PluginHost.StartupTimeout"/>; cleanup hooks get one that is cancelled after
	/// <see cref="PluginHost.CleanupTimeout"/>. A hook that misses its deadline makes the plugin
	/// <see cref="PluginState.Unresponsive"/>: the host calls <see cref="IPluginEmergencyStop.EmergencyStop"/> and keeps the
	/// instance's resources until the hook finishes. An exception from any hook except the cleanup hooks makes the plugin
	/// <see cref="PluginState.Faulted"/>; the host cleans it up and blocks plugins that depend on it, while other plugins
	/// keep running.
	/// </para>
	/// Derive from <see cref="PlayerPlugin"/> to implement only the hooks you need.
	/// </remarks>
	public interface IPlayerPlugin : IDisposable
	{
		/// <summary>
		/// First hook after <see cref="IPlayerPluginFactory.Create"/>, for setup that does not depend on the game, such as
		/// loading native libraries. Called once per instance, in initialize order, before any plugin is prepared.
		/// </summary>
		/// <param name="cancellation">Cancelled when the game is cancelled or the startup deadline expires.</param>
		Task InitializeAsync(CancellationToken cancellation);
		/// <summary>
		/// Sets up everything the plugin needs for the game: reads <see cref="PluginGameContext.Settings"/>, finds the table
		/// through a capability, binds or publishes resources, opens windows. Register everything that must be released
		/// when the game ends with <see cref="PluginGameContext.Lifetime"/>. Called once, in prepare order; PinUP loads the
		/// matching PUP pack and binds the DMD here, and B2S loads the backglass and creates its windows.
		/// </summary>
		/// <param name="game">The game being started and the services scoped to it.</param>
		/// <param name="cancellation">Cancelled when the game is cancelled or the startup deadline expires.</param>
		/// <remarks><see cref="StopGameAsync"/> is called even when this hook throws or is cancelled.</remarks>
		Task PrepareGameAsync(PluginGameContext game, CancellationToken cancellation);
		/// <summary>
		/// Starts the plugin's game activity after every plugin has been prepared, so resources published by other plugins
		/// during preparation are available. When it completes the plugin is <see cref="PluginState.Running"/>, and cleanup
		/// later runs in reverse of the order in which plugins were started.
		/// </summary>
		/// <param name="cancellation">Cancelled when the game is cancelled or the startup deadline expires.</param>
		Task StartGameAsync(CancellationToken cancellation);
		/// <summary>
		/// Called once when the table's player has started and the game is playable. If that happens while the host is still
		/// starting plugins, the call is delivered once startup finishes. Only running plugins receive it.
		/// </summary>
		void GameReady();
		/// <summary>
		/// Called when the game is paused or resumed, for example when the Player opens or closes its in-game menu. If the
		/// state changes several times while the host is busy, only the latest state is delivered. Only running plugins
		/// receive it.
		/// </summary>
		/// <param name="paused">True when the game was paused, false when it resumed.</param>
		void PauseChanged(bool paused);
		/// <summary>
		/// Called when the application window gains or loses focus. If focus changes several times while the host is busy,
		/// only the latest state is delivered. Only running plugins receive it.
		/// </summary>
		/// <param name="focused">True when the application has focus.</param>
		void FocusChanged(bool focused);
		/// <summary>
		/// Ends the plugin's game activity: stop workers and playback and wait until they no longer use game resources.
		/// Called when the game stops, or earlier when the plugin faults or is blocked, but only if
		/// <see cref="PrepareGameAsync"/> was called. Afterwards the host disposes <see cref="PluginGameContext.Lifetime"/>.
		/// Exceptions are logged and cleanup continues.
		/// </summary>
		/// <param name="cancellation">Cancelled after <see cref="PluginHost.CleanupTimeout"/>.</param>
		Task StopGameAsync(CancellationToken cancellation);
		/// <summary>
		/// Releases what <see cref="InitializeAsync"/> set up. Called after <see cref="StopGameAsync"/> for every created
		/// instance, even when initialization failed. Afterwards the host disposes
		/// <see cref="PluginApplicationContext.Lifetime"/> and then the plugin itself. Exceptions are logged and cleanup
		/// continues.
		/// </summary>
		/// <param name="cancellation">Cancelled after <see cref="PluginHost.CleanupTimeout"/>.</param>
		Task ShutdownAsync(CancellationToken cancellation);
	}

	/// <summary>Optional hook for plugins that need a per-frame update on the owner thread.</summary>
	/// <remarks>
	/// The Player calls it from its frame update, so it keeps running while the game is paused. B2S advances lamp and reel
	/// animations here, and DMD pumps its presentation destinations. Only running plugins are ticked, and a plugin is
	/// skipped while its asynchronous live reconfiguration is in progress.
	/// </remarks>
	public interface IPluginTick
	{
		/// <summary>Advances the plugin by one frame.</summary>
		/// <param name="unscaledSeconds">Real time since the previous frame in seconds, unaffected by Unity's time scale.</param>
		void Tick(double unscaledSeconds);
	}

	/// <summary>Optional hook for plugins that must update their output after all frame updates and before rendering.</summary>
	/// <remarks>
	/// The Player calls it from <c>LateUpdate</c>, once per frame. B2S composes the backglass and updates its windows and
	/// surfaces here, and PinUP renders its screen outputs. Only running plugins receive it.
	/// </remarks>
	public interface IPluginRender
	{
		/// <summary>Updates the plugin's visual output for the coming frame. Called on the owner thread.</summary>
		void BeforeRender();
	}

	/// <summary>Optional hook for stopping a plugin immediately, without awaiting anything.</summary>
	/// <remarks>
	/// The host calls it when there is no time for an orderly stop: when the application quits, when stopping the game
	/// cannot wait for a pending operation within <see cref="PluginHost.CleanupTimeout"/>, and when one of the plugin's
	/// hooks misses its deadline. Normal <see cref="IPlayerPlugin.StopGameAsync"/> and
	/// <see cref="IPlayerPlugin.ShutdownAsync"/> may not run afterwards, but <see cref="IDisposable.Dispose"/> still does.
	/// </remarks>
	public interface IPluginEmergencyStop
	{
		/// <summary>
		/// Stops playback, workers and device output synchronously on the owner thread. Must not block or throw, and must be
		/// safe to call more than once and after the plugin has already stopped. Pair it with
		/// <see cref="IPluginDeferredCleanup"/> when native workers need time to finish.
		/// </summary>
		void EmergencyStop();
	}

	/// <summary>
	/// Optional hook for plugins whose native workers may still use the plugin's resources after an emergency stop.
	/// </summary>
	/// <remarks>
	/// After <see cref="IPluginEmergencyStop.EmergencyStop"/>, or after abandoning an unresponsive hook, the host reads
	/// <see cref="Quiescence"/>. Until it completes the host keeps the instance's resource scopes (native handles, device
	/// leases) alive and marks the plugin <see cref="PluginState.Unresponsive"/>, so no new instance can start against
	/// the same hardware. The DMD plugin returns the completion of its native bridge shutdown here, PinUP that of its
	/// media player.
	/// </remarks>
	public interface IPluginDeferredCleanup
	{
		/// <summary>
		/// Completes when native workers can no longer use the plugin's resources. Read after an emergency stop; return a
		/// completed task when nothing is pending. A fault is logged and treated as completion.
		/// </summary>
		Task Quiescence { get; }
	}

	/// <summary>
	/// Optional hook for applying <see cref="ConfigurationChange.Live"/> settings to a running plugin asynchronously.
	/// </summary>
	/// <remarks>
	/// When the user commits settings that change a live field for the running game, the host calls this hook before saving.
	/// If the plugin also implements <see cref="IPluginSynchronousReconfiguration"/>, that one is used instead. While the
	/// call runs the plugin is skipped for tick, render and other dispatched calls. If saving fails afterwards, the host
	/// calls it again with the previous settings to roll back.
	/// </remarks>
	public interface IPluginReconfiguration
	{
		/// <summary>
		/// Applies new settings. Reconfiguration is transactional: on failure or cancellation the previous runtime state must
		/// remain intact. Called on the owner thread.
		/// </summary>
		/// <param name="settings">
		/// The plugin's current runtime settings with the changed live fields replaced. Fields that are not live keep their
		/// values from game start. Keep this snapshot as the plugin's current settings.
		/// </param>
		/// <param name="cancellation">Cancelled by the caller or after <see cref="PluginHost.StartupTimeout"/>.</param>
		/// <returns>
		/// Success, or a failure whose <see cref="PluginActionResult.Message"/> the Player shows; on failure nothing is saved.
		/// </returns>
		Task<PluginActionResult> ApplyAsync(PluginSettingsSnapshot settings, CancellationToken cancellation);
	}

	/// <summary>
	/// Optional hook for applying <see cref="ConfigurationChange.Live"/> settings to a running plugin synchronously.
	/// </summary>
	/// <remarks>
	/// Preferred over <see cref="IPluginReconfiguration"/> when a plugin implements both. Use it for fast updates such as a
	/// volume change: PinMAME sets the engine's output gain here, so the Player's volume mixer takes effect immediately.
	/// <example>
	/// <code>
	/// public PluginActionResult Apply(PluginSettingsSnapshot settings)
	/// {
	///     _application.Scheduler.VerifyOwner();
	///     if (_engine != null) _engine.OutputGain = Mathf.Clamp(settings.Integer("Volume", 100), 0, 100) / 100f;
	///     return new PluginActionResult(true, "");
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IPluginSynchronousReconfiguration
	{
		/// <summary>
		/// Applies new settings on the owner thread. On failure the previous runtime state must remain intact. If saving fails
		/// afterwards, the host calls it again with the previous settings to roll back.
		/// </summary>
		/// <param name="settings">
		/// The plugin's current runtime settings with the changed live fields replaced. Keep this snapshot as the plugin's
		/// current settings.
		/// </param>
		/// <returns>
		/// Success, or a failure whose <see cref="PluginActionResult.Message"/> the Player shows; on failure nothing is saved.
		/// </returns>
		PluginActionResult Apply(PluginSettingsSnapshot settings);
	}

	/// <summary>
	/// Base class for plugins with no-op implementations of every <see cref="IPlayerPlugin"/> hook, so a plugin overrides
	/// only what it needs. The host still controls its lifetime.
	/// </summary>
	/// <remarks>
	/// All bundled plugins derive from it and add optional interfaces such as <see cref="IPluginTick"/> or
	/// <see cref="IPluginEmergencyStop"/>.
	/// <example>
	/// A plugin that applies a volume setting to the table's PinMAME engine:
	/// <code>
	/// internal sealed class VolumePlugin : PlayerPlugin, IPluginSynchronousReconfiguration
	/// {
	///     private readonly PluginApplicationContext _application;
	///     private PinMameGamelogicEngine? _engine;
	///     public VolumePlugin(PluginApplicationContext application) =&gt; _application = application;
	///
	///     public override Task PrepareGameAsync(PluginGameContext game, CancellationToken cancellation)
	///     {
	///         var scene = game.Capabilities.Require&lt;IUnityGameScene&gt;(UnityPluginCapability.GameScene);
	///         _engine = scene.Table.GetComponentInChildren&lt;PinMameGamelogicEngine&gt;(true);
	///         Apply(game.Settings);
	///         return Task.CompletedTask;
	///     }
	///
	///     public PluginActionResult Apply(PluginSettingsSnapshot settings)
	///     {
	///         if (_engine != null) _engine.OutputGain = settings.Integer("Volume", 100) / 100f;
	///         return new PluginActionResult(true, "");
	///     }
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public abstract class PlayerPlugin : IPlayerPlugin
	{
		/// <inheritdoc/>
		/// <remarks>Does nothing by default.</remarks>
		public virtual Task InitializeAsync(CancellationToken cancellation) => Task.CompletedTask;
		/// <inheritdoc/>
		/// <remarks>Does nothing by default.</remarks>
		public virtual Task PrepareGameAsync(PluginGameContext game, CancellationToken cancellation) => Task.CompletedTask;
		/// <inheritdoc/>
		/// <remarks>Does nothing by default.</remarks>
		public virtual Task StartGameAsync(CancellationToken cancellation) => Task.CompletedTask;
		/// <inheritdoc/>
		/// <remarks>Does nothing by default.</remarks>
		public virtual void GameReady() { }
		/// <inheritdoc/>
		/// <remarks>Does nothing by default.</remarks>
		public virtual void PauseChanged(bool paused) { }
		/// <inheritdoc/>
		/// <remarks>Does nothing by default.</remarks>
		public virtual void FocusChanged(bool focused) { }
		/// <inheritdoc/>
		/// <remarks>Does nothing by default.</remarks>
		public virtual Task StopGameAsync(CancellationToken cancellation) => Task.CompletedTask;
		/// <inheritdoc/>
		/// <remarks>Does nothing by default.</remarks>
		public virtual Task ShutdownAsync(CancellationToken cancellation) => Task.CompletedTask;
		/// <summary>
		/// Releases whatever the plugin still holds. The host calls it last, on the owner thread, also after an emergency
		/// stop where <see cref="StopGameAsync"/> and <see cref="ShutdownAsync"/> did not run, so it must release
		/// everything and tolerate an already stopped plugin. Does nothing by default.
		/// </summary>
		public virtual void Dispose() { }
	}

	/// <summary>Host services handed to a plugin instance when it is created, valid for the instance's whole life.</summary>
	/// <remarks>
	/// The host builds it and passes it to <see cref="IPlayerPluginFactory.Create"/>; plugins keep it in a field. Since the
	/// host creates a new instance for every game, "application" here means the instance's life, from creation until
	/// <see cref="IPlayerPlugin.ShutdownAsync"/> completes; game-specific services come with
	/// <see cref="PluginGameContext"/>.
	/// </remarks>
	public sealed class PluginApplicationContext
	{
		/// <summary>This plugin's descriptor, for example to pass <see cref="PluginDescriptor.Id"/> as a resource owner.</summary>
		public PluginDescriptor Plugin { get; }
		/// <summary>
		/// The settings store. Plugins can use <see cref="PluginConfigurationStore.DirectoryFor"/> for their own files; the
		/// DMD plugin stages colorization caches there.
		/// </summary>
		public PluginConfigurationStore Configuration { get; }
		/// <summary>
		/// The owner-thread scheduler. Await <see cref="OwnerScheduler.Switch"/> to return to the owner thread after worker
		/// code, <see cref="OwnerScheduler.Post"/> to queue work there, and <see cref="OwnerScheduler.VerifyOwner"/> to assert
		/// that a call is on it. The host pumps it at the start of every <see cref="PluginHost.Tick"/>, which the Player calls
		/// every frame.
		/// </summary>
		public OwnerScheduler Scheduler { get; }
		/// <summary>
		/// Versioned services published by the host and by other plugins. Same instance as
		/// <see cref="PluginGameContext.Capabilities"/>.
		/// </summary>
		public PluginCapabilities Capabilities { get; }
		/// <summary>
		/// The registry for binding and publishing displays, controllers, surfaces and other resources. Same instance as
		/// <see cref="PluginGameContext.Resources"/>.
		/// </summary>
		public PluginResourceRegistry Resources { get; }
		/// <summary>
		/// Resources owned for the instance's whole life. The host disposes it after <see cref="IPlayerPlugin.ShutdownAsync"/>
		/// and before the plugin itself, in reverse order of registration; for game-specific resources use
		/// <see cref="PluginGameContext.Lifetime"/>.
		/// </summary>
		public ResourceScope Lifetime { get; }
		/// <summary>
		/// Writes a diagnostic line, prefixed with the plugin ID, at info level. Prefer <see cref="Logger"/>, which has levels.
		/// </summary>
		public Action<string> Log { get; }
		/// <summary>
		/// The plugin's log, with levels. The Player writes it to the session log; errors also end up in error reports. Use
		/// <see cref="PluginLogLevel.Warn"/> for conditions the user can fix, such as a missing PUP pack.
		/// </summary>
		public IPluginLog Logger { get; }
		/// <summary>Creates the context. Called by the <see cref="PluginHost"/>; plugins only receive it.</summary>
		/// <param name="plugin">The descriptor of the plugin being created.</param>
		/// <param name="configuration">The settings store.</param>
		/// <param name="scheduler">The owner-thread scheduler.</param>
		/// <param name="capabilities">The shared capability registry.</param>
		/// <param name="resources">The shared resource registry.</param>
		/// <param name="lifetime">The scope disposed after the instance shuts down.</param>
		/// <param name="log">The diagnostic sink for this plugin.</param>
		public PluginApplicationContext(PluginDescriptor plugin, PluginConfigurationStore configuration, OwnerScheduler scheduler,
			PluginCapabilities capabilities, PluginResourceRegistry resources, ResourceScope lifetime, Action<string> log)
			: this(plugin, configuration, scheduler, capabilities, resources, lifetime, new TextPluginLog(log)) { }
		/// <summary>Creates the context with a levelled log. Called by the <see cref="PluginHost"/>; plugins only receive it.</summary>
		/// <param name="logger">The plugin's log. <see cref="Log"/> writes to it at info level.</param>
		public PluginApplicationContext(PluginDescriptor plugin, PluginConfigurationStore configuration, OwnerScheduler scheduler,
			PluginCapabilities capabilities, PluginResourceRegistry resources, ResourceScope lifetime, IPluginLog logger)
		{
			Plugin = plugin; Configuration = configuration; Scheduler = scheduler; Capabilities = capabilities; Resources = resources; Lifetime = lifetime;
			Logger = logger;
			Log = message => logger.Write(PluginLogLevel.Info, message);
		}
	}

	/// <summary>The game being started and the services scoped to it, passed to <see cref="IPlayerPlugin.PrepareGameAsync"/>.</summary>
	/// <remarks>
	/// It tells the plugin which game runs (<see cref="Identity"/>, <see cref="PackagePath"/>, <see cref="Title"/>), how
	/// the plugin is configured for it (<see cref="Settings"/>), and how to find the game's resources
	/// (<see cref="ControllerId"/>, <see cref="Session"/>). Keep it in a field if later hooks need it; it stays valid until
	/// <see cref="IPlayerPlugin.StopGameAsync"/> completes.
	/// <example>
	/// Binding the game's DMD, as PinUP does:
	/// <code>
	/// var selection = new PluginResourceSelection(ResourceKind.Display) {
	///     Controller = game.ControllerId, Role = "dmd", Session = game.Session
	/// };
	/// var dmd = game.Lifetime.Own(game.Resources.Bind&lt;IPluginDisplaySource&gt;(selection));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginGameContext
	{
		/// <summary>The game being played, used to find matching media such as a PUP pack, backglass or colorization.</summary>
		public GameIdentity Identity { get; }
		/// <summary>Full path of the table package being played. The B2S plugin uses it, with the title, to match a backglass.</summary>
		public string PackagePath { get; }
		/// <summary>The table's title as shown in the Player.</summary>
		public string Title { get; }
		/// <summary>
		/// ID of the game's controller, such as <c>pinmame:t2_l8/controller/0</c>. Select resources of this controller with
		/// it, and use it as the prefix of IDs of resources the plugin publishes for this game.
		/// </summary>
		public string ControllerId { get; }
		/// <summary>
		/// Number of the game session, increasing with every game the Player starts. Pass it in resource selections and
		/// descriptors so they never match resources of a previous game.
		/// </summary>
		public long Session { get; }
		/// <summary>
		/// The plugin's effective settings for this game when it was prepared: defaults, global values and the game's
		/// overrides merged. It does not change afterwards; live changes arrive through
		/// <see cref="IPluginSynchronousReconfiguration"/> or <see cref="IPluginReconfiguration"/>.
		/// </summary>
		public PluginSettingsSnapshot Settings { get; }
		/// <summary>
		/// Versioned services, including those the Player publishes for the game such as the table scene. Same instance as
		/// <see cref="PluginApplicationContext.Capabilities"/>.
		/// </summary>
		public PluginCapabilities Capabilities { get; }
		/// <summary>
		/// The registry for binding and publishing resources. Same instance as
		/// <see cref="PluginApplicationContext.Resources"/>.
		/// </summary>
		public PluginResourceRegistry Resources { get; }
		/// <summary>
		/// Resources owned for this game, such as bindings, subscriptions and created game objects. The host disposes it
		/// right after <see cref="IPlayerPlugin.StopGameAsync"/>, in reverse order of registration.
		/// </summary>
		public ResourceScope Lifetime { get; }
		/// <summary>
		/// Cancelled when the game stops or its startup is cancelled. Use it for background work tied to the game; it is
		/// not the per-hook deadline token.
		/// </summary>
		public CancellationToken Cancellation { get; }
		/// <summary>Creates the context. Called by the <see cref="PluginHost"/>; plugins only receive it.</summary>
		/// <param name="identity">The game being played.</param>
		/// <param name="packagePath">Full path of the table package.</param>
		/// <param name="title">The table's title.</param>
		/// <param name="controllerId">ID of the game's controller.</param>
		/// <param name="session">The game session number.</param>
		/// <param name="settings">The plugin's effective settings for the game.</param>
		/// <param name="capabilities">The shared capability registry.</param>
		/// <param name="resources">The shared resource registry.</param>
		/// <param name="lifetime">The scope disposed after the game stops.</param>
		/// <param name="cancellation">The game's cancellation token.</param>
		public PluginGameContext(GameIdentity identity, string packagePath, string title, string controllerId, long session,
			PluginSettingsSnapshot settings, PluginCapabilities capabilities, PluginResourceRegistry resources, ResourceScope lifetime, CancellationToken cancellation)
		{ Identity = identity; PackagePath = packagePath; Title = title; ControllerId = controllerId; Session = session; Settings = settings; Capabilities = capabilities; Resources = resources; Lifetime = lifetime; Cancellation = cancellation; }
	}

	/// <summary>Global list of installed plugin factories, which the Player hands to its <see cref="PluginHost"/>.</summary>
	/// <remarks>
	/// Integration packages register their factory once at startup, from a
	/// <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]</c> method. The Player creates its
	/// host from <see cref="Installed"/> afterwards, and the host copies the list, so later registrations are not seen by
	/// an existing host. The registry is not thread-safe: register on the main thread.
	/// </remarks>
	public static class PlayerPluginRegistry
	{
		private static readonly Dictionary<string, IPlayerPluginFactory> Factories = new Dictionary<string, IPlayerPluginFactory>(StringComparer.Ordinal);
		/// <summary>The registered factories, as a live view in no particular order.</summary>
		public static IReadOnlyCollection<IPlayerPluginFactory> Installed => Factories.Values;
		/// <summary>
		/// Removes all registered factories. VPE calls this when the scripting domain starts, so factories registered
		/// by a previous play session in the editor do not linger; plugins have no reason to call it.
		/// </summary>
		public static void Reset() => Factories.Clear();
		/// <summary>Adds a factory under its <see cref="PluginDescriptor.Id"/>.</summary>
		/// <param name="factory">The factory to install.</param>
		/// <remarks>
		/// <example>
		/// <code>
		/// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		/// private static void Register() =&gt; PlayerPluginRegistry.Register(new PinUpPluginFactory());
		/// </code>
		/// </example>
		/// </remarks>
		/// <exception cref="InvalidOperationException">
		/// The factory's <see cref="PluginDescriptor.ApiVersion"/> differs from <see cref="PluginDescriptor.HostApiVersion"/>,
		/// or a factory with the same ID is already registered.
		/// </exception>
		public static void Register(IPlayerPluginFactory factory)
		{
			if (factory.Descriptor.ApiVersion != PluginDescriptor.HostApiVersion) throw new InvalidOperationException("Plugin API version is incompatible: " + factory.Descriptor.Id);
			if (Factories.ContainsKey(factory.Descriptor.Id)) throw new InvalidOperationException("Duplicate plugin ID: " + factory.Descriptor.Id);
			Factories.Add(factory.Descriptor.Id, factory);
		}
	}
}
