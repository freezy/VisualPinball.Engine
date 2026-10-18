// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Runs the installed Player plugins for one game at a time: decides which plugins run, creates them, drives their lifecycle
	/// hooks in dependency order and cleans them up in reverse, so that a failing plugin does not take the others down.
	/// </summary>
	/// <remarks>
	/// The Player creates one host at startup from <see cref="PlayerPluginRegistry.Installed"/> and keeps it for the application's
	/// life. For every table it calls <see cref="PrepareAndStartAsync"/>, forwards frames and state changes (<see cref="Tick"/>,
	/// <see cref="BeforeRender"/>, <see cref="GameReady"/>, <see cref="PauseChanged"/>, <see cref="FocusChanged"/>) while the game
	/// runs, calls <see cref="StopGameAsync"/> when the user leaves the table, and <see cref="EmergencyStop"/> when the application
	/// quits. Its settings page edits settings through a <see cref="PluginSettingsSession"/> that commits through the host, so live
	/// fields reach the running plugin. Plugins never see the host; they get its services through
	/// <see cref="PluginApplicationContext"/> and <see cref="PluginGameContext"/>.
	/// <para>
	/// The host belongs to the thread that created its <see cref="Scheduler"/> (Unity's main thread in the Player). Plugin hooks
	/// run there, and the host's own asynchronous operations resume there only when the scheduler is pumped, which
	/// <see cref="Tick"/> does. Call <see cref="Tick"/> every frame, also while no game runs, or pending starts, stops and settings
	/// commits stall.
	/// </para>
	/// <para>
	/// Starting a game, stopping it, committing settings and cleaning up after a fault are serialized behind one lifecycle gate
	/// (see <see cref="IsTransitioning"/>). A plugin whose hook throws becomes <see cref="PluginState.Faulted"/>, is cleaned up, and
	/// blocks the plugins that depend on it; the others keep running. A plugin whose hook misses its deadline
	/// (<see cref="StartupTimeout"/> or <see cref="CleanupTimeout"/>), or whose workers are still busy after
	/// <see cref="EmergencyStop"/>, is quarantined: it gets <see cref="IPluginEmergencyStop.EmergencyStop"/>, becomes
	/// <see cref="PluginState.Unresponsive"/>, and keeps its resource scopes (native handles, device leases) until every hook the
	/// host started on it and its <see cref="IPluginDeferredCleanup.Quiescence"/> have completed. Then the host shuts it down (after
	/// a missed deadline) and disposes it. Meanwhile no new instance of that plugin is created; the host itself never waits for a
	/// quarantined instance.
	/// </para>
	/// <example>
	/// How the Player drives the host (simplified from its plugin controller and main menu):
	/// <code>
	/// // on the main thread, once:
	/// var host = new PluginHost(PlayerPluginRegistry.Installed, new PluginConfigurationStore(root), new OwnerScheduler(),
	///     capabilities, new PluginResourceRegistry(Debug.LogException), "windows", message =&gt; Debug.Log("[Plugins] " + message));
	///
	/// // per table:
	/// await host.PrepareAndStartAsync(identity, packagePath, title, identity.Id + "/controller/0", ++session, cancellation);
	///
	/// // every frame:
	/// private void Update() =&gt; host.Tick(Time.unscaledDeltaTime);
	/// private void LateUpdate() =&gt; host.BeforeRender();
	///
	/// // leaving the table, and quitting:
	/// await host.StopGameAsync();
	/// host.EmergencyStop();
	/// </code>
	/// </example>
	/// </remarks>
	public sealed partial class PluginHost
	{
		private sealed class HookDeadlineException : TimeoutException
		{
			internal Task Hook { get; }
			internal HookDeadlineException(Task hook) : base("Plugin hook did not finish before its cancellation deadline.") { Hook = hook; }
		}
		private sealed class Instance
		{
			internal readonly IPlayerPluginFactory Factory;
			internal readonly IPlayerPlugin Plugin;
			internal readonly ResourceScope ApplicationScope;
			internal readonly ResourceScope GameScope = new ResourceScope();
			internal bool Prepared;
			internal bool Cleaned;
			internal bool Abandoned;
			internal bool Busy;
			private readonly List<Task> _hooks = new List<Task>();
			/// <summary>Records a lifecycle hook until it completes, so a quarantine can wait for it even after its deadline.</summary>
			internal void Track(Task hook)
			{
				if (hook.IsCompleted) return;
				lock (_hooks) _hooks.Add(hook);
				hook.ContinueWith(done => { lock (_hooks) _hooks.Remove(done); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
			}
			/// <summary>Completes when every hook running now has finished.</summary>
			internal Task RunningHooks() { lock (_hooks) return _hooks.Count == 0 ? Task.CompletedTask : Task.WhenAll(_hooks.ToArray()); }
			internal PluginSettingsSnapshot? Settings;
			internal Instance(IPlayerPluginFactory factory, IPlayerPlugin plugin, ResourceScope applicationScope) { Factory = factory; Plugin = plugin; ApplicationScope = applicationScope; }
		}
		private readonly Dictionary<string, IPlayerPluginFactory> _factories;
		private readonly Dictionary<string, PluginStatus> _statuses;
		private readonly List<Instance> _entered = new List<Instance>();
		private readonly List<Instance> _startOrder = new List<Instance>();
		private readonly SemaphoreSlim _operation = new SemaphoreSlim(1, 1);
		private readonly Dictionary<string, Task> _abandoned = new Dictionary<string, Task>();
		private readonly Action<string> _log;
		private readonly Func<PluginDescriptor, IPluginLog> _pluginLog;
		private readonly Action<string, Exception> _error;
		private CancellationTokenSource? _gameCancellation;
		private bool _running;
		private bool _ready;
		private bool? _paused;
		private bool? _focused;
		private GameIdentity? _activeGame;
		private string[] _hostCapabilities = Array.Empty<string>();
		private readonly Dictionary<PluginPhase, Instance[]> _dispatch = new Dictionary<PluginPhase, Instance[]>();
		private Task _faultCleanup = Task.CompletedTask;
		/// <summary>
		/// The owner-thread scheduler passed to the constructor, handed to every plugin as
		/// <see cref="PluginApplicationContext.Scheduler"/>. <see cref="Tick"/> pumps it. Code that awaited a host operation can
		/// return to the owner thread with <c>await host.Scheduler.Switch()</c>, as the Player's settings page does.
		/// </summary>
		public OwnerScheduler Scheduler { get; }
		/// <summary>
		/// The store the host reads plugin policy and settings from when it starts a game, and saves committed settings to. Handed
		/// to every plugin as <see cref="PluginApplicationContext.Configuration"/>; the Player's settings page and volume mixer read
		/// through it as well.
		/// </summary>
		public PluginConfigurationStore Configuration { get; }
		/// <summary>
		/// The capability registry shared with every plugin. The Player publishes its own services here (events, output leases and
		/// the current game's table scene). <see cref="PrepareAndStartAsync"/> takes a snapshot of the published capability IDs to
		/// check <see cref="PluginDescriptor.Requires"/>, so publish host capabilities before starting a game.
		/// </summary>
		public PluginCapabilities Capabilities { get; }
		/// <summary>
		/// The resource registry shared with every plugin. The Player publishes the game's controller, displays and audio here
		/// before starting plugins, and passes the registry's snapshot to settings catalogs.
		/// </summary>
		public PluginResourceRegistry Resources { get; }
		/// <summary>
		/// The platform ID the host runs on: <c>windows</c>, <c>macos</c> or <c>linux</c> in the Player, or Unity's lowercase
		/// platform name elsewhere. Plugins whose <see cref="PluginDescriptor.Platforms"/> do not list it are
		/// <see cref="PluginState.Unsupported"/>. It is also passed to settings providers as <see cref="PluginChoiceRequest.Platform"/>.
		/// </summary>
		public string Platform { get; }
		/// <summary>
		/// The status of every installed plugin, keyed by <see cref="PluginDescriptor.Id"/>, with an entry for each factory from the
		/// start, also for plugins that never ran. The status objects are updated in place on the owner thread; read them there.
		/// The Player's settings page shows them to explain why a plugin is not running.
		/// </summary>
		public IReadOnlyDictionary<string, PluginStatus> Statuses => _statuses;
		/// <summary>
		/// The installed plugin factories passed to the constructor, in no particular order. Factories registered later are not
		/// included. The Player lists them on its settings page and in its volume mixer.
		/// </summary>
		public IReadOnlyCollection<IPlayerPluginFactory> Factories => _factories.Values;
		/// <summary>
		/// True from the end of <see cref="PrepareAndStartAsync"/> until the game is stopped by <see cref="StopGameAsync"/>,
		/// <see cref="EmergencyStop"/> or the next start. It is also true when some or all plugins failed to start; check
		/// <see cref="Statuses"/> for individual plugins.
		/// </summary>
		/// <summary>
		/// The plugins of the current game as the Player's pause menu shows them: the running ones with what they
		/// write as <see cref="IPluginStatusSource"/>, and the ones that should run but don't (starting, blocked,
		/// faulted, unresponsive) with why. Disabled and unsupported plugins are left out, and so is a running one
		/// whose status says nothing, since it has nothing to do in this game.
		/// </summary>
		/// <remarks>Call on the owner thread. A plugin that throws while describing itself is logged, not faulted.</remarks>
		public IReadOnlyList<PluginStatusReport> DescribeStatus()
		{
			Scheduler.VerifyOwner();
			var reports = new List<PluginStatusReport>();
			foreach (var factory in _factories.Values) {
				var status = _statuses[factory.Descriptor.Id];
				switch (status.State) {
					case PluginState.Running:
						var instance = _startOrder.FirstOrDefault(entry => entry.Factory == factory);
						if (instance?.Plugin is not IPluginStatusSource source) {
							reports.Add(new PluginStatusReport(factory.Descriptor, status.State, "", Array.Empty<KeyValuePair<string, string>>()));
							break;
						}
						var writer = new PluginStatusWriter();
						try {
							source.DescribeStatus(writer);
						} catch (Exception error) {
							_error(factory.Descriptor.Id + " could not describe its status", error);
							writer.Add("Status", "unavailable");
						}
						if (writer.Lines.Count > 0) reports.Add(new PluginStatusReport(factory.Descriptor, status.State, "", writer.Lines));
						break;
					case PluginState.Starting:
					case PluginState.Blocked:
					case PluginState.Faulted:
					case PluginState.Unresponsive:
						reports.Add(new PluginStatusReport(factory.Descriptor, status.State, status.Message, Array.Empty<KeyValuePair<string, string>>()));
						break;
				}
			}
			return reports;
		}
		public bool IsRunning => _running;
		/// <summary>
		/// True while a lifecycle operation holds the host's gate: starting or stopping a game, committing settings, or cleaning up
		/// a plugin that faulted during a dispatched call. Meanwhile <see cref="GameReady"/>, <see cref="PauseChanged"/> and
		/// <see cref="FocusChanged"/> are held and delivered (latest state only) when the operation ends. <see cref="Tick"/> and
		/// <see cref="BeforeRender"/> still reach running plugins while settings are committed or a faulted plugin is cleaned up.
		/// </summary>
		public bool IsTransitioning => _operation.CurrentCount == 0;
		/// <summary>
		/// How long each cleanup hook (<see cref="IPlayerPlugin.StopGameAsync"/>, <see cref="IPlayerPlugin.ShutdownAsync"/>) may run
		/// before its plugin is quarantined as <see cref="PluginState.Unresponsive"/>, and how long <see cref="StopGameAsync"/> waits
		/// for a running operation before it falls back to <see cref="EmergencyStop"/>. Also bounds the deferred shutdown of a
		/// quarantined instance. Defaults to 10 seconds.
		/// </summary>
		public TimeSpan CleanupTimeout { get; set; } = TimeSpan.FromSeconds(10);
		/// <summary>
		/// How long each startup hook (<see cref="IPlayerPlugin.InitializeAsync"/>, <see cref="IPlayerPlugin.PrepareGameAsync"/>,
		/// <see cref="IPlayerPlugin.StartGameAsync"/>) and each <see cref="IPluginReconfiguration.ApplyAsync"/> may run before its
		/// plugin is quarantined as <see cref="PluginState.Unresponsive"/>, and how long <see cref="PrepareAndStartAsync"/> waits for
		/// a running operation to finish. Defaults to 30 seconds.
		/// </summary>
		public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);
		/// <summary>
		/// Creates a host for a fixed set of plugins. No plugin instance is created before <see cref="PrepareAndStartAsync"/>, and
		/// every plugin starts as <see cref="PluginState.Available"/>.
		/// </summary>
		/// <param name="factories">The installed plugins, usually <see cref="PlayerPluginRegistry.Installed"/>. The host copies them.</param>
		/// <param name="configuration">The store for plugin policy and settings.</param>
		/// <param name="scheduler">
		/// The owner-thread scheduler. Create it on the thread that drives the host; plugin hooks run on that thread.
		/// </param>
		/// <param name="capabilities">The capability registry shared with plugins.</param>
		/// <param name="resources">The resource registry shared with plugins.</param>
		/// <param name="platform">The platform ID, compared with <see cref="PluginDescriptor.Platforms"/>; see <see cref="Platform"/>.</param>
		/// <param name="log">
		/// Receives diagnostics: faults and cleanup errors, and plugin log lines (prefixed with the plugin ID) unless
		/// <paramref name="pluginLog"/> is given. Null discards them.
		/// </param>
		/// <param name="pluginLog">Creates each plugin's levelled log. Null sends plugin lines to <paramref name="log"/>.</param>
		/// <param name="error">
		/// Receives exceptions thrown by plugin code (faulted hooks, failed cleanup), with what failed. Null sends their
		/// message to <paramref name="log"/>.
		/// </param>
		/// <exception cref="ArgumentException">Two factories have the same <see cref="PluginDescriptor.Id"/>.</exception>
		public PluginHost(IEnumerable<IPlayerPluginFactory> factories, PluginConfigurationStore configuration, OwnerScheduler scheduler,
			PluginCapabilities capabilities, PluginResourceRegistry resources, string platform, Action<string>? log = null,
			Func<PluginDescriptor, IPluginLog>? pluginLog = null, Action<string, Exception>? error = null)
		{
			_factories = factories.ToDictionary(factory => factory.Descriptor.Id, StringComparer.Ordinal);
			_statuses = _factories.Keys.ToDictionary(id => id, _ => new PluginStatus());
			Configuration = configuration; Scheduler = scheduler; Capabilities = capabilities; Resources = resources; Platform = platform; _log = log ?? (_ => { });
			_pluginLog = pluginLog ?? (descriptor => new TextPluginLog(message => _log(descriptor.Id + ": " + message)));
			_error = error ?? ((what, exception) => _log(what + ": " + exception.Message));
		}

		/// <summary>
		/// Stops the current game's plugins, if any, then selects, creates and starts the plugins for a new game. Plugin failures do
		/// not throw: they are reported in <see cref="Statuses"/>, and the game runs with the remaining plugins.
		/// </summary>
		/// <remarks>
		/// The host first decides which plugins run. Plugins that are disabled for the game, unsupported on <see cref="Platform"/>,
		/// still quarantined from an earlier instance, missing a dependency or required capability, or caught in an ordering cycle
		/// are not created and get the matching <see cref="PluginState"/>. For the others, in initialize order, it validates the
		/// effective settings against the schema and <see cref="IPluginSettingsProvider.Validate"/>, creates the instance and calls
		/// <see cref="IPlayerPlugin.InitializeAsync"/>. Then it calls <see cref="IPlayerPlugin.PrepareGameAsync"/> on all of them in
		/// prepare order, and <see cref="IPlayerPlugin.StartGameAsync"/> in start order. A plugin that fails a step is cleaned up and
		/// the plugins depending on it are blocked before the next step. When the task completes, the started plugins are
		/// <see cref="PluginState.Running"/> and <see cref="IsRunning"/> is true; a <see cref="GameReady"/>, pause or focus change
		/// that arrived meanwhile is delivered then.
		/// <para>
		/// The Player calls it on the main thread after publishing the game's table scene capability and its controller and display
		/// resources, so plugins find them while they prepare.
		/// </para>
		/// </remarks>
		/// <param name="identity">
		/// The game to start. Selects per-game enablement and settings, and is passed to plugins as
		/// <see cref="PluginGameContext.Identity"/>.
		/// </param>
		/// <param name="packagePath">
		/// Full path of the table package, passed to plugins as <see cref="PluginGameContext.PackagePath"/> and to settings validation.
		/// </param>
		/// <param name="title">The table's title, passed to plugins as <see cref="PluginGameContext.Title"/>.</param>
		/// <param name="controllerId">
		/// ID of the game's controller resource, passed to plugins as <see cref="PluginGameContext.ControllerId"/>. The Player uses
		/// <c>identity.Id + "/controller/0"</c>.
		/// </param>
		/// <param name="session">
		/// The game session number, increasing with every game, passed to plugins as <see cref="PluginGameContext.Session"/>.
		/// </param>
		/// <param name="cancellation">
		/// Cancels the startup: the plugins started so far are stopped and the task is cancelled. The token is also linked to
		/// <see cref="PluginGameContext.Cancellation"/>, so cancelling it after startup signals the running plugins' game token, but
		/// does not stop them.
		/// </param>
		/// <returns>
		/// A task that completes when startup has finished. It also completes, without starting anything, when another operation
		/// still holds the host after <see cref="StartupTimeout"/>; every plugin is then <see cref="PluginState.Blocked"/>.
		/// </returns>
		/// <exception cref="OperationCanceledException">
		/// <paramref name="cancellation"/> was cancelled, or the game was stopped by <see cref="StopGameAsync"/> or
		/// <see cref="EmergencyStop"/> while it was starting.
		/// </exception>
		public async Task PrepareAndStartAsync(GameIdentity identity, string packagePath, string title, string controllerId, long session, CancellationToken cancellation)
		{
			using var gateDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
			gateDeadline.CancelAfter(StartupTimeout);
			try { await _operation.WaitAsync(gateDeadline.Token).ConfigureAwait(false); }
			catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) {
				await Scheduler.Switch();
				foreach (var factory in _factories.Values) SetStatus(factory.Descriptor.Id, PluginState.Blocked, "Plugin host is still stopping a previous operation.");
				return;
			}
			await Scheduler.Switch();
			try {
				await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
				await Scheduler.Switch();
				_gameCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
				_activeGame = identity;
				_hostCapabilities = Capabilities.Ids.ToArray();
				var gameToken = _gameCancellation.Token;
				var selected = Select(identity);
				foreach (var descriptor in PluginOrderResolver.Resolve(selected, PluginPhase.Initialize, _hostCapabilities)) {
					gameToken.ThrowIfCancellationRequested();
					if (MissingProvider(descriptor, selected) is string missing) { SetStatus(descriptor.Id, PluginState.Blocked, missing); continue; }
					var factory = _factories[descriptor.Id];
					var application = new ResourceScope();
					try {
						var settings = Configuration.Effective(descriptor, factory.Schema, identity);
						var issues = factory.Schema.Validate(settings).Concat(factory.SettingsProvider?.Validate(new PluginChoiceRequest(identity, settings, Platform) { Package = new PluginPackageReference(packagePath, title) }) ?? Array.Empty<PluginValidationIssue>()).ToArray();
						if (issues.Length > 0) throw new PluginConfigurationException(string.Join("; ", issues.Select(issue => issue.Field + ": " + issue.Message)));
						if (Configuration.Read(descriptor, factory.Schema).SchemaVersion > factory.Schema.Version || Configuration.Read(descriptor, factory.Schema, identity).SchemaVersion > factory.Schema.Version) throw new PluginConfigurationException("Configuration requires a newer plugin version.");
						var plugin = factory.Create(new PluginApplicationContext(descriptor, Configuration, Scheduler, Capabilities, Resources, application, _pluginLog(descriptor)));
						var entry = new Instance(factory, plugin, application);
						_entered.Add(entry);
						await InvokeAsync(entry, PluginPhase.Initialize, token => plugin.InitializeAsync(token), gameToken).ConfigureAwait(false);
						await Scheduler.Switch();
					} catch (OperationCanceledException) when (gameToken.IsCancellationRequested) { throw; }
					catch (Exception error) {
						await Scheduler.Switch();
						if (!(error is HookDeadlineException)) Fault(descriptor.Id, PluginPhase.Initialize, error);
						var failed = _entered.FirstOrDefault(entry => entry.ApplicationScope == application);
						if (failed == null) SafeDispose(application); else await CleanupAsync(failed, CancellationToken.None).ConfigureAwait(false);
						await Scheduler.Switch();
					}
				}
				foreach (var phase in new[] { PluginPhase.Prepare, PluginPhase.Start }) {
					await BlockDependentsAsync().ConfigureAwait(false); await Scheduler.Switch();
					var active = _entered.Where(Available).Select(entry => entry.Factory.Descriptor).ToArray();
					foreach (var descriptor in PluginOrderResolver.Resolve(active, phase, _hostCapabilities)) {
						gameToken.ThrowIfCancellationRequested();
						var entry = _entered.First(instance => instance.Factory.Descriptor.Id == descriptor.Id);
						if (!Available(entry)) continue;
						try {
							var settings = Configuration.Effective(descriptor, entry.Factory.Schema, identity);
							entry.Settings = settings;
							var context = new PluginGameContext(identity, packagePath, title, controllerId, session, settings, Capabilities, Resources, entry.GameScope, gameToken);
							if (phase == PluginPhase.Prepare) entry.Prepared = true;
							if (phase == PluginPhase.Start) _startOrder.Add(entry);
							await InvokeAsync(entry, phase, token => phase == PluginPhase.Prepare ? entry.Plugin.PrepareGameAsync(context, token) : entry.Plugin.StartGameAsync(token), gameToken).ConfigureAwait(false);
							await Scheduler.Switch();
							if (phase == PluginPhase.Start) SetStatus(descriptor.Id, PluginState.Running, "Running", phase);
						} catch (OperationCanceledException) when (gameToken.IsCancellationRequested) { throw; }
						catch (Exception error) {
							await Scheduler.Switch();
							if (!(error is HookDeadlineException)) Fault(descriptor.Id, phase, error);
							await CleanupAsync(entry, CancellationToken.None).ConfigureAwait(false);
							await BlockDependentsAsync().ConfigureAwait(false); await Scheduler.Switch();
						}
					}
				}
				_running = true;
				RebuildDispatch();
			} catch {
				await Scheduler.Switch();
				await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
				await Scheduler.Switch();
				throw;
			} finally { _operation.Release(); ReplayState(); }
		}

		private IReadOnlyList<PluginDescriptor> Select(GameIdentity game)
		{
			var selected = new List<PluginDescriptor>();
			foreach (var factory in _factories.Values) {
				var descriptor = factory.Descriptor;
				try {
					if (_abandoned.ContainsKey(descriptor.Id)) SetStatus(descriptor.Id, PluginState.Unresponsive, "Waiting for the previous instance to finish; restart the application if it remains blocked.");
					else if (!Configuration.Enabled(descriptor, game)) SetStatus(descriptor.Id, PluginState.Disabled, "Disabled for this game");
					else if (descriptor.Platforms.Count > 0 && !descriptor.Platforms.Contains(Platform)) SetStatus(descriptor.Id, PluginState.Unsupported, "Unavailable on " + Platform);
					else { selected.Add(descriptor); SetStatus(descriptor.Id, PluginState.Available, "Ready"); }
				} catch (Exception error) { Fault(descriptor.Id, PluginPhase.Initialize, error); }
			}
			bool changed;
			do {
				changed = false;
				foreach (var descriptor in selected.ToArray()) {
					string? missing = MissingProvider(descriptor, selected);
					if (missing != null) { selected.Remove(descriptor); SetStatus(descriptor.Id, PluginState.Blocked, "Missing required provider: " + missing); changed = true; }
				}
				foreach (var phase in new[] { PluginPhase.Initialize, PluginPhase.Prepare, PluginPhase.Start, PluginPhase.Ready, PluginPhase.Pause, PluginPhase.Resume, PluginPhase.Focus, PluginPhase.Tick, PluginPhase.Render }) {
					try { PluginOrderResolver.Resolve(selected, phase, _hostCapabilities); }
					catch (PluginOrderException error) {
						foreach (string id in error.Plugins) {
							selected.RemoveAll(descriptor => descriptor.Id == id);
							SetStatus(id, PluginState.Blocked, error.Message, phase);
						}
						changed = true;
					}
				}
			} while (changed);
			return selected;
		}
		private bool Available(Instance entry) => !entry.Cleaned && _statuses[entry.Factory.Descriptor.Id].State != PluginState.Faulted && _statuses[entry.Factory.Descriptor.Id].State != PluginState.Blocked && _statuses[entry.Factory.Descriptor.Id].State != PluginState.Unresponsive;
		private string? MissingProvider(PluginDescriptor descriptor, IEnumerable<PluginDescriptor> candidates)
		{
			var active = candidates.Where(candidate => _statuses[candidate.Id].State != PluginState.Faulted && _statuses[candidate.Id].State != PluginState.Blocked && _statuses[candidate.Id].State != PluginState.Unresponsive).ToArray();
			string? missing = descriptor.DependsOn.FirstOrDefault(id => active.All(candidate => candidate.Id != id));
			return missing ?? descriptor.Requires.FirstOrDefault(id => !_hostCapabilities.Contains(id) && active.All(candidate => !candidate.Provides.Contains(id)));
		}
		private async Task BlockDependentsAsync()
		{
			bool changed;
			do {
				changed = false;
				foreach (var entry in _entered.Where(Available).ToArray()) {
					string? missing = MissingProvider(entry.Factory.Descriptor, _entered.Where(Available).Select(instance => instance.Factory.Descriptor));
					if (missing == null) continue;
					SetStatus(entry.Factory.Descriptor.Id, PluginState.Blocked, "Missing required provider: " + missing);
					await CleanupAsync(entry, CancellationToken.None).ConfigureAwait(false); await Scheduler.Switch(); changed = true;
				}
			} while (changed);
		}
		private bool IsHealthy(Instance entry) => _statuses[entry.Factory.Descriptor.Id].State == PluginState.Running;
		private async Task InvokeAsync(Instance entry, PluginPhase phase, Func<CancellationToken, Task> invoke, CancellationToken cancellation)
		{
			SetStatus(entry.Factory.Descriptor.Id, PluginState.Starting, phase.ToString(), phase);
			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
			deadline.CancelAfter(StartupTimeout);
			try { await AwaitHookAsync(entry, invoke(deadline.Token), deadline.Token).ConfigureAwait(false); }
			catch (HookDeadlineException error) {
				await Scheduler.Switch(); Abandon(entry, error.Hook);
				if (cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation);
				throw;
			}
			await Scheduler.Switch();
		}
		private async Task AwaitHookAsync(Instance entry, Task hook, CancellationToken deadline)
		{
			entry.Track(hook);
			if (await Task.WhenAny(hook, Task.Delay(Timeout.Infinite, deadline)).ConfigureAwait(false) != hook) {
				_ = hook.ContinueWith(task => { var ignored = task.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
				throw new HookDeadlineException(hook);
			}
			await hook.ConfigureAwait(false);
		}
		private void Abandon(Instance entry, Task hook)
		{
			string id = entry.Factory.Descriptor.Id;
			SetStatus(id, PluginState.Unresponsive, "Waiting for an unresponsive plugin; its next instance is blocked.");
			// Stop the plugin now, but keep its scopes (native handles, device leases) until its hook and workers are done.
			Emergency(entry);
			if (entry.Abandoned) return;
			Quarantine(entry, Task.WhenAll(hook, entry.RunningHooks(), Quiescence(entry)), shutdown: true);
		}
		private static Task Quiescence(Instance entry) => (entry.Plugin as IPluginDeferredCleanup)?.Quiescence ?? Task.CompletedTask;
		/// <summary>
		/// Keeps a stopped instance and its scopes (native handles, device leases) until its pending work finishes, however
		/// long that takes, and blocks new instances of the plugin meanwhile. The host itself never waits for it.
		/// </summary>
		private void Quarantine(Instance entry, Task pending, bool shutdown)
		{
			string id = entry.Factory.Descriptor.Id;
			entry.Abandoned = entry.Cleaned = true;
			SetStatus(id, PluginState.Unresponsive, "Waiting for an unresponsive plugin; its next instance is blocked.");
			// Another instance of the same plugin may still be draining; the next one waits for both.
			_abandoned[id] = _abandoned.TryGetValue(id, out var earlier) ? Task.WhenAll(earlier, pending) : pending;
			_ = FinishQuarantineAsync(entry, pending, shutdown);
		}
		private async Task FinishQuarantineAsync(Instance entry, Task pending, bool shutdown)
		{
			try { await pending.ConfigureAwait(false); }
			catch (OperationCanceledException) { /* the plugin observed its deadline */ }
			catch (Exception error) { _error("Abandoned hook", error); }
			await Scheduler.Switch();
			if (shutdown) {
				// This old instance is quarantined independently of the active game's lifecycle gate.
				using var deadline = new CancellationTokenSource(CleanupTimeout);
				try { await entry.Plugin.ShutdownAsync(deadline.Token).ConfigureAwait(false); }
				catch (OperationCanceledException) { /* the deadline passed */ }
				catch (Exception error) { _error("Deferred shutdown", error); }
				await Scheduler.Switch();
			}
			SafeDispose(entry.GameScope); SafeDispose(entry.ApplicationScope); SafeDispose(entry.Plugin);
			string id = entry.Factory.Descriptor.Id;
			if (!_abandoned.TryGetValue(id, out var remaining)) return;
			// Unblock the plugin only when every abandoned instance has drained, not just this one.
			try { await remaining.ConfigureAwait(false); } catch { /* logged by each instance */ }
			await Scheduler.Switch();
			if (!_abandoned.TryGetValue(id, out var current) || current != remaining) return;
			_abandoned.Remove(id);
			if (_statuses[id].State == PluginState.Unresponsive) SetStatus(id, PluginState.Available, "Previous instance stopped.");
		}
		private void SetStatus(string id, PluginState state, string message, PluginPhase? phase = null) { var status = _statuses[id]; status.State = state; status.Message = message; status.Phase = phase; }
		private void Fault(string id, PluginPhase phase, Exception error)
		{
			SetStatus(id, PluginState.Faulted, error.Message, phase);
			// invalid settings are the user's to fix, not a bug in the plugin
			if (error is PluginConfigurationException) _log(id + " / " + phase + ": " + error.Message);
			else _error(id + " / " + phase, error);
		}
		/// <summary>The plugin's settings don't validate, or need a newer plugin.</summary>
		private sealed class PluginConfigurationException : InvalidOperationException
		{
			public PluginConfigurationException(string message) : base(message) { }
		}
		private void SafeDispose(IDisposable resource) { try { resource.Dispose(); } catch (Exception error) { _error("Plugin cleanup", error); } }

		/// <summary>
		/// Stops the current game's plugins and releases their game and instance resources. Safe to call when no game runs.
		/// </summary>
		/// <remarks>
		/// Cancels <see cref="PluginGameContext.Cancellation"/> at once, then waits up to <see cref="CleanupTimeout"/> for a running
		/// operation, such as a startup still in progress, to finish. Plugins are then cleaned up one at a time: partially started
		/// ones first, then started ones in reverse start order. Each gets <see cref="IPlayerPlugin.StopGameAsync"/> (if it was
		/// prepared), disposal of <see cref="PluginGameContext.Lifetime"/>, <see cref="IPlayerPlugin.ShutdownAsync"/>, disposal of
		/// <see cref="PluginApplicationContext.Lifetime"/>, and finally <see cref="IDisposable.Dispose"/>. Exceptions from these
		/// steps are logged and cleanup continues; a hook that misses its deadline quarantines its plugin (see
		/// <see cref="PluginHost"/>) and the host moves on to the next one. If the running operation does not finish in time, the
		/// host falls back to <see cref="EmergencyStop"/>.
		/// </remarks>
		/// <param name="cleanup">
		/// Cuts cleanup short. It cancels the token each cleanup hook receives, in addition to <see cref="CleanupTimeout"/>; if it
		/// fires while the host waits for a running operation, the host falls back to <see cref="EmergencyStop"/>.
		/// </param>
		/// <returns>A task that completes when every plugin of the game is stopped or quarantined.</returns>
		public async Task StopGameAsync(CancellationToken cleanup = default)
		{
			_gameCancellation?.Cancel();
			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cleanup);
			deadline.CancelAfter(CleanupTimeout);
			try { await _operation.WaitAsync(deadline.Token).ConfigureAwait(false); }
			catch (OperationCanceledException) { await Scheduler.Switch(); EmergencyStop(); return; }
			await Scheduler.Switch();
			try { await StopCoreAsync(cleanup).ConfigureAwait(false); await Scheduler.Switch(); }
			finally { _operation.Release(); ReplayState(); }
		}
		private async Task StopCoreAsync(CancellationToken cleanup)
		{
			_running = false; _dispatch.Clear(); _ready = false; _paused = null; _focused = null;
			_gameCancellation?.Cancel();
			// Partially prepared instances unwind first, then started instances in their actual start order.
			var reverse = _entered.Where(entry => !_startOrder.Contains(entry)).Reverse().Concat(_startOrder.AsEnumerable().Reverse()).ToArray();
			foreach (var entry in reverse) {
				await CleanupAsync(entry, cleanup).ConfigureAwait(false); await Scheduler.Switch();
			}
			_startOrder.Clear(); _entered.Clear(); _gameCancellation?.Dispose(); _gameCancellation = null; _activeGame = null;
		}
		/// <summary>
		/// Validates and saves edited settings of one plugin and, when they change <see cref="ConfigurationChange.Live"/> fields of
		/// the running game, applies them to the running plugin first. Both happen inside the host's lifecycle gate, so a commit
		/// never overlaps a game start or stop.
		/// </summary>
		/// <remarks>
		/// <see cref="PluginSettingsSession.ApplyAsync"/> calls this after its own checks; plugins do not call it. The steps are:
		/// <list type="number">
		/// <item><description>
		/// Merge the edited fields into the latest stored layer (global, or the game's profile) and validate the effective result
		/// against the schema. Provider validation and catalog checks are left to the settings session.
		/// </description></item>
		/// <item><description>
		/// If the plugin is running and the edit affects the active game (a global edit, or an edit of that game's profile), apply
		/// the changed live fields through <see cref="IPluginSynchronousReconfiguration.Apply"/> or
		/// <see cref="IPluginReconfiguration.ApplyAsync"/>. A plugin implementing neither, a failed result, or an asynchronous
		/// apply that misses <see cref="StartupTimeout"/> (which quarantines the plugin) ends the commit without saving.
		/// </description></item>
		/// <item><description>
		/// Save only the edited fields with <see cref="PluginConfigurationStore.SavePatch"/>, then raise
		/// <see cref="SettingsCommitted"/>.
		/// </description></item>
		/// </list>
		/// If saving fails after a live apply, the host applies the previous settings again, so plugins must keep reconfiguration
		/// reversible. A rollback that fails faults the plugin.
		/// </remarks>
		/// <param name="factory">The plugin whose settings change; must be one of <see cref="Factories"/>.</param>
		/// <param name="draft">
		/// The edited layer: the game's overrides when <paramref name="game"/> is set, the global values otherwise. Only the fields
		/// in <paramref name="edited"/> are taken; a listed field that the draft lacks is removed from the stored layer and inherits
		/// again. The host copies the draft.
		/// </param>
		/// <param name="game">The game whose profile to change, or null for the global settings.</param>
		/// <param name="cancellation">
		/// Cancels waiting for the gate and the live apply. Once saving has started, it runs to completion.
		/// </param>
		/// <param name="edited">IDs of the top-level fields to commit, or null for every field of the schema.</param>
		/// <returns>
		/// Success with the message <c>Saved.</c>, or a failure whose message lists the validation issues or says why nothing was
		/// saved. Errors and cancellation after the gate was entered are reported as failures, not thrown.
		/// </returns>
		/// <exception cref="ArgumentException"><paramref name="factory"/> is not installed in this host.</exception>
		/// <exception cref="OperationCanceledException"><paramref name="cancellation"/> fired while waiting for the gate.</exception>
		public Task<PluginActionResult> CommitSettingsAsync(IPlayerPluginFactory factory, PluginSettingsSnapshot draft, GameIdentity? game, CancellationToken cancellation, IReadOnlyCollection<string>? edited = null)
			=> CommitSettingsCoreAsync(factory, draft, game, cancellation, edited);
		private async Task CleanupAsync(Instance entry, CancellationToken cleanup)
		{
			if (entry.Cleaned) return;
			entry.Cleaned = true;
			string id = entry.Factory.Descriptor.Id;
			bool keepDiagnostic = _statuses[id].State == PluginState.Faulted || _statuses[id].State == PluginState.Blocked || _statuses[id].State == PluginState.Unresponsive;
			if (!keepDiagnostic) SetStatus(id, PluginState.Stopping, "Stopping", PluginPhase.Stop);
			if (entry.Prepared) {
				using var stopDeadline = CancellationTokenSource.CreateLinkedTokenSource(cleanup);
				stopDeadline.CancelAfter(CleanupTimeout);
				try { await AwaitHookAsync(entry, entry.Plugin.StopGameAsync(stopDeadline.Token), stopDeadline.Token).ConfigureAwait(false); await Scheduler.Switch(); }
				catch (HookDeadlineException error) { await Scheduler.Switch(); Abandon(entry, error.Hook); return; }
				catch (OperationCanceledException) when (stopDeadline.IsCancellationRequested) {
					await Scheduler.Switch(); Abandon(entry, Task.CompletedTask); return;
				}
				catch (Exception error) { await Scheduler.Switch(); _error(id + " / Stop", error); }
			}
			SafeDispose(entry.GameScope);
			using var shutdownDeadline = CancellationTokenSource.CreateLinkedTokenSource(cleanup);
			shutdownDeadline.CancelAfter(CleanupTimeout);
			try { await AwaitHookAsync(entry, entry.Plugin.ShutdownAsync(shutdownDeadline.Token), shutdownDeadline.Token).ConfigureAwait(false); await Scheduler.Switch(); }
			catch (HookDeadlineException error) { await Scheduler.Switch(); Abandon(entry, error.Hook); return; }
			catch (OperationCanceledException) { await Scheduler.Switch(); }
			catch (Exception error) { await Scheduler.Switch(); _error(id + " / Shutdown", error); }
			SafeDispose(entry.ApplicationScope); SafeDispose(entry.Plugin);
			if (!keepDiagnostic) SetStatus(id, PluginState.Available, "Stopped");
		}
		private void RebuildDispatch()
		{
			_dispatch.Clear();
			var active = _entered.Where(IsHealthy).Where(Available).ToDictionary(entry => entry.Factory.Descriptor.Id);
			foreach (var phase in new[] { PluginPhase.Ready, PluginPhase.Pause, PluginPhase.Resume, PluginPhase.Focus, PluginPhase.Tick, PluginPhase.Render })
				_dispatch[phase] = PluginOrderResolver.Resolve(active.Values.Select(entry => entry.Factory.Descriptor), phase, _hostCapabilities).Select(descriptor => active[descriptor.Id]).ToArray();
		}
		private void Dispatch(PluginPhase phase, double delta = 0, bool state = false)
		{
			Scheduler.VerifyOwner();
			bool continuous = phase == PluginPhase.Tick || phase == PluginPhase.Render;
			if (!_running || !continuous && IsTransitioning || !_dispatch.TryGetValue(phase, out var entries)) return;
			bool failed = false;
			foreach (var entry in entries) {
				if (entry.Busy) continue;
				if (!Available(entry)) { failed = true; continue; }
				if (failed && MissingProvider(entry.Factory.Descriptor, _entered.Where(Available).Select(candidate => candidate.Factory.Descriptor)) is string missing) {
					SetStatus(entry.Factory.Descriptor.Id, PluginState.Blocked, "Missing required provider: " + missing); continue;
				}
				try {
					switch (phase) {
						case PluginPhase.Ready: entry.Plugin.GameReady(); break;
						case PluginPhase.Pause: case PluginPhase.Resume: entry.Plugin.PauseChanged(state); break;
						case PluginPhase.Focus: entry.Plugin.FocusChanged(state); break;
						case PluginPhase.Tick: (entry.Plugin as IPluginTick)?.Tick(delta); break;
						case PluginPhase.Render: (entry.Plugin as IPluginRender)?.BeforeRender(); break;
					}
				} catch (Exception error) { Fault(entry.Factory.Descriptor.Id, phase, error); failed = true; }
			}
			if (failed && _faultCleanup.IsCompleted) _faultCleanup = CleanupFaultsAsync();
		}
		private async Task CleanupFaultsAsync()
		{
			await _operation.WaitAsync().ConfigureAwait(false); await Scheduler.Switch();
			try {
				await BlockDependentsAsync().ConfigureAwait(false); await Scheduler.Switch();
				while (_entered.Any(entry => !Available(entry) && !entry.Cleaned))
					foreach (var entry in _entered.Where(entry => !Available(entry) && !entry.Cleaned).Reverse().ToArray()) { await CleanupAsync(entry, CancellationToken.None).ConfigureAwait(false); await Scheduler.Switch(); }
				RebuildDispatch();
			} catch (Exception error) { _error("Plugin recovery", error); }
			finally { _operation.Release(); ReplayState(); }
		}
		private void ReplayState()
		{
				if (_ready) { _ready = false; Dispatch(PluginPhase.Ready); }
				if (_paused.HasValue) { bool paused = _paused.Value; _paused = null; Dispatch(paused ? PluginPhase.Pause : PluginPhase.Resume, state: paused); }
				if (_focused.HasValue) { bool focused = _focused.Value; _focused = null; Dispatch(PluginPhase.Focus, state: focused); }
		}
		/// <summary>
		/// Tells the running plugins that the table's player has started and the game is playable; they receive
		/// <see cref="IPlayerPlugin.GameReady"/> in ready order. Call it on the owner thread. Ignored when no game is running or
		/// starting. While the host is busy (<see cref="IsTransitioning"/>) the call is held and delivered when the operation ends.
		/// The Player calls it from the table player's <c>OnPlayerStarted</c> event.
		/// </summary>
		public void GameReady() { if (!_running && !IsTransitioning) return; _ready = true; if (!IsTransitioning) ReplayState(); }
		/// <summary>
		/// Tells the running plugins that the game was paused or resumed; they receive <see cref="IPlayerPlugin.PauseChanged"/> in
		/// pause or resume order. Call it on the owner thread. Ignored when no game is running or starting. While the host is busy,
		/// only the latest state is delivered when the operation ends. The Player calls it when its in-game menu opens and closes.
		/// </summary>
		/// <param name="paused">True when the game was paused, false when it resumed.</param>
		public void PauseChanged(bool paused) { if (!_running && !IsTransitioning) return; _paused = paused; if (!IsTransitioning) ReplayState(); }
		/// <summary>
		/// Tells the running plugins that the application gained or lost focus; they receive
		/// <see cref="IPlayerPlugin.FocusChanged"/> in focus order. Call it on the owner thread. Ignored when no game is running or
		/// starting. While the host is busy, only the latest state is delivered when the operation ends. The Player calls it from
		/// Unity's <c>OnApplicationFocus</c>.
		/// </summary>
		/// <param name="focused">True when the application has focus.</param>
		public void FocusChanged(bool focused) { if (!_running && !IsTransitioning) return; _focused = focused; if (!IsTransitioning) ReplayState(); }
		/// <summary>
		/// Runs one frame: first runs the work queued on <see cref="Scheduler"/>, such as continuations of the host's own operations
		/// and of plugin hooks, then calls <see cref="IPluginTick.Tick"/> on every running plugin in tick order.
		/// </summary>
		/// <remarks>
		/// Call it on the owner thread every frame, also while the game is paused and while no game runs, since starting, stopping
		/// and committing settings depend on the pump. Plugins keep being ticked while the host commits settings or cleans up a
		/// faulted plugin, but not while a game starts or stops, and a plugin is skipped while its own asynchronous
		/// reconfiguration runs. A plugin whose tick throws becomes <see cref="PluginState.Faulted"/> and is cleaned up in the
		/// background. The Player calls it from its <c>Update</c> with <c>Time.unscaledDeltaTime</c>.
		/// </remarks>
		/// <param name="delta">Real time since the previous frame in seconds, passed on to <see cref="IPluginTick.Tick"/>.</param>
		/// <exception cref="InvalidOperationException">Called from a thread other than the owner thread.</exception>
		public void Tick(double delta) { Scheduler.Pump(); Dispatch(PluginPhase.Tick, delta); }
		/// <summary>
		/// Calls <see cref="IPluginRender.BeforeRender"/> on every running plugin in render order. Call it on the owner thread once
		/// per frame, after all frame updates; the Player calls it from <c>LateUpdate</c>. Like <see cref="Tick"/>, it reaches
		/// plugins while the host commits settings or cleans up a faulted plugin, and a plugin whose call throws is faulted. It
		/// does not pump the scheduler.
		/// </summary>
		/// <exception cref="InvalidOperationException">Called from a thread other than the owner thread.</exception>
		public void BeforeRender() => Dispatch(PluginPhase.Render);
		/// <summary>
		/// Stops all plugins of the current game immediately and synchronously, without awaiting any hook. Use it when there is no
		/// time for <see cref="StopGameAsync"/>: the Player calls it when the application quits, and <see cref="StopGameAsync"/>
		/// falls back to it when a running operation does not finish within <see cref="CleanupTimeout"/>.
		/// </summary>
		/// <remarks>
		/// Cancels <see cref="PluginGameContext.Cancellation"/> and stops all dispatched calls. Then, in reverse order of creation,
		/// it calls <see cref="IPluginEmergencyStop.EmergencyStop"/> on every instance (exceptions are logged) and, unless the
		/// instance was already cleaned up, disposes its <see cref="PluginGameContext.Lifetime"/>,
		/// <see cref="PluginApplicationContext.Lifetime"/> and the plugin right away. <see cref="IPlayerPlugin.StopGameAsync"/> and
		/// <see cref="IPlayerPlugin.ShutdownAsync"/> are not called. An instance that still has a hook running, or whose
		/// <see cref="IPluginDeferredCleanup.Quiescence"/> has not completed, is quarantined instead: it keeps its scopes until both
		/// are done, and the plugin stays <see cref="PluginState.Unresponsive"/>, blocking new instances, until then.
		/// </remarks>
		/// <exception cref="InvalidOperationException">Called from a thread other than the owner thread.</exception>
		public void EmergencyStop()
		{
			Scheduler.VerifyOwner();
			_running = false; _gameCancellation?.Cancel(); _dispatch.Clear();
			foreach (var entry in _entered.AsEnumerable().Reverse()) {
				Emergency(entry);
				if (entry.Cleaned) continue;
				entry.Cleaned = true;
				// Hooks and workers still running keep their resources until they are done; everything else goes now.
				var pending = Task.WhenAll(entry.RunningHooks(), Quiescence(entry));
				if (!pending.IsCompleted) { Quarantine(entry, pending, shutdown: false); continue; }
				SafeDispose(entry.GameScope); SafeDispose(entry.ApplicationScope); SafeDispose(entry.Plugin);
			}
		}
		private void Emergency(Instance entry)
		{
			try { (entry.Plugin as IPluginEmergencyStop)?.EmergencyStop(); } catch (Exception error) { _error("Emergency stop", error); }
		}
	}
}
