// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VisualPinball.Plugins
{
	public enum SettingKind { Boolean, Integer, Number, Text, Folder, File, Choice, Color, Rectangle, Collection, Status, Action, Custom }

	public sealed class PluginField
	{
		public string Id { get; }
		public string Label { get; }
		public SettingKind Kind { get; }
		public string Description { get; set; } = "";
		public ConfigurationScope Scope { get; set; } = ConfigurationScope.GlobalOnly;
		public ConfigurationChange Change { get; set; } = ConfigurationChange.NextGame;
		public JToken? Default { get; set; }
		public double? Minimum { get; set; }
		public double? Maximum { get; set; }
		public string Provider { get; set; } = "";
		public string ViewExtension { get; set; } = "";
		public string VisibleWhen { get; set; } = "";
		public JToken? VisibleValue { get; set; }
		public IReadOnlyList<PluginField> Children { get; set; } = Array.Empty<PluginField>();
		public Func<JToken?, string?>? Validate { get; set; }
		public PluginField(string id, string label, SettingKind kind) { Id = id; Label = label; Kind = kind; }
		public bool Visible(PluginSettingsSnapshot snapshot) => VisibleWhen.Length == 0 || JToken.DeepEquals(snapshot.Value(VisibleWhen), VisibleValue ?? new JValue(true));
	}

	public sealed class PluginSettingsGroup
	{
		public string Title { get; }
		public string Description { get; }
		public IReadOnlyList<PluginField> Fields { get; }
		public PluginSettingsGroup(string title, string description, params PluginField[] fields) { Title = title; Description = description; Fields = fields; }
	}

	public readonly struct PluginValidationIssue
	{
		public string Field { get; }
		public string Message { get; }
		public PluginValidationIssue(string field, string message) { Field = field; Message = message; }
	}

	public sealed class PluginSettingsSchema
	{
		public int Version { get; }
		public IReadOnlyList<PluginSettingsGroup> Groups { get; }
		/// <summary>Each entry upgrades a detached values tree from its key to the next version.</summary>
		public IReadOnlyDictionary<int, Func<JObject, JObject>> Migrations { get; set; } = new Dictionary<int, Func<JObject, JObject>>();
		public IEnumerable<PluginField> Fields => Groups.SelectMany(group => group.Fields);
		public PluginSettingsSchema(int version, params PluginSettingsGroup[] groups)
		{
			if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
			Version = version;
			Groups = groups;
			var seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (var field in Fields) if (!seen.Add(field.Id)) throw new ArgumentException("Duplicate plugin field " + field.Id + ".");
		}
		public PluginSettingsSnapshot Upgrade(PluginSettingsSnapshot snapshot)
		{
			var values = (JObject)snapshot.Values.DeepClone();
			for (int version = snapshot.SchemaVersion; version < Version; version++) {
				if (!Migrations.TryGetValue(version, out var migrate)) throw new InvalidOperationException("No configuration migration from version " + version + ".");
				values = migrate(values) ?? throw new InvalidOperationException("Configuration migration returned no values.");
			}
			return new PluginSettingsSnapshot(Math.Max(snapshot.SchemaVersion, Version), values);
		}
		public PluginSettingsSnapshot Defaults()
		{
			var values = new JObject();
			foreach (var field in Fields) if (field.Default != null) values[field.Id] = field.Default.DeepClone();
			return new PluginSettingsSnapshot(Version, values);
		}
		public IReadOnlyList<PluginValidationIssue> Validate(PluginSettingsSnapshot snapshot)
		{
			var issues = new List<PluginValidationIssue>();
			foreach (var field in Fields) {
				if (!field.Visible(snapshot) || field.Kind == SettingKind.Status || field.Kind == SettingKind.Action || field.Kind == SettingKind.Custom) continue;
				var value = snapshot.Value(field.Id);
				string? error = field.Validate?.Invoke(value);
				if (error == null && value != null) {
					if (field.Kind == SettingKind.Boolean && value.Type != JTokenType.Boolean) error = "Choose on or off.";
					if (field.Kind == SettingKind.Integer && value.Type != JTokenType.Integer) error = "Enter a whole number.";
					if (field.Kind == SettingKind.Number || field.Kind == SettingKind.Integer) {
						if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer) error = "Enter a number.";
						else {
							double number = value.Value<double>();
							if (double.IsNaN(number) || double.IsInfinity(number) || field.Minimum.HasValue && number < field.Minimum || field.Maximum.HasValue && number > field.Maximum)
								error = "Value is outside the supported range.";
						}
					}
					if (field.Kind == SettingKind.Collection && value.Type != JTokenType.Array) error = "Expected a list.";
					if ((field.Kind == SettingKind.Text || field.Kind == SettingKind.File || field.Kind == SettingKind.Folder || field.Kind == SettingKind.Choice || field.Kind == SettingKind.Color) && value.Type != JTokenType.String) error = "Expected text.";
					if (field.Kind == SettingKind.Rectangle) {
						if (!(value is JObject rectangle)) error = "Expected a rectangle.";
						else foreach (string dimension in new[] { "x", "y", "width", "height" }) {
							var coordinate = rectangle[dimension];
							if (coordinate == null || coordinate.Type != JTokenType.Integer && coordinate.Type != JTokenType.Float || !double.IsFinite(coordinate.Value<double>()) || (dimension == "width" || dimension == "height") && coordinate.Value<double>() <= 0) error = "Enter valid coordinates and positive dimensions.";
						}
					}
				}
				if (error != null) issues.Add(new PluginValidationIssue(field.Id, error));
			}
			return issues;
		}
	}

	public sealed class PluginSettingsSnapshot
	{
		public int SchemaVersion { get; }
		public JObject Values { get; }
		public PluginSettingsSnapshot(int schemaVersion, JObject? values = null) { SchemaVersion = schemaVersion; Values = values ?? new JObject(); }
		public JToken? Value(string id) => Values[id];
		public string String(string id, string fallback = "") => Value(id)?.Type == JTokenType.String ? Value(id)!.Value<string>() ?? fallback : fallback;
		public bool Boolean(string id, bool fallback = false) => Value(id)?.Type == JTokenType.Boolean ? Value(id)!.Value<bool>() : fallback;
		public int Integer(string id, int fallback = 0) => Value(id)?.Type == JTokenType.Integer ? Value(id)!.Value<int>() : fallback;
		public double Number(string id, double fallback = 0) => Value(id)?.Type == JTokenType.Integer || Value(id)?.Type == JTokenType.Float ? Value(id)!.Value<double>() : fallback;
		public PluginSettingsSnapshot Copy() => new PluginSettingsSnapshot(SchemaVersion, (JObject)Values.DeepClone());
		public void Set(string id, JToken? value) { if (value == null) Values.Remove(id); else Values[id] = value.DeepClone(); }
		public static PluginSettingsSnapshot Merge(PluginSettingsSnapshot defaults, params PluginSettingsSnapshot[] layers)
		{
			var merged = defaults.Copy();
			foreach (var layer in layers) foreach (var value in layer.Values.Properties()) merged.Values[value.Name] = value.Value.DeepClone();
			return merged;
		}
	}

	public sealed class PluginChoice
	{
		public string Id { get; }
		public string Label { get; }
		public string Description { get; }
		public bool Available { get; }
		public string UnavailableReason { get; }
		public PluginChoice(string id, string label, string description = "", bool available = true, string unavailableReason = "")
		{ Id = id; Label = label; Description = description; Available = available; UnavailableReason = unavailableReason; }
	}

	public sealed class PluginChoiceRequest
	{
		public GameIdentity? Game { get; }
		public PluginSettingsSnapshot Settings { get; }
		public string Platform { get; }
		public IReadOnlyList<PluginResourceDescriptor> Resources { get; }
		public PluginChoiceRequest(GameIdentity? game, PluginSettingsSnapshot settings, string platform, IReadOnlyList<PluginResourceDescriptor>? resources = null)
		{ Game = game; Settings = settings; Platform = platform; Resources = resources ?? Array.Empty<PluginResourceDescriptor>(); }
	}

	public sealed class PluginChoiceCatalog
	{
		public string Revision { get; }
		public IReadOnlyList<PluginChoice> Choices { get; }
		/// <summary>False when a runtime-only catalog cannot be inspected while its game is stopped.</summary>
		public bool IsComplete { get; }
		public PluginChoiceCatalog(string revision, IReadOnlyList<PluginChoice> choices, bool isComplete = true) { Revision = revision; Choices = choices; IsComplete = isComplete; }
		public PluginChoice? Find(string id) => Choices.FirstOrDefault(choice => choice.Id == id);
	}

	public readonly struct PluginActionResult
	{
		public bool Success { get; }
		public string Message { get; }
		public PluginSettingsSnapshot? Changes { get; }
		public PluginActionResult(bool success, string message, PluginSettingsSnapshot? changes = null) { Success = success; Message = message; Changes = changes; }
	}

	public interface IPluginSettingsProvider
	{
		Task<PluginChoiceCatalog> DiscoverAsync(string provider, PluginChoiceRequest request, CancellationToken cancellation);
		Task<PluginActionResult> RunActionAsync(string action, PluginChoiceRequest request, CancellationToken cancellation);
		IReadOnlyList<PluginValidationIssue> Validate(PluginChoiceRequest request);
	}
}
