// SPDX-License-Identifier: MIT
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
		public GameIdentity? Game { get; }
		public PluginSettingsSnapshot Draft { get; private set; }
		public PluginSettingsSnapshot Effective => Game.HasValue
			? PluginConfigurationStore.Resolve(_factory.Schema, _store.Read(_factory.Descriptor, _factory.Schema), Draft)
			: PluginConfigurationStore.Resolve(_factory.Schema, Draft);
		public PluginSettingsSession(IPlayerPluginFactory factory, PluginConfigurationStore store, string platform, GameIdentity? game = null, Func<IReadOnlyList<PluginResourceDescriptor>>? resources = null)
		{ _factory = factory; _store = store; _platform = platform; Game = game; _resources = resources ?? (() => Array.Empty<PluginResourceDescriptor>()); Draft = store.Read(factory.Descriptor, factory.Schema, game); }
		public void Set(string fieldId, JToken? value)
		{
			var field = _factory.Schema.Fields.First(field => field.Id == fieldId);
			if (Game.HasValue ? field.Scope == ConfigurationScope.GlobalOnly : field.Scope == ConfigurationScope.GameOnly) throw new InvalidOperationException("This field is not configurable in the selected scope.");
			Draft.Set(fieldId, value);
		}
		public void Revert() => Draft = _store.Read(_factory.Descriptor, _factory.Schema, Game);
		public void Reset() => Draft = new PluginSettingsSnapshot(_factory.Schema.Version);
		public IReadOnlyList<PluginValidationIssue> Validate()
		{
			var effective = Effective;
			return _factory.Schema.Validate(effective).Concat(_factory.SettingsProvider?.Validate(new PluginChoiceRequest(Game, effective, _platform)) ?? Array.Empty<PluginValidationIssue>()).ToArray();
		}
		public Task<PluginChoiceCatalog> DiscoverAsync(string provider, CancellationToken cancellation)
			=> _factory.SettingsProvider?.DiscoverAsync(provider, new PluginChoiceRequest(Game, Effective, _platform, _resources()), cancellation) ?? Task.FromResult(new PluginChoiceCatalog("empty", Array.Empty<PluginChoice>()));
		public async Task<PluginActionResult> RunActionAsync(string action, CancellationToken cancellation)
		{
			if (_factory.SettingsProvider == null) return new PluginActionResult(false, "This action is unavailable.");
			var result = await _factory.SettingsProvider.RunActionAsync(action, new PluginChoiceRequest(Game, Effective, _platform), cancellation);
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
				var effective = Game.HasValue ? PluginConfigurationStore.Resolve(_factory.Schema, _store.Read(_factory.Descriptor, _factory.Schema), draft) : PluginConfigurationStore.Resolve(_factory.Schema, draft);
				var request = new PluginChoiceRequest(Game, effective, _platform, _resources());
				var issues = _factory.Schema.Validate(effective).Concat(_factory.SettingsProvider?.Validate(request) ?? Array.Empty<PluginValidationIssue>()).ToList();
				foreach (var field in _factory.Schema.Fields.Where(field => field.Kind == SettingKind.Choice && field.Provider.Length > 0 && field.Visible(effective))) {
					string selected = effective.String(field.Id);
					if (selected.Length == 0 || _factory.SettingsProvider == null) continue;
					var catalog = await _factory.SettingsProvider.DiscoverAsync(field.Provider, request, linked.Token).ConfigureAwait(false);
					var choice = catalog.Find(selected);
					if (choice != null && !choice.Available || choice == null && catalog.IsComplete) issues.Add(new PluginValidationIssue(field.Id, choice?.UnavailableReason ?? "Selected item is missing."));
				}
				linked.Token.ThrowIfCancellationRequested();
				if (issues.Count > 0) return new PluginActionResult(false, string.Join("\n", issues.Select(issue => issue.Field + ": " + issue.Message)));
				if (host != null) return await host.CommitSettingsAsync(_factory, draft, Game, linked.Token).ConfigureAwait(false);
				_store.Save(_factory.Descriptor, _factory.Schema, draft, Game);
				return new PluginActionResult(true, "Saved. Changes take effect on the next game.");
			} finally { _apply.Release(); }
		}
		public void Dispose() { _lifetime.Cancel(); }
	}
}
