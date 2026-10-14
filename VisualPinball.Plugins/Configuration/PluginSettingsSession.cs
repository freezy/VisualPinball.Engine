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
	/// An editable, unsaved copy of one plugin's settings in one scope, either the global settings or one game's profile. Edit the
	/// draft, preview the effective values, query catalogs and run actions, then apply.
	/// </summary>
	/// <remarks>
	/// The session is detached from the store: edits stay in <see cref="Draft"/> until <see cref="ApplyAsync"/> saves them, and
	/// nothing reaches a running plugin before that. A game session's draft holds only that game's overrides; fields absent from it
	/// inherit the global value. Applying saves only the fields edited in this session, so values changed elsewhere in the
	/// meantime, such as by the volume mixer, are kept. Before saving, apply checks again that every selected catalog item, such as
	/// a PinUP pack or a B2S backglass file, still exists and is available.
	/// <para>
	/// The Player's plugin settings page opens one session for the selected plugin and scope, disposes it when the user switches
	/// plugin or scope or leaves the page, and applies through its <see cref="PluginHost"/> so live settings reach the running
	/// game. A session is not thread-safe: use it from one thread, the owner thread in the Player. Only <see cref="ApplyAsync"/>
	/// guards against overlapping calls.
	/// </para>
	/// <example>
	/// <code>
	/// using var session = new PluginSettingsSession(factory, host.Configuration, host.Platform, game, host.Resources.Snapshot);
	/// session.Package = new PluginPackageReference(packagePath, title);
	/// session.Set("Volume", 80);
	/// var result = await session.ApplyAsync(cancellation, host);
	/// ShowFeedback(result.Message);
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginSettingsSession : IDisposable
	{
		private readonly IPlayerPluginFactory _factory;
		private readonly PluginConfigurationStore _store;
		private readonly string _platform;
		private readonly Func<IReadOnlyList<PluginResourceDescriptor>> _resources;
		private readonly SemaphoreSlim _apply = new SemaphoreSlim(1, 1);
		private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
		private PluginSettingsSnapshot _baseline;
		private readonly HashSet<string> _resetFields = new HashSet<string>(StringComparer.Ordinal);
		/// <summary>
		/// Whether a field has a pending change in this session: its draft value differs from the value loaded from, or last saved
		/// to, the store, or <see cref="Reset"/> cleared it. Only pending fields are saved by <see cref="ApplyAsync"/>.
		/// </summary>
		/// <param name="field">The top-level field ID.</param>
		public bool HasEdits(string field) => _resetFields.Contains(field) || !JToken.DeepEquals(_baseline.Value(field), Draft.Value(field));
		private string[] ChangedFields() => _factory.Schema.Fields.Where(field => HasEdits(field.Id)).Select(field => field.Id).ToArray();
		/// <summary>The game whose profile this session edits, or null when it edits the global settings. Fixed for the session.</summary>
		public GameIdentity? Game { get; }
		/// <summary>
		/// The table package being configured, passed to catalogs, actions and validation as <see cref="PluginChoiceRequest.Package"/>.
		/// Null by default and for global sessions. The Player sets it for game sessions, so that the B2S plugin, for example, can
		/// look for a backglass next to the package.
		/// </summary>
		public PluginPackageReference? Package { get; set; }
		/// <summary>
		/// The edited layer as it would be stored: only the game's overrides in a game session, the global values in a global
		/// session. A field absent from it inherits the global value or the default. <see cref="Revert"/>, <see cref="Reset"/> and
		/// a successful <see cref="ApplyAsync"/> replace the instance, so read the property again rather than keeping the snapshot.
		/// Change values through <see cref="Set"/>, which also enforces each field's scope.
		/// </summary>
		public PluginSettingsSnapshot Draft { get; private set; }
		/// <summary>
		/// The values the plugin would run with if the draft were saved: schema defaults, then the saved global values and the draft
		/// in a game session, or the draft in a global session. Computed as a new snapshot on every read; in a game session that
		/// includes reading the global settings file, so avoid reading it in a loop.
		/// </summary>
		public PluginSettingsSnapshot Effective => Game.HasValue
			? PluginConfigurationStore.Resolve(_factory.Schema, _store.Read(_factory.Descriptor, _factory.Schema), Draft)
			: PluginConfigurationStore.Resolve(_factory.Schema, Draft);
		/// <summary>
		/// Opens a session and loads the stored layer of the given scope as the draft. The file is read synchronously on the calling
		/// thread.
		/// </summary>
		/// <param name="factory">The plugin whose settings to edit.</param>
		/// <param name="store">
		/// The settings store to read from, and to save to when <see cref="ApplyAsync"/> runs without a host. The Player passes
		/// <see cref="PluginHost.Configuration"/>.
		/// </param>
		/// <param name="platform">The platform ID passed to settings providers; the Player passes <see cref="PluginHost.Platform"/>.</param>
		/// <param name="game">The game whose profile to edit, or null for the global settings.</param>
		/// <param name="resources">
		/// Returns the currently published resources, for catalog lookups and the availability check on apply. The Player passes
		/// <see cref="PluginResourceRegistry.Snapshot()"/>. Null means no resources.
		/// </param>
		/// <exception cref="System.IO.InvalidDataException">The stored game profile belongs to another game.</exception>
		public PluginSettingsSession(IPlayerPluginFactory factory, PluginConfigurationStore store, string platform, GameIdentity? game = null, Func<IReadOnlyList<PluginResourceDescriptor>>? resources = null)
		{ _factory = factory; _store = store; _platform = platform; Game = game; _resources = resources ?? (() => Array.Empty<PluginResourceDescriptor>()); Draft = store.Read(factory.Descriptor, factory.Schema, game); _baseline = Draft.Copy(); }
		/// <summary>Changes a field in the draft and marks it as edited. Nothing is saved until <see cref="ApplyAsync"/>.</summary>
		/// <param name="fieldId">The ID of a top-level field of the plugin's schema.</param>
		/// <param name="value">
		/// The new value, copied into the draft. Null removes the field from the draft: in a game session it inherits the global
		/// value again, in a global session it falls back to the default.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// The schema has no field with that ID, or the field's <see cref="PluginField.Scope"/> does not allow editing it in this
		/// session's scope.
		/// </exception>
		public void Set(string fieldId, JToken? value)
		{
			var field = _factory.Schema.Fields.First(field => field.Id == fieldId);
			if (Game.HasValue ? field.Scope == ConfigurationScope.GlobalOnly : field.Scope == ConfigurationScope.GameOnly) throw new InvalidOperationException("This field is not configurable in the selected scope.");
			Draft.Set(fieldId, value);
			_resetFields.Remove(fieldId);
		}
		/// <summary>
		/// Discards every pending edit and reset by reloading the stored layer from the store; the file is read synchronously. The
		/// Player's Revert button calls it.
		/// </summary>
		public void Revert() { Draft = _store.Read(_factory.Descriptor, _factory.Schema, Game); _baseline = Draft.Copy(); _resetFields.Clear(); }
		/// <summary>
		/// Takes over values that were saved elsewhere while the session is open, for fields without pending edits, so the page shows
		/// them without discarding the user's own edits. The Player calls it from <see cref="PluginHost.SettingsCommitted"/>, for
		/// example when the volume mixer changed a global setting that the open settings page also shows.
		/// </summary>
		/// <param name="saved">The freshly read stored layer of this session's scope.</param>
		/// <param name="fields">IDs of the top-level fields that were saved. Fields with pending edits are skipped.</param>
		public void RefreshUnedited(PluginSettingsSnapshot saved, IEnumerable<string> fields)
		{
			foreach (string id in fields) {
				if (HasEdits(id)) continue;
				Draft.Set(id, saved.Value(id)); _baseline.Set(id, saved.Value(id));
			}
		}
		/// <summary>
		/// Empties the draft, so every field goes back to its inherited value (the global value in a game session, the default in a
		/// global session), and marks every field editable in this scope as pending, so <see cref="ApplyAsync"/> removes their stored
		/// values. Nothing is saved until then. The Player's Reset button calls it.
		/// </summary>
		public void Reset()
		{
			Draft = new PluginSettingsSnapshot(_factory.Schema.Version);
			foreach (var field in _factory.Schema.Fields)
				if (Game.HasValue ? field.Scope != ConfigurationScope.GlobalOnly : field.Scope != ConfigurationScope.GameOnly) _resetFields.Add(field.Id);
		}
		/// <summary>
		/// Checks <see cref="Effective"/> against the schema and the plugin's <see cref="IPluginSettingsProvider.Validate"/>. Catalog
		/// selections are not checked here; <see cref="ApplyAsync"/> checks them in addition.
		/// </summary>
		/// <returns>The issues found; empty when the settings are valid.</returns>
		public IReadOnlyList<PluginValidationIssue> Validate()
		{
			var effective = Effective;
			return _factory.Schema.Validate(effective).Concat(_factory.SettingsProvider?.Validate(Request(effective)) ?? Array.Empty<PluginValidationIssue>()).ToArray();
		}
		private PluginChoiceRequest Request(PluginSettingsSnapshot settings, IReadOnlyList<PluginResourceDescriptor>? resources = null)
			=> new PluginChoiceRequest(Game, settings, _platform, resources) { Package = Package };
		/// <summary>
		/// Lists the items a choice or screen layout field can select, through the plugin's
		/// <see cref="IPluginSettingsProvider.DiscoverAsync"/>, based on the draft's effective values, <see cref="Package"/> and the
		/// currently published resources. The task may complete on a worker thread.
		/// </summary>
		/// <param name="provider">The field's <see cref="PluginField.Provider"/>.</param>
		/// <param name="cancellation">Passed to the provider; cancel it when the field is no longer shown.</param>
		/// <returns>The provider's catalog, or an empty, complete catalog when the plugin has no settings provider.</returns>
		public Task<PluginChoiceCatalog> DiscoverAsync(string provider, CancellationToken cancellation)
			=> _factory.SettingsProvider?.DiscoverAsync(provider, Request(Effective, _resources()), cancellation) ?? Task.FromResult(new PluginChoiceCatalog("empty", Array.Empty<PluginChoice>()));
		/// <summary>
		/// Runs the action of an action field through <see cref="IPluginSettingsProvider.RunActionAsync"/>, such as PinUP locating
		/// LibVLC. When it succeeds and returns <see cref="PluginActionResult.Changes"/>, the values of the fields editable in this
		/// scope are put into the draft as pending edits; the user still has to apply them.
		/// </summary>
		/// <param name="action">The field's <see cref="PluginField.Provider"/>.</param>
		/// <param name="cancellation">Passed to the provider.</param>
		/// <returns>The provider's result, or a failure when the plugin has no settings provider.</returns>
		/// <exception cref="OperationCanceledException">
		/// <paramref name="cancellation"/> was cancelled; the draft is then left unchanged.
		/// </exception>
		public async Task<PluginActionResult> RunActionAsync(string action, CancellationToken cancellation)
		{
			if (_factory.SettingsProvider == null) return new PluginActionResult(false, "This action is unavailable.");
			var result = await _factory.SettingsProvider.RunActionAsync(action, Request(Effective), cancellation);
			cancellation.ThrowIfCancellationRequested();
			if (result.Success && result.Changes != null) foreach (var field in _factory.Schema.Fields) {
				if (Game.HasValue ? field.Scope == ConfigurationScope.GlobalOnly : field.Scope == ConfigurationScope.GameOnly) continue;
				if (result.Changes.Values.TryGetValue(field.Id, out var value)) Set(field.Id, value);
			}
			return result;
		}
		/// <summary>
		/// Validates the pending edits and saves them. Only fields with pending edits (see <see cref="HasEdits"/>) are written; other
		/// stored values, including ones changed elsewhere since the session opened, are kept.
		/// </summary>
		/// <remarks>
		/// The draft is frozen when the call starts. Apply merges it into the latest stored layer, validates the effective result
		/// against the schema and <see cref="IPluginSettingsProvider.Validate"/>, and asks the provider for a fresh catalog for every
		/// visible choice or screen layout field, including those inside composite and collection records, to check that each
		/// selected item still exists and is available. A selection missing from an incomplete catalog is accepted. Any issue fails
		/// the apply and nothing is saved.
		/// <para>
		/// With a <paramref name="host"/>, the edits are committed through <see cref="PluginHost.CommitSettingsAsync"/>, which also
		/// applies live fields to a running plugin and raises <see cref="PluginHost.SettingsCommitted"/>. Without one they are saved
		/// straight to the store and take effect with the next game. After a successful save the draft is reloaded from the store;
		/// edits the user made while the apply ran stay pending. Calls are serialized: a second apply waits for the first.
		/// </para>
		/// </remarks>
		/// <param name="cancellation">Cancels the apply. <see cref="Dispose"/> cancels it too.</param>
		/// <param name="host">
		/// The host to commit through, so that a running game picks up live settings. The Player always passes it. When given, the
		/// task completes on the host's owner thread.
		/// </param>
		/// <returns>
		/// Success, or a failure whose message says why nothing was saved. Validation issues are listed one per line as
		/// <c>field: message</c>; a selection inside a record is named by its path, such as <c>Items[0].Pack</c>.
		/// </returns>
		/// <exception cref="OperationCanceledException">
		/// The apply was cancelled, or the session disposed, before the settings were committed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The stored settings were written by a newer plugin version and cannot be overwritten.
		/// </exception>
		public async Task<PluginActionResult> ApplyAsync(CancellationToken cancellation = default, PluginHost? host = null)
		{
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
			await _apply.WaitAsync(linked.Token).ConfigureAwait(false);
			try {
				// Freeze the draft before any asynchronous discovery so editing cannot change the validated transaction.
				var draft = Draft.Copy();
				var frozenResets = new HashSet<string>(_resetFields, StringComparer.Ordinal);
				var changed = ChangedFields();
				var resources = _resources();
				var effective = await Task.Run(() => {
					var latest = _store.MergePatch(_factory.Descriptor, _factory.Schema, draft, changed, Game);
					return Game.HasValue ? PluginConfigurationStore.Resolve(_factory.Schema, _store.Read(_factory.Descriptor, _factory.Schema), latest) : PluginConfigurationStore.Resolve(_factory.Schema, latest);
				}, linked.Token).ConfigureAwait(false);
				if (host != null) await host.Scheduler.Switch();
				var request = Request(effective, resources);
				var issues = _factory.Schema.Validate(effective).Concat(_factory.SettingsProvider?.Validate(request) ?? Array.Empty<PluginValidationIssue>()).ToList();
				var catalogs = new Dictionary<string, PluginChoiceCatalog>(StringComparer.Ordinal);
				foreach (var binding in ChoiceBindings(_factory.Schema.Fields, effective)) {
					var field = binding.Field;
					var selections = field.Kind == SettingKind.ScreenLayout ? (binding.Value as JArray)?.OfType<JObject>().Select(screen => screen["screen"]?.ToString() ?? "").ToArray() ?? Array.Empty<string>() : new[] { binding.Value?.ToString() ?? "" };
					if (selections.Length == 0 || _factory.SettingsProvider == null) continue;
					if (!catalogs.TryGetValue(field.Provider, out var catalog)) {
						catalog = await _factory.SettingsProvider.DiscoverAsync(field.Provider, request, linked.Token).ConfigureAwait(false);
						catalogs.Add(field.Provider, catalog);
						}
					foreach (string selected in selections) {
						if (selected.Length == 0) continue;
						var choice = catalog.Find(selected);
						if (choice != null && !choice.Available || choice == null && catalog.IsComplete) issues.Add(new PluginValidationIssue(binding.Path, choice?.UnavailableReason ?? "Selected item is missing: " + selected));
						}
				}
				linked.Token.ThrowIfCancellationRequested();
				if (issues.Count > 0) return new PluginActionResult(false, string.Join("\n", issues.Select(issue => issue.Field + ": " + issue.Message)));
				if (host != null) {
					var result = await host.CommitSettingsAsync(_factory, draft, Game, linked.Token, changed).ConfigureAwait(false);
					await host.Scheduler.Switch();
					if (result.Success) Rebase(draft, frozenResets);
					return result;
				}
				await Task.Run(() => _store.SavePatch(_factory.Descriptor, _factory.Schema, draft, changed, Game)).ConfigureAwait(false);
				Rebase(draft, frozenResets);
				return new PluginActionResult(true, "Saved. Changes take effect on the next game.");
			} finally { _apply.Release(); }
		}
		/// <summary>
		/// Reloads the saved profile after a successful apply, then re-applies fields edited after <paramref name="frozen"/>
		/// was taken, so changes made while the apply ran stay pending instead of being discarded.
		/// </summary>
		private void Rebase(PluginSettingsSnapshot frozen, HashSet<string> frozenResets)
		{
			var current = Draft;
			var currentResets = new HashSet<string>(_resetFields, StringComparer.Ordinal);
			Revert();
			foreach (var field in _factory.Schema.Fields) {
				string id = field.Id;
				bool reset = currentResets.Contains(id);
				if (JToken.DeepEquals(frozen.Value(id), current.Value(id)) && reset == frozenResets.Contains(id)) continue;
				Draft.Set(id, current.Value(id));
				if (reset) _resetFields.Add(id);
			}
		}
		private static IEnumerable<(PluginField Field, JToken? Value, string Path)> ChoiceBindings(IEnumerable<PluginField> fields, PluginSettingsSnapshot snapshot, string prefix = "")
		{
			foreach (var field in fields) {
				if (!field.Visible(snapshot)) continue;
				var value = snapshot.Value(field.Id); string path = prefix + field.Id;
				if ((field.Kind == SettingKind.Choice || field.Kind == SettingKind.ScreenLayout) && field.Provider.Length > 0) yield return (field, value, path);
				if (field.Children.Count == 0) continue;
				if (field.Kind == SettingKind.Composite && value is JObject record)
					foreach (var child in ChoiceBindings(field.Children, new PluginSettingsSnapshot(snapshot.SchemaVersion, record), path + ".")) yield return child;
				if (field.Kind == SettingKind.Collection && value is JArray records) for (int index = 0; index < records.Count; index++) {
					if (!(records[index] is JObject item)) continue;
					foreach (var child in ChoiceBindings(field.Children, new PluginSettingsSnapshot(snapshot.SchemaVersion, item), path + "[" + index + "].")) yield return child;
					}
			}
		}
		/// <summary>
		/// Cancels a running <see cref="ApplyAsync"/> and makes later calls to it fail with <see cref="OperationCanceledException"/>.
		/// An apply that has already started saving still completes, and the other members keep working. The Player disposes the
		/// session when the user switches plugin or scope or leaves the settings page.
		/// </summary>
		public void Dispose() { _lifetime.Cancel(); }
	}
}
