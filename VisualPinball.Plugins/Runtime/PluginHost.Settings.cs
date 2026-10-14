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
	public sealed partial class PluginHost
	{
		public event Action<string, IReadOnlyCollection<string>, GameIdentity?>? SettingsCommitted;
		/// <summary>Edits one canonical global field. Waiting for game startup does not discard a mixer edit.</summary>
		public Task<PluginActionResult> CommitGlobalSettingAsync(IPlayerPluginFactory factory, string fieldId, JToken value, CancellationToken cancellation = default)
		{
			var field = factory.Schema.Fields.FirstOrDefault(item => item.Id == fieldId);
			if (field?.GlobalSetting == null || field.Scope == ConfigurationScope.GameOnly) throw new ArgumentException("The field is not exposed as a global setting.");
			var patch = new PluginSettingsSnapshot(factory.Schema.Version); patch.Set(fieldId, value);
			return CommitSettingsCoreAsync(factory, patch, null, cancellation, new[] { fieldId }, field);
		}
		private async Task<PluginActionResult> CommitSettingsCoreAsync(IPlayerPluginFactory factory, PluginSettingsSnapshot draft, GameIdentity? game, CancellationToken cancellation, IReadOnlyCollection<string>? edited, PluginField? single = null)
		{
			if (!_factories.TryGetValue(factory.Descriptor.Id, out var installed) || installed != factory) throw new ArgumentException("The factory is not installed in this host.");
			var patch = draft.Copy(); var fields = (edited ?? factory.Schema.Fields.Select(field => field.Id).ToArray()).ToArray();
			await _operation.WaitAsync(cancellation).ConfigureAwait(false);
			await Scheduler.Switch();
			Instance? entry = null;
			PluginSettingsSnapshot? previous = null;
			IPluginSynchronousReconfiguration? synchronous = null;
			IPluginReconfiguration? asynchronous = null;
			bool applied = false;
			try {
				entry = _entered.FirstOrDefault(candidate => candidate.Factory == factory && IsHealthy(candidate) && Available(candidate));
				GameIdentity? active = _activeGame;
				var prepared = await Task.Run(() => {
					var latest = Configuration.MergePatch(factory.Descriptor, factory.Schema, patch, fields, game);
					var global = game.HasValue ? Configuration.Read(factory.Descriptor, factory.Schema) : latest;
					var effective = game.HasValue ? PluginConfigurationStore.Resolve(factory.Schema, global, latest) : PluginConfigurationStore.Resolve(factory.Schema, global);
					var runtime = active.HasValue && (!game.HasValue || game.Value.Equals(active.Value))
						? PluginConfigurationStore.Resolve(factory.Schema, global, game.HasValue ? latest : Configuration.Read(factory.Descriptor, factory.Schema, active)) : effective;
					return (Effective: effective, Runtime: runtime);
				}, cancellation).ConfigureAwait(false);
				await Scheduler.Switch(); cancellation.ThrowIfCancellationRequested();
				var validation = single == null ? factory.Schema : new PluginSettingsSchema(factory.Schema.Version, new PluginSettingsGroup("", "", single));
				var issues = validation.Validate(prepared.Effective);
				if (issues.Count > 0) return new PluginActionResult(false, string.Join("\n", issues.Select(issue => issue.Field + ": " + issue.Message)));
				if (single != null && !single.Visible(prepared.Effective)) return new PluginActionResult(false, "This setting is currently unavailable.");
				previous = entry?.Settings;
				PluginSettingsSnapshot? live = null;
				if (entry != null && previous != null && active.HasValue && (!game.HasValue || game.Value.Equals(active.Value))) {
					var changed = factory.Schema.Fields.Where(field => fields.Contains(field.Id) && field.Change == ConfigurationChange.Live && !JToken.DeepEquals(previous.Value(field.Id), prepared.Runtime.Value(field.Id))).ToArray();
					if (changed.Length > 0) {
						live = previous.Copy(); foreach (var field in changed) live.Set(field.Id, prepared.Runtime.Value(field.Id));
						synchronous = entry.Plugin as IPluginSynchronousReconfiguration;
						asynchronous = entry.Plugin as IPluginReconfiguration;
						PluginActionResult result;
						if (synchronous != null) result = synchronous.Apply(live);
						else if (asynchronous != null) {
							entry.Busy = true;
							try {
								using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(StartupTimeout);
								var hook = asynchronous.ApplyAsync(live, deadline.Token);
								await AwaitHookAsync(hook, deadline.Token).ConfigureAwait(false); await Scheduler.Switch(); result = await hook.ConfigureAwait(false);
							} catch (HookDeadlineException error) { await Scheduler.Switch(); Abandon(entry, error.Hook); return new PluginActionResult(false, "Live configuration did not finish; settings were not saved."); }
							finally { await Scheduler.Switch(); entry.Busy = false; }
						} else return new PluginActionResult(false, "The plugin does not implement live configuration.");
						if (!result.Success) return result;
						applied = true; entry.Settings = live;
					}
				}
				cancellation.ThrowIfCancellationRequested();
				// Once persistence starts, join it. Cancelling the waiter could otherwise allow a late write after rollback.
				await Task.Run(() => Configuration.SavePatch(factory.Descriptor, factory.Schema, patch, fields, game)).ConfigureAwait(false);
				await Scheduler.Switch();
				NotifySettings(factory.Descriptor.Id, fields, game);
				return new PluginActionResult(true, "Saved.");
			} catch (Exception error) {
				await Scheduler.Switch();
				if (applied && previous != null && entry != null && !entry.Cleaned) {
					try {
						PluginActionResult rollback;
						if (synchronous != null) rollback = synchronous.Apply(previous);
						else {
							entry.Busy = true; using var deadline = new CancellationTokenSource(StartupTimeout);
							var hook = asynchronous!.ApplyAsync(previous, deadline.Token);
							await AwaitHookAsync(hook, deadline.Token).ConfigureAwait(false); await Scheduler.Switch(); rollback = await hook.ConfigureAwait(false);
						}
						if (!rollback.Success) Fault(factory.Descriptor.Id, PluginPhase.Prepare, new InvalidOperationException("Runtime settings rollback failed."));
						else entry.Settings = previous;
					} catch (HookDeadlineException failure) { Abandon(entry, failure.Hook); }
					catch (Exception failure) { Fault(factory.Descriptor.Id, PluginPhase.Prepare, failure); }
					finally { await Scheduler.Switch(); entry.Busy = false; }
				}
				return new PluginActionResult(false, error is OperationCanceledException ? "Settings change cancelled." : error.Message);
			} finally { _operation.Release(); ReplayState(); }
		}
		private void NotifySettings(string id, IReadOnlyCollection<string> fields, GameIdentity? game)
		{
			if (SettingsCommitted == null) return;
			foreach (Action<string, IReadOnlyCollection<string>, GameIdentity?> listener in SettingsCommitted.GetInvocationList())
				try { listener(id, fields, game); } catch (Exception error) { _log("Settings observer: " + error.Message); }
		}
	}
}
