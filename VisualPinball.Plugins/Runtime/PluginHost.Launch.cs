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
	public sealed partial class PluginHost
	{
		private readonly Dictionary<string, Task> _pendingLaunchChecks = new Dictionary<string, Task>();

		/// <summary>Checks required plugins without loading the table, starting plugins, or stopping a running game.
		/// Disabled plugins are skipped. Errors and timeouts block launch; cancellation propagates to the caller.</summary>
		public async Task<IReadOnlyList<PluginLaunchBlocker>> CheckLaunchAsync(GameIdentity game, string packagePath, string title, CancellationToken cancellation)
		{
			await Scheduler.Switch();
			cancellation.ThrowIfCancellationRequested();
			var candidates = LaunchCheckCandidates(game);
			if (candidates.Count == 0) return Array.Empty<PluginLaunchBlocker>();
			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
			deadline.CancelAfter(StartupTimeout);
			try { await _operation.WaitAsync(deadline.Token).ConfigureAwait(false); }
			catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) {
				await Scheduler.Switch();
				cancellation.ThrowIfCancellationRequested();
				return candidates.Select(factory => new PluginLaunchBlocker(factory.Descriptor, new[] {
					new PluginValidationIssue("", "Plugin settings are still being saved or a previous game is stopping. Try launching again.")
				}, game)).ToArray();
			}
			await Scheduler.Switch();
			try { return await CheckLaunchCoreAsync(game, packagePath, title, cancellation).ConfigureAwait(false); }
			finally { await Scheduler.Switch(); _operation.Release(); ReplayState(); }
		}

		private IReadOnlyList<IPlayerPluginFactory> LaunchCheckCandidates(GameIdentity game)
		{
			var candidates = new List<IPlayerPluginFactory>();
			foreach (var factory in _factories.Values) {
				if (factory is not IPluginLaunchCheck check) continue;
				try { if (!Configuration.Enabled(factory.Descriptor, game) || !check.AppliesTo(game)) continue; }
				catch { /* A broken predicate or settings read must become a blocker in the guarded check. */ }
				candidates.Add(factory);
			}
			return candidates;
		}

		private async Task<IReadOnlyList<PluginLaunchBlocker>> CheckLaunchCoreAsync(GameIdentity game, string packagePath, string title, CancellationToken cancellation)
		{
			var blockers = new List<PluginLaunchBlocker>();
			foreach (var factory in _factories.Values) {
				await Scheduler.Switch();
				cancellation.ThrowIfCancellationRequested();
				if (factory is not IPluginLaunchCheck check) continue;
				var descriptor = factory.Descriptor;
				var issues = new List<PluginValidationIssue>();
				try {
					if (!Configuration.Enabled(descriptor, game) || !check.AppliesTo(game)) continue;
					if (_abandoned.ContainsKey(descriptor.Id)) throw new InvalidOperationException("The previous plugin instance is still stopping. Wait or restart the Player.");
					if (descriptor.Platforms.Count > 0 && !descriptor.Platforms.Contains(Platform)) throw new InvalidOperationException("This game's required plugin is unavailable on " + Platform + ".");
					if (_pendingLaunchChecks.TryGetValue(descriptor.Id, out var pending) && !pending.IsCompleted) throw new InvalidOperationException("The previous launch check is still finishing. Try again shortly.");
					if (Configuration.Read(descriptor, factory.Schema).SchemaVersion > factory.Schema.Version || Configuration.Read(descriptor, factory.Schema, game).SchemaVersion > factory.Schema.Version) throw new PluginConfigurationException("Configuration requires a newer plugin version.");
					var settings = Configuration.Effective(descriptor, factory.Schema, game);
					var package = new PluginPackageReference(packagePath, title);
					issues.AddRange(factory.Schema.Validate(settings));
					issues.AddRange(factory.SettingsProvider?.Validate(new PluginChoiceRequest(game, settings, Platform) { Package = package }) ?? Array.Empty<PluginValidationIssue>());
					if (issues.Count == 0) {
						using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
						deadline.CancelAfter(StartupTimeout);
						var hook = check.CheckLaunchAsync(new PluginLaunchContext(game, package, settings, Platform), deadline.Token);
						_pendingLaunchChecks[descriptor.Id] = hook;
						if (await Task.WhenAny(hook, Task.Delay(Timeout.Infinite, deadline.Token)).ConfigureAwait(false) != hook) {
							_ = hook.ContinueWith(done => { var ignored = done.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
							cancellation.ThrowIfCancellationRequested();
							throw new TimeoutException("The launch check did not finish in time. Try again after it stops.");
						}
						issues.AddRange(await hook.ConfigureAwait(false));
					}
				} catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
				catch (OperationCanceledException) { issues.Add(new PluginValidationIssue("", "The launch check did not finish in time. Try again after it stops.")); }
				catch (Exception error) { issues.Add(new PluginValidationIssue("", error.Message)); }
				await Scheduler.Switch();
				cancellation.ThrowIfCancellationRequested();
				if (issues.Count > 0) blockers.Add(new PluginLaunchBlocker(descriptor, issues, game));
			}
			return blockers;
		}

		private void RequireLaunchPluginsStarted(GameIdentity game)
		{
			var blockers = new List<PluginLaunchBlocker>();
			foreach (var factory in _factories.Values) {
				if (factory is IPluginLaunchCheck check && Configuration.Enabled(factory.Descriptor, game) && check.AppliesTo(game)
					&& _statuses[factory.Descriptor.Id].State != PluginState.Running)
					blockers.Add(new PluginLaunchBlocker(factory.Descriptor, new[] { new PluginValidationIssue("", _statuses[factory.Descriptor.Id].Message) }, game));
			}
			if (blockers.Count > 0) throw new PluginLaunchBlockedException(blockers);
		}
	}
}
