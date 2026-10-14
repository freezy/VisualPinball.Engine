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
	/// <summary>A detached draft. Game drafts contain only overrides, and Apply rechecks dynamic catalogs.</summary>
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
		public bool HasEdits(string field) => _resetFields.Contains(field) || !JToken.DeepEquals(_baseline.Value(field), Draft.Value(field));
		private string[] ChangedFields() => _factory.Schema.Fields.Where(field => HasEdits(field.Id)).Select(field => field.Id).ToArray();
		public GameIdentity? Game { get; }
		/// <summary>The table package being configured, passed to catalogs, actions and validation.</summary>
		public PluginPackageReference? Package { get; set; }
		public PluginSettingsSnapshot Draft { get; private set; }
		public PluginSettingsSnapshot Effective => Game.HasValue
			? PluginConfigurationStore.Resolve(_factory.Schema, _store.Read(_factory.Descriptor, _factory.Schema), Draft)
			: PluginConfigurationStore.Resolve(_factory.Schema, Draft);
		public PluginSettingsSession(IPlayerPluginFactory factory, PluginConfigurationStore store, string platform, GameIdentity? game = null, Func<IReadOnlyList<PluginResourceDescriptor>>? resources = null)
		{ _factory = factory; _store = store; _platform = platform; Game = game; _resources = resources ?? (() => Array.Empty<PluginResourceDescriptor>()); Draft = store.Read(factory.Descriptor, factory.Schema, game); _baseline = Draft.Copy(); }
		public void Set(string fieldId, JToken? value)
		{
			var field = _factory.Schema.Fields.First(field => field.Id == fieldId);
			if (Game.HasValue ? field.Scope == ConfigurationScope.GlobalOnly : field.Scope == ConfigurationScope.GameOnly) throw new InvalidOperationException("This field is not configurable in the selected scope.");
			Draft.Set(fieldId, value);
			_resetFields.Remove(fieldId);
		}
		public void Revert() { Draft = _store.Read(_factory.Descriptor, _factory.Schema, Game); _baseline = Draft.Copy(); _resetFields.Clear(); }
		/// <summary>Refreshes aliases without discarding edits already made in this draft.</summary>
		public void RefreshUnedited(PluginSettingsSnapshot saved, IEnumerable<string> fields)
		{
			foreach (string id in fields) {
				if (HasEdits(id)) continue;
				Draft.Set(id, saved.Value(id)); _baseline.Set(id, saved.Value(id));
			}
		}
		public void Reset()
		{
			Draft = new PluginSettingsSnapshot(_factory.Schema.Version);
			foreach (var field in _factory.Schema.Fields)
				if (Game.HasValue ? field.Scope != ConfigurationScope.GlobalOnly : field.Scope != ConfigurationScope.GameOnly) _resetFields.Add(field.Id);
		}
		public IReadOnlyList<PluginValidationIssue> Validate()
		{
			var effective = Effective;
			return _factory.Schema.Validate(effective).Concat(_factory.SettingsProvider?.Validate(Request(effective)) ?? Array.Empty<PluginValidationIssue>()).ToArray();
		}
		private PluginChoiceRequest Request(PluginSettingsSnapshot settings, IReadOnlyList<PluginResourceDescriptor>? resources = null)
			=> new PluginChoiceRequest(Game, settings, _platform, resources) { Package = Package };
		public Task<PluginChoiceCatalog> DiscoverAsync(string provider, CancellationToken cancellation)
			=> _factory.SettingsProvider?.DiscoverAsync(provider, Request(Effective, _resources()), cancellation) ?? Task.FromResult(new PluginChoiceCatalog("empty", Array.Empty<PluginChoice>()));
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
		public void Dispose() { _lifetime.Cancel(); }
	}
}
