// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>One plugin owns one folder; game overrides contain only explicitly changed values.</summary>
	public sealed class PluginConfigurationStore
	{
		private readonly object _writes = new object();
		public string Root { get; }
		public PluginConfigurationStore(string root) { Root = Path.GetFullPath(root); }
		public string DirectoryFor(PluginDescriptor plugin) => Path.Combine(Root, plugin.Id);
		public string SettingsPath(PluginDescriptor plugin, GameIdentity? game = null) => game.HasValue
			? Path.Combine(DirectoryFor(plugin), "games", Hash(game.Value.Id) + ".json") : Path.Combine(DirectoryFor(plugin), "settings.json");
		public PluginSettingsSnapshot Read(PluginDescriptor plugin, PluginSettingsSchema schema, GameIdentity? game = null)
		{
			var document = ReadDocument(SettingsPath(plugin, game));
			if (game.HasValue && document["game"] != null && document.Value<string>("game") != game.Value.Id)
				throw new InvalidDataException("The saved profile belongs to another game.");
			return schema.Upgrade(new PluginSettingsSnapshot(document.Value<int?>("schemaVersion") ?? schema.Version, (JObject?)document["values"]?.DeepClone() ?? new JObject()));
		}
		public PluginSettingsSnapshot Effective(PluginDescriptor plugin, PluginSettingsSchema schema, GameIdentity? game = null)
		{
			var global = Read(plugin, schema);
			return Resolve(schema, global, game.HasValue ? Read(plugin, schema, game) : null);
		}
		/// <summary>Known fields are restricted to their declared scope; unknown values remain available for compatible upgrades.</summary>
		public static PluginSettingsSnapshot Resolve(PluginSettingsSchema schema, PluginSettingsSnapshot global, PluginSettingsSnapshot? game = null)
		{
			PluginSettingsSnapshot Scoped(PluginSettingsSnapshot values, bool gameScope)
			{
				var scoped = values.Copy();
				foreach (var field in schema.Fields)
					if (gameScope ? field.Scope == ConfigurationScope.GlobalOnly : field.Scope == ConfigurationScope.GameOnly) scoped.Values.Remove(field.Id);
				return scoped;
			}
			var resolved = game == null ? PluginSettingsSnapshot.Merge(schema.Defaults(), Scoped(global, false))
				: PluginSettingsSnapshot.Merge(schema.Defaults(), Scoped(global, false), Scoped(game, true));
			PluginSettingsSchema.FillRecordDefaults(schema.Fields, resolved.Values);
			return resolved;
		}
		public void Save(PluginDescriptor plugin, PluginSettingsSchema schema, PluginSettingsSnapshot values, GameIdentity? game = null)
		{
			lock (_writes) SaveCore(plugin, schema, values, game);
		}
		private void SaveCore(PluginDescriptor plugin, PluginSettingsSchema schema, PluginSettingsSnapshot values, GameIdentity? game)
		{
			string path = SettingsPath(plugin, game);
			var document = ReadDocument(path);
			if ((document.Value<int?>("schemaVersion") ?? schema.Version) > schema.Version)
				throw new InvalidOperationException("This configuration was written by a newer plugin. It is preserved and cannot be overwritten.");
			var stored = Read(plugin, schema, game).Values;
			foreach (var field in schema.Fields) {
				bool allowed = game.HasValue ? field.Scope != ConfigurationScope.GlobalOnly : field.Scope != ConfigurationScope.GameOnly;
				if (!allowed || field.Kind == SettingKind.Action || field.Kind == SettingKind.Status || field.Kind == SettingKind.Preview) continue;
				if (values.Values.TryGetValue(field.Id, out var value)) stored[field.Id] = value.DeepClone();
				else stored.Remove(field.Id);
			}
			document["schemaVersion"] = schema.Version;
			document["values"] = stored;
			if (game.HasValue) document["game"] = game.Value.Id;
			WriteDocument(path, document);
		}
		/// <summary>Merges a scoped edit into the latest profile without replacing unrelated fields.</summary>
		public PluginSettingsSnapshot MergePatch(PluginDescriptor plugin, PluginSettingsSchema schema, PluginSettingsSnapshot values, IReadOnlyCollection<string> changed, GameIdentity? game = null)
		{
			var latest = Read(plugin, schema, game);
			if (latest.SchemaVersion > schema.Version) throw new InvalidOperationException("This configuration was written by a newer plugin and cannot be overwritten.");
			foreach (var field in schema.Fields) {
				if (!changed.Contains(field.Id) || (game.HasValue ? field.Scope == ConfigurationScope.GlobalOnly : field.Scope == ConfigurationScope.GameOnly)
					|| field.Kind == SettingKind.Action || field.Kind == SettingKind.Status || field.Kind == SettingKind.Preview) continue;
				latest.Set(field.Id, values.Value(field.Id));
			}
			return latest;
		}
		public void SavePatch(PluginDescriptor plugin, PluginSettingsSchema schema, PluginSettingsSnapshot values, IReadOnlyCollection<string> changed, GameIdentity? game = null)
		{
			lock (_writes) SaveCore(plugin, schema, MergePatch(plugin, schema, values, changed, game), game);
		}
		public bool Enabled(PluginDescriptor plugin, GameIdentity? game = null)
		{
			if (!plugin.CanDisable) return true;
			var policy = ReadDocument(Path.Combine(DirectoryFor(plugin), "policy.json"));
			if (game.HasValue && policy["games"] is JObject games && games[game.Value.Id]?.Type == JTokenType.Boolean) return games.Value<bool>(game.Value.Id);
			return policy.Value<bool?>("enabled") ?? plugin.EnabledByDefault;
		}
		public void SetEnabled(PluginDescriptor plugin, bool? enabled, GameIdentity? game = null)
		{
			if (!plugin.CanDisable) {
				if (enabled == false) throw new InvalidOperationException("This configuration service cannot be disabled.");
				return;
			}
			string path = Path.Combine(DirectoryFor(plugin), "policy.json");
			var policy = ReadDocument(path);
			if (game.HasValue) {
				var games = policy["games"] as JObject ?? new JObject();
				if (enabled.HasValue) games[game.Value.Id] = enabled.Value; else games.Remove(game.Value.Id);
				policy["games"] = games;
			} else if (enabled.HasValue) policy["enabled"] = enabled.Value; else policy.Remove("enabled");
			WriteDocument(path, policy);
		}
		public bool? EnableOverride(PluginDescriptor plugin, GameIdentity game) => ReadDocument(Path.Combine(DirectoryFor(plugin), "policy.json"))["games"]?[game.Id]?.Value<bool?>();

		internal static JObject ReadDocument(string path)
		{
			if (!File.Exists(path)) return new JObject();
			using var stream = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
			using var reader = new JsonTextReader(stream) { DateParseHandling = DateParseHandling.None, MaxDepth = 64, FloatParseHandling = FloatParseHandling.Double };
			return JObject.Load(reader);
		}
		internal static void WriteDocument(string path, JObject document)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			string pending = path + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp";
			try {
				using (var file = new StreamWriter(pending, false, new UTF8Encoding(false)))
				using (var writer = new JsonTextWriter(file) { Formatting = Formatting.Indented }) document.WriteTo(writer);
				for (int attempt = 0; ; attempt++) {
					try { if (File.Exists(path)) File.Replace(pending, path, null); else File.Move(pending, path); break; }
					catch (IOException error) when (attempt < 3 && ((error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33)) { Thread.Sleep(20 * (attempt + 1)); }
				}
			} finally { if (File.Exists(pending)) File.Delete(pending); }
		}
		public static string Hash(string identity)
		{
			using var sha = SHA256.Create();
			var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
			var result = new StringBuilder(bytes.Length * 2);
			foreach (byte value in bytes) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
			return result.ToString();
		}
	}
}
