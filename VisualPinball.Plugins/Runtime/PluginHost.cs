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
	/// <summary>Serializes game hooks, resolves policy before constructing plugins, and owns reverse cleanup.</summary>
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
			internal bool Busy;
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
		private CancellationTokenSource? _gameCancellation;
		private bool _running;
		private bool _ready;
		private bool? _paused;
		private bool? _focused;
		private GameIdentity? _activeGame;
		private string[] _hostCapabilities = Array.Empty<string>();
		private readonly Dictionary<PluginPhase, Instance[]> _dispatch = new Dictionary<PluginPhase, Instance[]>();
		private Task _faultCleanup = Task.CompletedTask;
		public OwnerScheduler Scheduler { get; }
		public PluginConfigurationStore Configuration { get; }
		public PluginCapabilities Capabilities { get; }
		public PluginResourceRegistry Resources { get; }
		public string Platform { get; }
		public IReadOnlyDictionary<string, PluginStatus> Statuses => _statuses;
		public IReadOnlyCollection<IPlayerPluginFactory> Factories => _factories.Values;
		public bool IsRunning => _running;
		public bool IsTransitioning => _operation.CurrentCount == 0;
		public TimeSpan CleanupTimeout { get; set; } = TimeSpan.FromSeconds(10);
		public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);
		public PluginHost(IEnumerable<IPlayerPluginFactory> factories, PluginConfigurationStore configuration, OwnerScheduler scheduler,
			PluginCapabilities capabilities, PluginResourceRegistry resources, string platform, Action<string>? log = null)
		{
			_factories = factories.ToDictionary(factory => factory.Descriptor.Id, StringComparer.Ordinal);
			_statuses = _factories.Keys.ToDictionary(id => id, _ => new PluginStatus());
			Configuration = configuration; Scheduler = scheduler; Capabilities = capabilities; Resources = resources; Platform = platform; _log = log ?? (_ => { });
		}

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
						var issues = factory.Schema.Validate(settings).Concat(factory.SettingsProvider?.Validate(new PluginChoiceRequest(identity, settings, Platform)) ?? Array.Empty<PluginValidationIssue>()).ToArray();
						if (issues.Length > 0) throw new InvalidOperationException(string.Join("; ", issues.Select(issue => issue.Field + ": " + issue.Message)));
						if (Configuration.Read(descriptor, factory.Schema).SchemaVersion > factory.Schema.Version || Configuration.Read(descriptor, factory.Schema, identity).SchemaVersion > factory.Schema.Version) throw new InvalidOperationException("Configuration requires a newer plugin version.");
						var plugin = factory.Create(new PluginApplicationContext(descriptor, Configuration, Scheduler, Capabilities, Resources, application, message => _log(descriptor.Id + ": " + message)));
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
			try { await AwaitHookAsync(invoke(deadline.Token), deadline.Token).ConfigureAwait(false); }
			catch (HookDeadlineException error) {
				await Scheduler.Switch(); Abandon(entry, error.Hook);
				if (cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation);
				throw;
			}
			await Scheduler.Switch();
		}
		private async Task AwaitHookAsync(Task hook, CancellationToken deadline)
		{
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
			Emergency(entry);
			entry.Cleaned = true;
			SafeDispose(entry.GameScope); SafeDispose(entry.ApplicationScope);
			if (_abandoned.ContainsKey(id)) return;
			var quiescence = (entry.Plugin as IPluginDeferredCleanup)?.Quiescence ?? Task.CompletedTask;
			var pending = Task.WhenAll(hook, quiescence);
			_abandoned[id] = pending;
			_ = FinishAbandonedAsync(entry, pending);
		}
		private async Task FinishAbandonedAsync(Instance entry, Task pending)
		{
			try { await pending.ConfigureAwait(false); } catch (Exception error) { _log("Abandoned hook: " + error.Message); }
			await Scheduler.Switch();
			// This old instance is quarantined independently of the active game's lifecycle gate.
			using var deadline = new CancellationTokenSource(CleanupTimeout);
			try { await entry.Plugin.ShutdownAsync(deadline.Token).ConfigureAwait(false); }
			catch (Exception error) { _log("Deferred shutdown: " + error.Message); }
			await Scheduler.Switch();
			SafeDispose(entry.Plugin);
			string id = entry.Factory.Descriptor.Id;
			_abandoned.Remove(id);
			if (_statuses[id].State == PluginState.Unresponsive) SetStatus(id, PluginState.Available, "Previous instance stopped.");
		}
		private void SetStatus(string id, PluginState state, string message, PluginPhase? phase = null) { var status = _statuses[id]; status.State = state; status.Message = message; status.Phase = phase; }
		private void Fault(string id, PluginPhase phase, Exception error) { SetStatus(id, PluginState.Faulted, error.Message, phase); _log(id + " / " + phase + ": " + error.Message); }
		private void SafeDispose(IDisposable resource) { try { resource.Dispose(); } catch (Exception error) { _log("Plugin cleanup: " + error.Message); } }

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
		/// <summary>Applies live values and persistence within the same lifecycle gate. Plugins must roll back failed reconfiguration.</summary>
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
				try { await AwaitHookAsync(entry.Plugin.StopGameAsync(stopDeadline.Token), stopDeadline.Token).ConfigureAwait(false); await Scheduler.Switch(); }
				catch (HookDeadlineException error) { await Scheduler.Switch(); Abandon(entry, error.Hook); return; }
				catch (OperationCanceledException) when (stopDeadline.IsCancellationRequested) {
					await Scheduler.Switch(); Abandon(entry, Task.CompletedTask); return;
				}
				catch (Exception error) { await Scheduler.Switch(); _log(id + " / Stop: " + error.Message); }
			}
			SafeDispose(entry.GameScope);
			using var shutdownDeadline = CancellationTokenSource.CreateLinkedTokenSource(cleanup);
			shutdownDeadline.CancelAfter(CleanupTimeout);
			try { await AwaitHookAsync(entry.Plugin.ShutdownAsync(shutdownDeadline.Token), shutdownDeadline.Token).ConfigureAwait(false); await Scheduler.Switch(); }
			catch (HookDeadlineException error) { await Scheduler.Switch(); Abandon(entry, error.Hook); return; }
			catch (Exception error) { await Scheduler.Switch(); _log(id + " / Shutdown: " + error.Message); }
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
			} catch (Exception error) { _log("Plugin recovery: " + error.Message); }
			finally { _operation.Release(); ReplayState(); }
		}
		private void ReplayState()
		{
				if (_ready) { _ready = false; Dispatch(PluginPhase.Ready); }
				if (_paused.HasValue) { bool paused = _paused.Value; _paused = null; Dispatch(paused ? PluginPhase.Pause : PluginPhase.Resume, state: paused); }
				if (_focused.HasValue) { bool focused = _focused.Value; _focused = null; Dispatch(PluginPhase.Focus, state: focused); }
		}
		public void GameReady() { if (!_running && !IsTransitioning) return; _ready = true; if (!IsTransitioning) ReplayState(); }
		public void PauseChanged(bool paused) { if (!_running && !IsTransitioning) return; _paused = paused; if (!IsTransitioning) ReplayState(); }
		public void FocusChanged(bool focused) { if (!_running && !IsTransitioning) return; _focused = focused; if (!IsTransitioning) ReplayState(); }
		public void Tick(double delta) { Scheduler.Pump(); Dispatch(PluginPhase.Tick, delta); }
		public void BeforeRender() => Dispatch(PluginPhase.Render);
		public void EmergencyStop()
		{
			Scheduler.VerifyOwner();
			_running = false; _gameCancellation?.Cancel(); _dispatch.Clear();
			foreach (var entry in _entered.AsEnumerable().Reverse()) {
				Emergency(entry);
				if (entry.Cleaned) continue;
				entry.Cleaned = true;
				SafeDispose(entry.GameScope); SafeDispose(entry.ApplicationScope); SafeDispose(entry.Plugin);
			}
		}
		private void Emergency(Instance entry)
		{
			try { (entry.Plugin as IPluginEmergencyStop)?.EmergencyStop(); } catch (Exception error) { _log(error.Message); }
		}
	}
}
