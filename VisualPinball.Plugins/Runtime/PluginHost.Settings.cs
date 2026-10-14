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
		/// <summary>
		/// Raised on the owner thread after settings were saved through <see cref="CommitGlobalSettingAsync"/> or
		/// <see cref="CommitSettingsAsync"/>. Arguments: the plugin ID, the IDs of the saved fields, and the game whose
		/// per-game settings changed, or null for global settings.
		/// </summary>
		/// <remarks>
		/// Lets one settings view follow edits made elsewhere: the Player's plugin settings page refreshes the fields the user
		/// has not touched when the volume mixer saves a value. Exceptions thrown by a handler are logged and do not reach other
		/// handlers or the committing caller. Not raised when the commit fails or is cancelled.
		/// </remarks>
		public event Action<string, IReadOnlyCollection<string>, GameIdentity?>? SettingsCommitted;
		/// <summary>
		/// Saves one field's global value, applying it to the running plugin first when the field is
		/// <see cref="ConfigurationChange.Live"/>. This is the write path for fields exposed on shared Player pages through
		/// <see cref="PluginField.GlobalSetting"/>, such as the volume mixer.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The call waits behind any game start, stop or other settings commit instead of failing, so an edit made while a game
		/// is loading is applied once loading finishes rather than discarded. Only <paramref name="fieldId"/> is validated and
		/// written; other saved fields are left as they are.
		/// </para>
		/// <para>
		/// When a game is running, the field is <see cref="ConfigurationChange.Live"/> and its runtime value changes, the host
		/// passes the plugin its current settings with the new value through <see cref="IPluginSynchronousReconfiguration"/> or
		/// <see cref="IPluginReconfiguration"/> and saves only if that succeeds. If the plugin implements neither, the commit
		/// fails and nothing is saved. If the running game overrides the field per game, the global edit is saved but the
		/// runtime value, and so the plugin, stays on the game's value. Other fields take effect on the next game.
		/// </para>
		/// <para>
		/// The Player queues these calls from its volume mixer and sends only the latest value per field.
		/// </para>
		/// </remarks>
		/// <param name="factory">The installed plugin whose setting changes.</param>
		/// <param name="fieldId">ID of a field that declares <see cref="PluginField.GlobalSetting"/> and is not game-only.</param>
		/// <param name="value">The new value, in the field's JSON form.</param>
		/// <param name="cancellation">
		/// Cancels the commit. While waiting for the lifecycle gate it cancels the returned task; afterwards it yields a failed
		/// result and any live change is rolled back. Once saving has started the save completes.
		/// </param>
		/// <returns>
		/// Completes on the owner thread with success and <c>Saved.</c>, or failure and a message to show next to the control
		/// (validation issues, a rejected live change, or <c>Settings change cancelled.</c>).
		/// </returns>
		/// <exception cref="ArgumentException">
		/// Thrown immediately when the field does not exist, is not exposed as a global setting or is game-only; the task faults
		/// with it when <paramref name="factory"/> is not installed in this host.
		/// </exception>
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
								await AwaitHookAsync(entry, hook, deadline.Token).ConfigureAwait(false); await Scheduler.Switch(); result = await hook.ConfigureAwait(false);
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
							await AwaitHookAsync(entry, hook, deadline.Token).ConfigureAwait(false); await Scheduler.Switch(); rollback = await hook.ConfigureAwait(false);
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
