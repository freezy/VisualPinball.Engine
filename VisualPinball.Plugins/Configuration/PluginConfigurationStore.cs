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
	/// <remarks>
	/// The store keeps every plugin's saved settings and enablement as JSON files below <see cref="Root"/>:
	/// <list type="bullet">
	/// <item><description><c>&lt;plugin id&gt;/settings.json</c>: the global values.</description></item>
	/// <item><description><c>&lt;plugin id&gt;/games/&lt;hash&gt;.json</c>: one game's overrides, named by <see cref="Hash"/> of the
	/// <see cref="GameIdentity.Id"/>.</description></item>
	/// <item><description><c>&lt;plugin id&gt;/policy.json</c>: whether the plugin is enabled, globally and per game.</description></item>
	/// </list>
	/// Each settings file holds <c>schemaVersion</c>, <c>values</c> and, for game files, the <c>game</c> it belongs to. Values the
	/// current schema does not know are kept, so a newer plugin version can still find them.
	/// <para>
	/// The Player creates one store at <c>&lt;user config directory&gt;/plugins</c> and hands it to every plugin as
	/// <see cref="PluginApplicationContext.Configuration"/>. The host and the settings page read the effective values from it before a
	/// game starts and write edits back through it. Plugins rarely need more than <see cref="DirectoryFor"/>: the B2S plugin and the DMD
	/// plugin keep their decoded caches under their own folder.
	/// </para>
	/// <para>
	/// Reading is lock-free and may happen on any thread. Writes are atomic (a temporary file replaces the old one), and
	/// <see cref="Save"/> and <see cref="SavePatch"/> are serialized per store instance. Because each call touches the file system, the
	/// host and the Player call these methods on worker threads.
	/// </para>
	/// <example>
	/// A plugin keeping a cache next to its settings:
	/// <code>
	/// string cache = Path.Combine(context.Configuration.DirectoryFor(context.Plugin), "cache", "pictures");
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginConfigurationStore
	{
		private readonly object _writes = new object();
		/// <summary>The absolute directory that holds one folder per plugin.</summary>
		public string Root { get; }
		/// <summary>Creates a store rooted at a directory. Nothing is created on disk until the first write.</summary>
		/// <param name="root">The configuration directory, made absolute with <see cref="Path.GetFullPath(string)"/>.</param>
		public PluginConfigurationStore(string root) { Root = Path.GetFullPath(root); }
		/// <summary>
		/// The folder that belongs to a plugin, <c>Root/&lt;plugin id&gt;</c>. It may not exist yet. Plugins may keep their own files in
		/// it, such as caches, as long as they avoid <c>settings.json</c>, <c>policy.json</c> and <c>games/</c>.
		/// </summary>
		/// <param name="plugin">The plugin whose folder to return.</param>
		public string DirectoryFor(PluginDescriptor plugin) => Path.Combine(Root, plugin.Id);
		/// <summary>
		/// The path of a plugin's global settings file, or of one game's override file when <paramref name="game"/> is given. The file may
		/// not exist yet.
		/// </summary>
		/// <param name="plugin">The plugin whose settings file to return.</param>
		/// <param name="game">The game whose overrides to locate, or null for the global settings.</param>
		public string SettingsPath(PluginDescriptor plugin, GameIdentity? game = null) => game.HasValue
			? Path.Combine(DirectoryFor(plugin), "games", Hash(game.Value.Id) + ".json") : Path.Combine(DirectoryFor(plugin), "settings.json");
		/// <summary>
		/// Reads one stored layer as it is saved: the global values, or only the overrides of one game. Defaults and the other layer are
		/// not applied; use <see cref="Effective"/> for the values a plugin runs with.
		/// </summary>
		/// <remarks>
		/// A missing file returns an empty snapshot at the schema's version. Stored values are upgraded with
		/// <see cref="PluginSettingsSchema.Upgrade"/>; a file written by a newer schema is returned unchanged with its newer
		/// <see cref="PluginSettingsSnapshot.SchemaVersion"/>, which callers can compare with <see cref="PluginSettingsSchema.Version"/>.
		/// </remarks>
		/// <param name="plugin">The plugin whose settings to read.</param>
		/// <param name="schema">The plugin's schema, used to upgrade older files.</param>
		/// <param name="game">The game whose overrides to read, or null for the global values.</param>
		/// <returns>A new, detached snapshot the caller may modify.</returns>
		/// <exception cref="InvalidDataException">The game file names a different game.</exception>
		/// <exception cref="JsonException">The file is not valid JSON.</exception>
		/// <exception cref="InvalidOperationException">An upgrade step is missing or failed.</exception>
		public PluginSettingsSnapshot Read(PluginDescriptor plugin, PluginSettingsSchema schema, GameIdentity? game = null)
		{
			var document = ReadDocument(SettingsPath(plugin, game));
			if (game.HasValue && document["game"] != null && document.Value<string>("game") != game.Value.Id)
				throw new InvalidDataException("The saved profile belongs to another game.");
			return schema.Upgrade(new PluginSettingsSnapshot(document.Value<int?>("schemaVersion") ?? schema.Version, (JObject?)document["values"]?.DeepClone() ?? new JObject()));
		}
		/// <summary>
		/// Reads the values a plugin runs with: schema defaults, then the saved global values, then the game's overrides when
		/// <paramref name="game"/> is given (see <see cref="Resolve"/>).
		/// </summary>
		/// <remarks>
		/// The host calls this when it prepares a game and passes the result to the plugin as <see cref="PluginGameContext.Settings"/>.
		/// The volume mixer calls it without a game to show global values.
		/// </remarks>
		/// <param name="plugin">The plugin whose settings to read.</param>
		/// <param name="schema">The plugin's schema.</param>
		/// <param name="game">The game being played or configured, or null for the global values only.</param>
		/// <returns>A new, detached snapshot.</returns>
		/// <exception cref="InvalidDataException">The game file names a different game.</exception>
		/// <exception cref="JsonException">A file is not valid JSON.</exception>
		public PluginSettingsSnapshot Effective(PluginDescriptor plugin, PluginSettingsSchema schema, GameIdentity? game = null)
		{
			var global = Read(plugin, schema);
			return Resolve(schema, global, game.HasValue ? Read(plugin, schema, game) : null);
		}
		/// <summary>Known fields are restricted to their declared scope; unknown values remain available for compatible upgrades.</summary>
		/// <remarks>
		/// Combines schema defaults, the global layer and the optional game layer into effective values, without touching the disk. Before
		/// merging, <see cref="ConfigurationScope.GameOnly"/> fields are removed from the global layer and
		/// <see cref="ConfigurationScope.GlobalOnly"/> fields from the game layer, so a stray value in the wrong file has no effect. Later
		/// layers replace whole top-level values; missing defaults inside composite and collection records are filled in afterwards.
		/// The settings session uses this to preview a draft that is not saved yet.
		/// </remarks>
		/// <param name="schema">The plugin's schema, which supplies defaults and scopes.</param>
		/// <param name="global">The global values, as returned by <see cref="Read"/> without a game.</param>
		/// <param name="game">The game's overrides, or null to resolve global values only.</param>
		/// <returns>A new snapshot at <see cref="PluginSettingsSchema.Version"/>.</returns>
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
		/// <summary>
		/// Replaces one stored layer with <paramref name="values"/>: every field allowed in that layer takes the value from the snapshot,
		/// or is removed from the file when the snapshot has none (for a game layer, removing means "inherit the global value").
		/// </summary>
		/// <remarks>
		/// Fields outside the layer's scope, and <see cref="SettingKind.Action"/>, <see cref="SettingKind.Status"/> and
		/// <see cref="SettingKind.Preview"/> fields, are never written. Stored values the schema does not know are kept. The values are
		/// not validated here; the host and the settings session validate before they save. To change only some fields without
		/// overwriting concurrent edits to others, use <see cref="SavePatch"/>.
		/// </remarks>
		/// <param name="plugin">The plugin whose settings to write.</param>
		/// <param name="schema">The plugin's schema.</param>
		/// <param name="values">The complete layer to store.</param>
		/// <param name="game">The game whose overrides to write, or null for the global values.</param>
		/// <exception cref="InvalidOperationException">The file was written by a newer schema version and is left untouched.</exception>
		/// <exception cref="InvalidDataException">The game file names a different game.</exception>
		/// <exception cref="IOException">The file could not be written.</exception>
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
		/// <remarks>
		/// Reads the stored layer and copies over only the fields named in <paramref name="changed"/> that are allowed in that layer and
		/// hold a value (not <see cref="SettingKind.Action"/>, <see cref="SettingKind.Status"/> or <see cref="SettingKind.Preview"/>). A
		/// changed field that <paramref name="values"/> does not contain is removed, which resets it to the inherited value. Nothing is
		/// written; the host uses the result to validate an edit before <see cref="SavePatch"/> stores it.
		/// </remarks>
		/// <param name="plugin">The plugin whose settings to read.</param>
		/// <param name="schema">The plugin's schema.</param>
		/// <param name="values">The edited values; only the fields in <paramref name="changed"/> are taken from it.</param>
		/// <param name="changed">IDs of the top-level fields the user changed.</param>
		/// <param name="game">The game whose overrides to merge into, or null for the global values.</param>
		/// <returns>The stored layer with the edit applied, as a new snapshot.</returns>
		/// <exception cref="InvalidOperationException">The stored layer was written by a newer schema version.</exception>
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
		/// <summary>
		/// Saves only the fields named in <paramref name="changed"/> into the latest stored layer, leaving every other stored value as it
		/// is on disk. The read and the write happen under the store's write lock, so two edits of different fields do not overwrite each
		/// other.
		/// </summary>
		/// <remarks>
		/// This is how the Player saves: the settings page saves the fields the user edited, and the volume mixer saves one field. See
		/// <see cref="MergePatch"/> for which fields are taken.
		/// </remarks>
		/// <param name="plugin">The plugin whose settings to write.</param>
		/// <param name="schema">The plugin's schema.</param>
		/// <param name="values">The edited values.</param>
		/// <param name="changed">IDs of the top-level fields to save.</param>
		/// <param name="game">The game whose overrides to write, or null for the global values.</param>
		/// <exception cref="InvalidOperationException">The stored layer was written by a newer schema version.</exception>
		/// <exception cref="IOException">The file could not be written.</exception>
		public void SavePatch(PluginDescriptor plugin, PluginSettingsSchema schema, PluginSettingsSnapshot values, IReadOnlyCollection<string> changed, GameIdentity? game = null)
		{
			lock (_writes) SaveCore(plugin, schema, MergePatch(plugin, schema, values, changed, game), game);
		}
		/// <summary>
		/// Whether the plugin should run, globally or for one game. A plugin with <see cref="PluginDescriptor.CanDisable"/> false is always
		/// enabled. Otherwise a per-game choice wins, then the global choice, then <see cref="PluginDescriptor.EnabledByDefault"/>.
		/// </summary>
		/// <remarks>
		/// The host checks this for every plugin when it selects the plugins for a game; the Player's settings page shows it as the
		/// "Enabled by default" or "Enabled for this game" toggle.
		/// </remarks>
		/// <param name="plugin">The plugin to check.</param>
		/// <param name="game">The game to check, or null for the global choice.</param>
		/// <exception cref="JsonException"><c>policy.json</c> is not valid JSON.</exception>
		public bool Enabled(PluginDescriptor plugin, GameIdentity? game = null)
		{
			if (!plugin.CanDisable) return true;
			var policy = ReadDocument(Path.Combine(DirectoryFor(plugin), "policy.json"));
			if (game.HasValue && policy["games"] is JObject games && games[game.Value.Id]?.Type == JTokenType.Boolean) return games.Value<bool>(game.Value.Id);
			return policy.Value<bool?>("enabled") ?? plugin.EnabledByDefault;
		}
		/// <summary>
		/// Saves whether a plugin is enabled, globally or for one game, in its <c>policy.json</c>. The change takes effect on the next game.
		/// </summary>
		/// <remarks>
		/// For a plugin with <see cref="PluginDescriptor.CanDisable"/> false nothing is written, and asking to disable it throws.
		/// Unlike <see cref="Save"/>, this does not take the store's write lock.
		/// </remarks>
		/// <param name="plugin">The plugin to change.</param>
		/// <param name="enabled">True or false to enable or disable; null removes the choice, so the game follows the global choice and the
		/// global choice follows <see cref="PluginDescriptor.EnabledByDefault"/>.</param>
		/// <param name="game">The game to change, or null for the global choice.</param>
		/// <exception cref="InvalidOperationException"><paramref name="enabled"/> is false for a plugin that cannot be disabled.</exception>
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
		/// <summary>
		/// The per-game enablement saved for <paramref name="game"/>, or null when the game follows the global choice. Use it to tell
		/// whether a game overrides the global policy; <see cref="Enabled"/> gives the resulting decision. Does not consider
		/// <see cref="PluginDescriptor.CanDisable"/>.
		/// </summary>
		/// <param name="plugin">The plugin to check.</param>
		/// <param name="game">The game to check.</param>
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
		/// <summary>
		/// Turns a game identity into a file name: the lowercase hexadecimal SHA-256 of its UTF-8 bytes. Used for the per-game settings
		/// files, so any identity yields a safe, fixed-length name.
		/// </summary>
		/// <param name="identity">The text to hash, normally a <see cref="GameIdentity.Id"/> such as <c>pinmame:t2_l8</c>.</param>
		/// <returns>64 lowercase hexadecimal characters.</returns>
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
