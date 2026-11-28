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
	/// The type of a <see cref="PluginField"/>: which JSON value it stores, how <see cref="PluginSettingsSchema.Validate"/> checks it, and
	/// which editor the Player shows for it.
	/// </summary>
	/// <remarks>
	/// <see cref="Status"/>, <see cref="Action"/>, <see cref="Preview"/>, <see cref="Info"/> and <see cref="Outputs"/> store nothing: they
	/// are never validated or saved.
	/// </remarks>
	public enum SettingKind
	{
		/// <summary>A JSON <c>true</c> or <c>false</c>, shown as a toggle.</summary>
		Boolean,
		/// <summary>
		/// A JSON integer, shown as a number box or, with <see cref="PluginFieldPresentation.Slider"/>, a slider. Checked against
		/// <see cref="PluginField.Minimum"/> and <see cref="PluginField.Maximum"/>.
		/// </summary>
		Integer,
		/// <summary>
		/// A JSON number (integer or floating point), shown like <see cref="Integer"/> and checked against the same bounds. NaN and
		/// infinity are rejected.
		/// </summary>
		Number,
		/// <summary>A JSON string, shown as a text box.</summary>
		Text,
		/// <summary>A directory path stored as a JSON string, shown as a text box with a folder picker.</summary>
		Folder,
		/// <summary>A file path stored as a JSON string, shown as a text box with a file picker.</summary>
		File,
		/// <summary>
		/// The <see cref="PluginChoice.Id"/> of one item from the catalog named by <see cref="PluginField.Provider"/>, stored as a JSON
		/// string. The Player asks <see cref="IPluginSettingsProvider.DiscoverAsync"/> for the catalog, shows up to five choices as buttons
		/// and more as a dropdown, and refuses to apply a selection that the catalog marks unavailable or, when the catalog is
		/// <see cref="PluginChoiceCatalog.IsComplete"/>, no longer lists.
		/// </summary>
		Choice,
		/// <summary>
		/// A color stored as a JSON string, such as <c>#ff8000</c> as used by the DMD plugin. Only checked to be text; the Player edits it
		/// as text.
		/// </summary>
		Color,
		/// <summary>
		/// A JSON object with numeric <c>x</c>, <c>y</c>, <c>width</c> and <c>height</c>, where width and height are positive. The B2S
		/// plugin uses it for the DMD area on a backglass.
		/// </summary>
		Rectangle,
		/// <summary>
		/// A JSON array. With <see cref="PluginField.Children"/>, each item is a record (JSON object) of those fields, validated like a
		/// <see cref="Composite"/>; without children, the Player edits the items as text. The B2S plugin uses it for its list of
		/// backglass folders.
		/// </summary>
		Collection,
		/// <summary>Read-only text showing the plugin's current runtime status message. Stores nothing.</summary>
		Status,
		/// <summary>
		/// A button that runs <see cref="IPluginSettingsProvider.RunActionAsync"/> with <see cref="PluginField.Provider"/> as the action
		/// name, such as PinUP's "Find installed media libraries". Stores nothing; the action may return values for the draft.
		/// </summary>
		Action,
		/// <summary>A JSON object whose properties are the <see cref="PluginField.Children"/> fields, edited and validated as one record.</summary>
		Composite,
		/// <summary>
		/// A JSON array of window placements, each an object with a unique <c>screen</c> identifier (string or integer) and integer
		/// <c>x</c>, <c>y</c>, <c>width</c> and <c>height</c> in desktop pixels (sizes from 1 to 16384). The Player shows a placement
		/// editor that opens a window per screen; the screen identifiers are checked against the catalog named by
		/// <see cref="PluginField.Provider"/>. PinUP and B2S use it for their output windows.
		/// </summary>
		ScreenLayout,
		/// <summary>
		/// A live preview area. The Player opens it from the plugin's Unity preview provider with <see cref="PluginField.Provider"/> as the
		/// preview name, such as PinUP's pack preview. Stores nothing.
		/// </summary>
		Preview,
		/// <summary>
		/// Read-only information the plugin works out while its settings page is shown, such as which configuration is active or which
		/// devices are connected. The Player asks <see cref="IPluginSettingsInfoProvider.DescribeAsync"/> with
		/// <see cref="PluginField.Provider"/> as the name each time it draws the page, so the block also shows what an action just
		/// changed. Stores nothing. Only for top-level fields, not inside composite or collection records.
		/// </summary>
		Info,
		/// <summary>
		/// The outputs a plugin drives, such as the ports of the boards in a cabinet: the Player shows each board's outputs as a grid,
		/// with what is wired to each and a button that tests it. It asks <see cref="IPluginOutputMapProvider.DescribeOutputsAsync"/>
		/// with <see cref="PluginField.Provider"/> as the name each time it draws the page, and
		/// <see cref="IPluginOutputMapProvider.TestOutputAsync"/> when a test button is pressed. Stores nothing. Only for top-level
		/// fields, not inside composite or collection records.
		/// </summary>
		Outputs
	}

	/// <summary>
	/// One setting a plugin declares: its stored key, its type, how the Player presents it, where it can be configured and when a
	/// change takes effect.
	/// </summary>
	/// <remarks>
	/// Fields are declared once, in the plugin factory's <see cref="IPlayerPluginFactory.Schema"/>. The Player builds the plugin's
	/// settings page from them, the store saves their values under <see cref="Id"/>, and the plugin reads the effective values from
	/// <see cref="PluginGameContext.Settings"/> when a game is prepared. Set the optional properties in an object initializer and do
	/// not change a field after the schema is handed to the host.
	/// <example>
	/// Fields from the B2S plugin: a game-only choice, a folder list, and two integers that share a row and only show for one mode:
	/// <code>
	/// new PluginField("Backglass", "Backglass", SettingKind.Choice) {
	///     Default = "auto", Scope = ConfigurationScope.GameOnly, Provider = "backglasses"
	/// },
	/// new PluginField("Folders", "Backglass folders", SettingKind.Collection) {
	///     Default = new JArray(), FullWidth = true,
	///     Children = new[] { new PluginField("Path", "Folder", SettingKind.Folder) { Default = "" } }
	/// },
	/// new PluginField("FadeOn", "Fade in (ms)", SettingKind.Integer) {
	///     Default = 20, Minimum = 0, Maximum = 1000, Row = "fade", VisibleWhen = "LampResponse", VisibleValue = "smooth"
	/// },
	/// new PluginField("FadeOff", "Fade out (ms)", SettingKind.Integer) {
	///     Default = 40, Minimum = 0, Maximum = 1000, Row = "fade", VisibleWhen = "LampResponse", VisibleValue = "smooth"
	/// },
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginField
	{
		/// <summary>
		/// The key the value is stored under. Unique among a schema's top-level fields, or among the <see cref="Children"/> of one field.
		/// Keep it stable: renaming it loses saved values unless a <see cref="PluginSettingsSchema.Migrations"/> step moves them.
		/// </summary>
		public string Id { get; }
		/// <summary>The name shown next to the editor; for an <see cref="SettingKind.Action"/> field, the button text.</summary>
		public string Label { get; }
		/// <summary>The value type and editor of the field.</summary>
		public SettingKind Kind { get; }
		/// <summary>Help text shown below the editor. Empty (the default) shows none.</summary>
		public string Description { get; set; } = "";
		/// <summary>Places the label above the editor for long values; ordinary fields render inline.</summary>
		/// <remarks>
		/// Collection, composite, screen layout and preview fields always use the full width. Paths are a typical use, such as PinUP's
		/// <c>PUPVideos</c> library folder.
		/// </remarks>
		public bool FullWidth { get; set; }
		/// <summary>Optional previous setting in guided launch setup. Enables changing an earlier choice when setup resumes at this field.</summary>
		public string SetupPrevious { get; set; } = "";
		/// <summary>For an Action field, runs it without another confirmation when a launch check requires it during guided setup.</summary>
		/// <remarks>Defaults to false. Normal settings pages still require pressing the action button. A failed action waits for an explicit retry.</remarks>
		public bool SetupAutoRun { get; set; }
		/// <summary>Consecutive fields with the same non-empty hint share a row. The Player owns their sizing and appearance.</summary>
		/// <remarks>The B2S plugin puts its fade-in and fade-out times on one row with <c>Row = "fade"</c>.</remarks>
		public string Row { get; set; } = "";
		/// <summary>
		/// For a <see cref="SettingKind.Action"/> field, the <see cref="Id"/> of another field in the same group whose row shows the
		/// button, after its editor; the action then gets no row of its own, and its result is shown under that row. Empty (the default)
		/// gives the action its own row. The target must not be an action itself. The button is disabled while the target has nothing
		/// to act on: an empty text, file or folder, an empty list, or a choice without an available item picked; on an
		/// <see cref="SettingKind.Info"/> field, while <see cref="PluginInfo.UnavailableActions"/> names it.
		/// </summary>
		/// <remarks>
		/// An import button belongs next to the file it imports, and an undo button next to what it undoes:
		/// <code>
		/// new PluginField("ImportZip", "Config Tool zip", SettingKind.File) { Default = "" },
		/// new PluginField("Import", "Import", SettingKind.Action) { Provider = "import-zip", AttachTo = "ImportZip" },
		/// </code>
		/// </remarks>
		public string AttachTo { get; set; } = "";
		/// <summary>
		/// The editor style for <see cref="SettingKind.Integer"/> and <see cref="SettingKind.Number"/> fields. Defaults to a number box.
		/// </summary>
		public PluginFieldPresentation Presentation { get; set; }
		/// <summary>
		/// Unit text appended to the value shown by a <see cref="PluginFieldPresentation.Slider"/>, such as <c>%</c>. Empty by default.
		/// Put units of other fields in the <see cref="Label"/>.
		/// </summary>
		public string Unit { get; set; } = "";
		/// <summary>Opt-in appearance on a shared Player page; storage still belongs to this plugin.</summary>
		/// <remarks>Null (the default) shows the field only on the plugin's own page. See <see cref="PluginGlobalSetting"/>.</remarks>
		public PluginGlobalSetting? GlobalSetting { get; set; }
		/// <summary>
		/// Where the field can be set: only globally (the default), only per game, or globally with optional per-game overrides. The store
		/// ignores a value saved in a layer the scope does not allow.
		/// </summary>
		public ConfigurationScope Scope { get; set; } = ConfigurationScope.GlobalOnly;
		/// <summary>
		/// When an applied edit reaches the plugin. Defaults to <see cref="ConfigurationChange.NextGame"/>: the edit is saved and the
		/// plugin sees it in <see cref="PluginGameContext.Settings"/> when the next game is prepared. With
		/// <see cref="ConfigurationChange.Live"/>, when the plugin is running the game the edit applies to, the host first passes the new
		/// values to it through <see cref="IPluginSynchronousReconfiguration"/> or <see cref="IPluginReconfiguration"/> and only saves them
		/// if that succeeds.
		/// </summary>
		public ConfigurationChange Change { get; set; } = ConfigurationChange.NextGame;
		/// <summary>
		/// The value used when nothing is saved, in the JSON shape of <see cref="Kind"/>. Null means no default: the value is absent and
		/// the typed <see cref="PluginSettingsSnapshot"/> accessors return their fallback. Defaults of <see cref="Children"/> fill missing
		/// properties inside composite and collection records.
		/// </summary>
		public JToken? Default { get; set; }
		/// <summary>
		/// Inclusive lower bound for <see cref="SettingKind.Integer"/> and <see cref="SettingKind.Number"/> values, or null for none. Also
		/// the low end of a slider.
		/// </summary>
		public double? Minimum { get; set; }
		/// <summary>
		/// Inclusive upper bound for <see cref="SettingKind.Integer"/> and <see cref="SettingKind.Number"/> values, or null for none. Also
		/// the high end of a slider.
		/// </summary>
		public double? Maximum { get; set; }
		/// <summary>
		/// The name the Player passes to the plugin's providers for this field: the catalog name for
		/// <see cref="IPluginSettingsProvider.DiscoverAsync"/> (<see cref="SettingKind.Choice"/> and <see cref="SettingKind.ScreenLayout"/>),
		/// the action name for <see cref="IPluginSettingsProvider.RunActionAsync"/> (<see cref="SettingKind.Action"/>), the preview name
		/// (<see cref="SettingKind.Preview"/>), or the information name for <see cref="IPluginSettingsInfoProvider.DescribeAsync"/>
		/// (<see cref="SettingKind.Info"/>). Empty for other kinds. The names are private to the plugin, such as PinUP's
		/// <c>packs</c> and <c>presets</c> catalogs.
		/// </summary>
		public string Provider { get; set; } = "";
		/// <summary>
		/// For <see cref="SettingKind.Choice"/> fields, lets the user pick any number of the catalog's items instead of exactly one. The
		/// Player shows every item as a button that toggles on and off, and the value is a JSON array of <see cref="PluginChoice.Id"/>s,
		/// empty when none is picked. Read it with <see cref="PluginSettingsSnapshot.Strings"/>. Not supported inside composite or
		/// collection records. False by default.
		/// </summary>
		/// <remarks>
		/// The B2S plugin lets the user show the backglass in a window, on the table's backglass, or both:
		/// <code>
		/// new PluginField("Output", "Output", SettingKind.Choice) { Provider = "outputs", Multiple = true, Default = new JArray("native") }
		/// </code>
		/// </remarks>
		public bool Multiple { get; set; }
		/// <summary>
		/// The <see cref="Id"/> of another field that controls whether this one is shown and validated; empty (the default) always shows
		/// it. Inside a composite or collection record it names a sibling in the same record. A hidden field keeps its saved value.
		/// See <see cref="Visible"/>.
		/// </summary>
		public string VisibleWhen { get; set; } = "";
		/// <summary>
		/// The value <see cref="VisibleWhen"/> must hold for this field to show, compared with <see cref="JToken.DeepEquals(JToken, JToken)"/>.
		/// Null (the default) means <c>true</c>, which suits a boolean toggle.
		/// </summary>
		public JToken? VisibleValue { get; set; }
		/// <summary>
		/// The fields of one record, for <see cref="SettingKind.Composite"/> and <see cref="SettingKind.Collection"/> fields. Empty by
		/// default. Inside records the Player uses plain editors (no sliders, screen layouts or previews), and <see cref="Scope"/>,
		/// <see cref="Change"/> and <see cref="GlobalSetting"/> only apply to top-level fields.
		/// </summary>
		public IReadOnlyList<PluginField> Children { get; set; } = Array.Empty<PluginField>();
		/// <summary>
		/// Optional extra check of this field's value. It receives the raw value (null when absent) and returns an error message to show,
		/// or null when the value is valid. When it returns a message, the built-in checks for <see cref="Kind"/> are skipped.
		/// </summary>
		/// <remarks>
		/// It runs during every <see cref="PluginSettingsSchema.Validate"/>, including on the Player's main thread, so keep it fast and
		/// free of side effects. Checks that need other fields or the file system belong in <see cref="IPluginSettingsProvider.Validate"/>.
		/// <example>
		/// <code>
		/// new PluginField("Name", "Name", SettingKind.Text) {
		///     Validate = value =&gt; value?.ToString().Length &gt; 32 ? "Use at most 32 characters." : null
		/// }
		/// </code>
		/// </example>
		/// </remarks>
		public Func<JToken?, string?>? Validate { get; set; }
		/// <summary>Creates a field. Set the optional properties with an object initializer.</summary>
		/// <param name="id">The stable storage key; see <see cref="Id"/>.</param>
		/// <param name="label">The name shown to the user.</param>
		/// <param name="kind">The value type and editor.</param>
		public PluginField(string id, string label, SettingKind kind) { Id = id; Label = label; Kind = kind; }
		/// <summary>
		/// Whether the field is shown and validated under the given values: true when <see cref="VisibleWhen"/> is empty or the controlling
		/// field's value equals <see cref="VisibleValue"/> (or <c>true</c>). A controlling field without a value hides the field.
		/// </summary>
		/// <param name="snapshot">The values to test, normally the effective settings or, inside a record, that record's values.</param>
		/// <summary>
		/// Whether the field holds a setting: false for <see cref="SettingKind.Status"/>, <see cref="SettingKind.Action"/>,
		/// <see cref="SettingKind.Preview"/>, <see cref="SettingKind.Info"/> and <see cref="SettingKind.Outputs"/>, which are never
		/// validated, saved or reset.
		/// </summary>
		public bool StoresValue => Kind != SettingKind.Status && Kind != SettingKind.Action && Kind != SettingKind.Preview && Kind != SettingKind.Info && Kind != SettingKind.Outputs;
		public bool Visible(PluginSettingsSnapshot snapshot) => VisibleWhen.Length == 0 || JToken.DeepEquals(snapshot.Value(VisibleWhen), VisibleValue ?? new JValue(true));
	}

	/// <summary>A titled section of a plugin's settings page that groups related fields.</summary>
	/// <remarks>
	/// Groups only affect presentation; field IDs must be unique across all groups of a schema. The Player hides a group when none of
	/// its fields is visible in the scope being edited, for example a group of global-only fields while a game is selected.
	/// </remarks>
	public sealed class PluginSettingsGroup
	{
		/// <summary>The section heading.</summary>
		public string Title { get; }
		/// <summary>Help text shown below the heading, or empty for none.</summary>
		public string Description { get; }
		/// <summary>The fields in display order.</summary>
		public IReadOnlyList<PluginField> Fields { get; }
		/// <summary>Creates a group.</summary>
		/// <param name="title">The section heading.</param>
		/// <param name="description">Help text below the heading; pass an empty string for none.</param>
		/// <param name="fields">The fields in display order. The array is used as is, not copied.</param>
		public PluginSettingsGroup(string title, string description, params PluginField[] fields) { Title = title; Description = description; Fields = fields; }
	}

	/// <summary>A problem found in a settings value, reported to the user before settings are applied or a game starts.</summary>
	/// <remarks>
	/// Returned by <see cref="PluginSettingsSchema.Validate"/> and <see cref="IPluginSettingsProvider.Validate"/>. When the user applies
	/// settings, issues block saving and are shown as <c>field: message</c>; when the host prepares a game, issues keep the plugin from
	/// starting for that game.
	/// </remarks>
	public readonly struct PluginValidationIssue
	{
		/// <summary>
		/// The <see cref="PluginField.Id"/> of the offending field. Choice checks inside records use a path such as
		/// <c>AnimationSlowdowns[0].Animation</c>.
		/// </summary>
		public string Field { get; }
		/// <summary>A short message for the user that says what to fix.</summary>
		public string Message { get; }
		/// <summary>Creates an issue.</summary>
		/// <param name="field">The ID of the offending field.</param>
		/// <param name="message">The message shown to the user.</param>
		public PluginValidationIssue(string field, string message) { Field = field; Message = message; }
	}

	/// <summary>
	/// The complete description of a plugin's settings: its fields, grouped for the settings page, and the version of their stored
	/// layout.
	/// </summary>
	/// <remarks>
	/// Every plugin factory exposes one schema as <see cref="IPlayerPluginFactory.Schema"/>. The Player builds the plugin's settings
	/// page from it, <see cref="PluginConfigurationStore"/> uses it to resolve defaults and scopes and to upgrade old files, and the host
	/// validates the effective values with it before the plugin starts a game. Build it once, typically in a static method of the
	/// factory, and do not change it afterwards.
	/// <para>
	/// When the stored layout changes (a field is renamed, split or changes its JSON shape), increase <see cref="Version"/> and add a
	/// <see cref="Migrations"/> step from the previous version. Adding a field with a default needs no new version.
	/// </para>
	/// <example>
	/// A small schema, as the PinMAME integration declares it:
	/// <code>
	/// public PluginSettingsSchema Schema { get; } = new PluginSettingsSchema(1,
	///     new PluginSettingsGroup("ROM audio", "Controls PinMAME playback without changing ROM settings.",
	///         new PluginField("Volume", "Volume", SettingKind.Integer) {
	///             Default = 100, Minimum = 0, Maximum = 100, Unit = "%", Change = ConfigurationChange.Live,
	///             Presentation = PluginFieldPresentation.Slider,
	///             GlobalSetting = new PluginGlobalSetting(PluginGlobalSections.VolumeMixer) { Order = 20 }
	///         }));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginSettingsSchema
	{
		/// <summary>The version of the stored layout, 1 or higher. Saved with every settings file and used to pick migrations.</summary>
		public int Version { get; }
		/// <summary>The groups of the settings page, in display order.</summary>
		public IReadOnlyList<PluginSettingsGroup> Groups { get; }
		/// <summary>Each entry upgrades a detached values tree from its key to the next version.</summary>
		/// <remarks>
		/// The entry for key <c>n</c> turns values saved by version <c>n</c> into values for version <c>n + 1</c>. It may modify and
		/// return the tree it receives, which is a copy, and must not return null. Upgrades happen when settings are read; the upgraded
		/// values are only written back when the user next saves. Empty by default.
		/// <example>
		/// Renaming a field in version 2:
		/// <code>
		/// Migrations = new Dictionary&lt;int, Func&lt;JObject, JObject&gt;&gt; {
		///     [1] = values =&gt; { values["new"] = values["old"]; values.Remove("old"); return values; }
		/// }
		/// </code>
		/// </example>
		/// </remarks>
		public IReadOnlyDictionary<int, Func<JObject, JObject>> Migrations { get; set; } = new Dictionary<int, Func<JObject, JObject>>();
		/// <summary>All top-level fields of all groups, in display order. Children of composite and collection fields are not included.</summary>
		public IEnumerable<PluginField> Fields => Groups.SelectMany(group => group.Fields);
		/// <summary>Creates a schema.</summary>
		/// <param name="version">The version of the stored layout; start at 1.</param>
		/// <param name="groups">The groups of the settings page, in display order. The array is used as is, not copied.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is less than 1.</exception>
		/// <exception cref="ArgumentException">
		/// Two top-level fields share an <see cref="PluginField.Id"/>, a composite or collection record has an
		/// <see cref="SettingKind.Info"/> field or a field with <see cref="PluginField.AttachTo"/>, or a top-level
		/// <see cref="PluginField.AttachTo"/> is set on a field that is not an action or names no other non-action field of the same
		/// group.
		/// </exception>
		public PluginSettingsSchema(int version, params PluginSettingsGroup[] groups)
		{
			if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
			Version = version;
			Groups = groups;
			var seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (var field in Fields) if (!seen.Add(field.Id)) throw new ArgumentException("Duplicate plugin field " + field.Id + ".");
			foreach (var field in Fields) CheckRecordFields(field.Children);
			foreach (var group in Groups) foreach (var field in group.Fields) {
				if (field.AttachTo.Length == 0) continue;
				if (field.Kind != SettingKind.Action) throw new ArgumentException("Only an action can be attached to another field: " + field.Id + ".");
				if (!group.Fields.Any(target => target.Id == field.AttachTo && target.Kind != SettingKind.Action))
					throw new ArgumentException("Plugin field " + field.Id + " is attached to " + field.AttachTo + ", which is no other field of its group.");
			}
		}
		// A record's fields are values of the record, each in a row of its own: information has no value to keep there, and an
		// action is not attached to another field.
		private static void CheckRecordFields(IReadOnlyList<PluginField> children)
		{
			foreach (var child in children) {
				if (child.Kind == SettingKind.Info) throw new ArgumentException("Information can only be a top-level field: " + child.Id + ".");
				if (child.Kind == SettingKind.Outputs) throw new ArgumentException("Outputs can only be a top-level field: " + child.Id + ".");
				if (child.AttachTo.Length > 0) throw new ArgumentException("Only a top-level action can be attached to another field: " + child.Id + ".");
				CheckRecordFields(child.Children);
			}
		}
		/// <summary>
		/// Brings values saved by an older version up to <see cref="Version"/> by running each <see cref="Migrations"/> step in turn.
		/// </summary>
		/// <remarks>
		/// The input snapshot is not modified. Values from the current version are returned as a copy. Values from a newer version are
		/// returned unchanged with their newer <see cref="PluginSettingsSnapshot.SchemaVersion"/>; the store then refuses to overwrite
		/// them and the host refuses to start the plugin with them.
		/// </remarks>
		/// <param name="snapshot">The saved values and the version they were written with.</param>
		/// <returns>A new snapshot.</returns>
		/// <exception cref="InvalidOperationException">A step needed for the upgrade is missing or returned null.</exception>
		public PluginSettingsSnapshot Upgrade(PluginSettingsSnapshot snapshot)
		{
			var values = (JObject)snapshot.Values.DeepClone();
			for (int version = snapshot.SchemaVersion; version < Version; version++) {
				if (!Migrations.TryGetValue(version, out var migrate)) throw new InvalidOperationException("No configuration migration from version " + version + ".");
				values = migrate(values) ?? throw new InvalidOperationException("Configuration migration returned no values.");
			}
			return new PluginSettingsSnapshot(Math.Max(snapshot.SchemaVersion, Version), values);
		}
		/// <summary>
		/// Returns the default values: every field's <see cref="PluginField.Default"/>, plus a record of child defaults for each
		/// <see cref="SettingKind.Composite"/> field. Fields without a default are absent.
		/// </summary>
		/// <returns>A new snapshot at <see cref="Version"/>.</returns>
		public PluginSettingsSnapshot Defaults()
		{
			var values = new JObject();
			foreach (var field in Fields) if (field.Default != null) values[field.Id] = field.Default.DeepClone();
			FillRecordDefaults(Fields, values);
			return new PluginSettingsSnapshot(Version, values);
		}
		internal static void FillRecordDefaults(IEnumerable<PluginField> fields, JObject values)
		{
			foreach (var field in fields) {
				if (values[field.Id] == null && field.Default != null) values[field.Id] = field.Default.DeepClone();
				if (field.Kind == SettingKind.Composite) {
					if (values[field.Id] == null) values[field.Id] = new JObject();
					if (values[field.Id] is JObject record) FillRecordDefaults(field.Children, record);
				} else if (field.Kind == SettingKind.Collection && field.Children.Count > 0 && values[field.Id] is JArray records) {
					foreach (var item in records.OfType<JObject>()) FillRecordDefaults(field.Children, item);
				}
			}
		}
		/// <summary>
		/// Checks values against the declared fields and returns every problem found.
		/// </summary>
		/// <remarks>
		/// Only visible fields are checked (see <see cref="PluginField.Visible"/>), and <see cref="SettingKind.Status"/>,
		/// <see cref="SettingKind.Action"/>, <see cref="SettingKind.Preview"/> and <see cref="SettingKind.Info"/> fields are skipped. For each field, its
		/// <see cref="PluginField.Validate"/> runs first; if it reports nothing and a value is present, the value is checked against
		/// <see cref="PluginField.Kind"/> and, for numbers, <see cref="PluginField.Minimum"/> and <see cref="PluginField.Maximum"/>. An
		/// absent value is valid. Records of composite and collection fields are checked against their <see cref="PluginField.Children"/>,
		/// and their problems are reported as one issue on the parent field.
		/// <para>
		/// Pass effective values (defaults and layers resolved), as the host does before a game starts and the settings session does on
		/// apply. Plugin-specific checks of choices, files and combinations of fields belong in
		/// <see cref="IPluginSettingsProvider.Validate"/>, which callers run in addition.
		/// </para>
		/// </remarks>
		/// <param name="snapshot">The values to check.</param>
		/// <returns>The issues found; empty when the values are valid.</returns>
		public IReadOnlyList<PluginValidationIssue> Validate(PluginSettingsSnapshot snapshot)
		{
			var issues = new List<PluginValidationIssue>();
			foreach (var field in Fields) {
				if (!field.Visible(snapshot) || !field.StoresValue) continue;
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
					if (field.Kind == SettingKind.Composite && !(value is JObject)) error = "Expected a settings record.";
					if (field.Children.Count > 0 && error == null) {
						var nested = new PluginSettingsSchema(Version, new PluginSettingsGroup(field.Label, "", field.Children.ToArray()));
						if (field.Kind == SettingKind.Composite && value is JObject record) {
							var failures = nested.Validate(new PluginSettingsSnapshot(Version, record));
							if (failures.Count > 0) error = string.Join("; ", failures.Select(issue => issue.Field + ": " + issue.Message));
						}
						if (field.Kind == SettingKind.Collection && value is JArray records) foreach (var item in records) {
							if (!(item is JObject row)) { error = "Expected a list of settings records."; break; }
							var failures = nested.Validate(new PluginSettingsSnapshot(Version, row));
							if (failures.Count > 0) { error = string.Join("; ", failures.Select(issue => issue.Field + ": " + issue.Message)); break; }
							}
						}
					if (field.Kind == SettingKind.ScreenLayout) error = ScreenLayoutError(value);
					if ((field.Kind == SettingKind.Text || field.Kind == SettingKind.File || field.Kind == SettingKind.Folder || field.Kind == SettingKind.Choice && !field.Multiple || field.Kind == SettingKind.Color) && value.Type != JTokenType.String) error = "Expected text.";
					if (field.Kind == SettingKind.Choice && field.Multiple && (!(value is JArray picked) || picked.Any(item => item.Type != JTokenType.String))) error = "Expected a list of choices.";
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
		private static string? ScreenLayoutError(JToken value)
		{
			if (!(value is JArray screens)) return "Expected a screen layout.";
			var ids = new HashSet<string>(StringComparer.Ordinal);
			foreach (var item in screens) {
				if (!(item is JObject screen) || screen["screen"] == null || screen["screen"]!.Type != JTokenType.String && screen["screen"]!.Type != JTokenType.Integer || string.IsNullOrWhiteSpace(screen["screen"]!.ToString()) || !ids.Add(screen["screen"]!.ToString())) return "Each screen needs a unique identifier.";
				foreach (string axis in new[] { "x", "y", "width", "height" }) {
					var number = screen[axis];
					if (number?.Type != JTokenType.Integer || !int.TryParse(number.ToString(), out int coordinate) || (axis == "width" || axis == "height") && (coordinate < 1 || coordinate > 16384)) return "Use integer coordinates and dimensions from 1 to 16384 pixels.";
				}
			}
			return null;
		}
	}

	/// <summary>
	/// A set of settings values keyed by <see cref="PluginField.Id"/>, together with the schema version they follow.
	/// </summary>
	/// <remarks>
	/// Snapshots are what plugins read their configuration from: <see cref="PluginGameContext.Settings"/> holds the effective values for
	/// the game being prepared, and <see cref="IPluginSynchronousReconfiguration"/> and <see cref="IPluginReconfiguration"/> receive
	/// new ones for live changes. The same type also carries a single stored layer, a draft on the settings page, or the values an
	/// action returns. Read values with the typed accessors, which never throw and return the fallback when the value is absent or of
	/// another JSON type.
	/// <para>
	/// A snapshot is a mutable JSON tree and is not thread-safe. Treat snapshots you are handed as read-only and use
	/// <see cref="Copy"/> before changing one.
	/// </para>
	/// <example>
	/// Reading settings in a plugin, with keys from PinUP's schema:
	/// <code>
	/// public override Task PrepareGameAsync(PluginGameContext game, CancellationToken cancellation)
	/// {
	///     string root = game.Settings.String("VideosRoot");
	///     int volume = game.Settings.Integer("Volume", 100);
	///     bool hardware = game.Settings.Boolean("HardwareDecoding", true);
	///     ...
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginSettingsSnapshot
	{
		/// <summary>The <see cref="PluginSettingsSchema.Version"/> the values follow.</summary>
		public int SchemaVersion { get; }
		/// <summary>
		/// The values as a JSON object, one property per field ID. This is the live tree, not a copy. Values the current schema does not
		/// know may be present.
		/// </summary>
		public JObject Values { get; }
		/// <summary>Creates a snapshot.</summary>
		/// <param name="schemaVersion">The schema version the values follow.</param>
		/// <param name="values">The values, used as is (not copied); null starts empty.</param>
		public PluginSettingsSnapshot(int schemaVersion, JObject? values = null) { SchemaVersion = schemaVersion; Values = values ?? new JObject(); }
		/// <summary>
		/// Returns the raw value of a field, or null when it is absent. The token is part of <see cref="Values"/>; do not modify it.
		/// </summary>
		/// <param name="id">The field ID.</param>
		public JToken? Value(string id) => Values[id];
		/// <summary>Returns a field's value if it is a JSON string, otherwise <paramref name="fallback"/>.</summary>
		/// <param name="id">The field ID.</param>
		/// <param name="fallback">The value to return when the field is absent or not a string.</param>
		public string String(string id, string fallback = "") => Value(id)?.Type == JTokenType.String ? Value(id)!.Value<string>() ?? fallback : fallback;
		/// <summary>Returns a field's value if it is a JSON boolean, otherwise <paramref name="fallback"/>.</summary>
		/// <param name="id">The field ID.</param>
		/// <param name="fallback">The value to return when the field is absent or not a boolean.</param>
		public bool Boolean(string id, bool fallback = false) => Value(id)?.Type == JTokenType.Boolean ? Value(id)!.Value<bool>() : fallback;
		/// <summary>
		/// Returns the strings of a field whose value is a JSON array, such as the choice IDs of a <see cref="PluginField.Multiple"/>
		/// choice; other items are skipped. Empty when the field is absent or not an array.
		/// </summary>
		/// <param name="id">The field ID.</param>
		public IReadOnlyList<string> Strings(string id) => Value(id) is JArray items
			? items.Where(item => item.Type == JTokenType.String).Select(item => item.Value<string>() ?? "").ToArray()
			: Array.Empty<string>();
		/// <summary>
		/// Returns a field's value if it is a JSON integer that fits in an <see cref="int"/>, otherwise <paramref name="fallback"/>.
		/// Floating-point values are not converted.
		/// </summary>
		/// <param name="id">The field ID.</param>
		/// <param name="fallback">The value to return when the field is absent, not an integer or out of range.</param>
		public int Integer(string id, int fallback = 0) => Value(id)?.Type == JTokenType.Integer && int.TryParse(Value(id)!.ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int number) ? number : fallback;
		/// <summary>Returns a field's value if it is a JSON integer or floating-point number, otherwise <paramref name="fallback"/>.</summary>
		/// <param name="id">The field ID.</param>
		/// <param name="fallback">The value to return when the field is absent or not a number.</param>
		public double Number(string id, double fallback = 0) => Value(id)?.Type == JTokenType.Integer || Value(id)?.Type == JTokenType.Float ? Value(id)!.Value<double>() : fallback;
		/// <summary>Returns a deep copy that can be changed without affecting this snapshot.</summary>
		public PluginSettingsSnapshot Copy() => new PluginSettingsSnapshot(SchemaVersion, (JObject)Values.DeepClone());
		/// <summary>
		/// Stores a copy of <paramref name="value"/> for a field, or removes the field when <paramref name="value"/> is null. In a game
		/// layer, removing a field means it inherits the global value; in a global layer, the default.
		/// </summary>
		/// <param name="id">The field ID.</param>
		/// <param name="value">The new value, or null to remove it.</param>
		public void Set(string id, JToken? value) { if (value == null) Values.Remove(id); else Values[id] = value.DeepClone(); }
		/// <summary>
		/// Combines layers into a new snapshot: a copy of <paramref name="defaults"/>, with each top-level value of each layer replacing
		/// the one before it. Values are replaced whole, not merged property by property.
		/// </summary>
		/// <param name="defaults">The base values; its <see cref="SchemaVersion"/> becomes the result's.</param>
		/// <param name="layers">The layers to apply in order, such as the global values and then a game's overrides.</param>
		/// <returns>A new snapshot; the inputs are not modified.</returns>
		public static PluginSettingsSnapshot Merge(PluginSettingsSnapshot defaults, params PluginSettingsSnapshot[] layers)
		{
			var merged = defaults.Copy();
			foreach (var layer in layers) foreach (var value in layer.Values.Properties()) merged.Values[value.Name] = value.Value.DeepClone();
			return merged;
		}
	}

	/// <summary>One item a <see cref="SettingKind.Choice"/> field can select, returned in a <see cref="PluginChoiceCatalog"/>.</summary>
	/// <remarks>
	/// Only the <see cref="Id"/> is saved, so it must stay stable while labels may change. PinUP lists one choice per pack folder, B2S
	/// one per <c>.directb2s</c> file it found, and <see cref="PluginResourceChoices.Displays"/> one per display of the running game.
	/// </remarks>
	public sealed class PluginChoice
	{
		/// <summary>The value stored in the field when this item is selected.</summary>
		public string Id { get; }
		/// <summary>The text shown in the picker. When several choices share a label, the Player appends their IDs.</summary>
		public string Label { get; }
		/// <summary>Optional longer text, shown as a tooltip.</summary>
		public string Description { get; }
		/// <summary>
		/// False for an item that exists but cannot be used now. The Player does not offer unavailable items, and applying settings that
		/// select one fails with <see cref="UnavailableReason"/>.
		/// </summary>
		public bool Available { get; }
		/// <summary>Why the item cannot be used, shown to the user when a saved selection points to it. Empty for available items.</summary>
		public string UnavailableReason { get; }
		/// <summary>Creates a choice.</summary>
		/// <param name="id">The stable value to store.</param>
		/// <param name="label">The text shown in the picker.</param>
		/// <param name="description">Optional tooltip text.</param>
		/// <param name="available">False when the item cannot be selected now.</param>
		/// <param name="unavailableReason">Why it cannot be selected, when <paramref name="available"/> is false.</param>
		public PluginChoice(string id, string label, string description = "", bool available = true, string unavailableReason = "")
		{ Id = id; Label = label; Description = description; Available = available; UnavailableReason = unavailableReason; }
	}

	/// <summary>The table package a game-scoped request is about, for catalogs that look next to it.</summary>
	/// <remarks>
	/// The B2S plugin, for example, looks for a <c>.directb2s</c> file next to the package and named after the table file or its
	/// folder, and compares the title with file names.
	/// </remarks>
	public sealed class PluginPackageReference
	{
		/// <summary>Full path of the table package.</summary>
		public string Path { get; }
		/// <summary>The table's display title, or empty when unknown.</summary>
		public string Title { get; }
		/// <summary>Creates a reference. Null arguments become empty strings.</summary>
		/// <param name="path">Full path of the table package.</param>
		/// <param name="title">The table's display title.</param>
		public PluginPackageReference(string path, string title) { Path = path ?? ""; Title = title ?? ""; }
	}

	/// <summary>
	/// The context passed to an <see cref="IPluginSettingsProvider"/>: which game and table are being configured, the settings to
	/// work with, the platform and the resources that are currently published.
	/// </summary>
	/// <remarks>
	/// The settings session builds a request for every catalog lookup, action and validation; the host builds one to validate a
	/// plugin's settings before a game starts. Providers only read it.
	/// </remarks>
	public sealed class PluginChoiceRequest
	{
		/// <summary>The game being configured, or null when global settings are edited.</summary>
		public GameIdentity? Game { get; }
		/// <summary>The selected table package, when the request concerns one; null for global settings.</summary>
		public PluginPackageReference? Package { get; set; }
		/// <summary>Optional progress sink for a user-requested action. Report from any thread; the caller owns dispatch.</summary>
		public IProgress<PluginActionProgress>? Progress { get; set; }
		/// <summary>
		/// The effective settings to work with, including unsaved edits from the settings page. Use these, not the saved values, so that
		/// catalogs follow what the user just changed (for example PinUP lists the packs of the library folder in the draft).
		/// </summary>
		public PluginSettingsSnapshot Settings { get; }
		/// <summary>The platform ID: <c>windows</c>, <c>macos</c> or <c>linux</c> in the Player.</summary>
		public string Platform { get; }
		/// <summary>
		/// The resources published in the plugin resource registry at the time of the request, such as the displays of a running game.
		/// Filled for catalog lookups and the check made when settings are applied; empty for actions, for validation and when nothing
		/// runs. Never null.
		/// </summary>
		public IReadOnlyList<PluginResourceDescriptor> Resources { get; }
		/// <summary>Creates a request.</summary>
		/// <param name="game">The game being configured, or null for global settings.</param>
		/// <param name="settings">The effective settings to work with.</param>
		/// <param name="platform">The platform ID.</param>
		/// <param name="resources">The currently published resources, or null for none.</param>
		public PluginChoiceRequest(GameIdentity? game, PluginSettingsSnapshot settings, string platform, IReadOnlyList<PluginResourceDescriptor>? resources = null)
		{ Game = game; Settings = settings; Platform = platform; Resources = resources ?? Array.Empty<PluginResourceDescriptor>(); }
	}

	/// <summary>The items a <see cref="SettingKind.Choice"/> or <see cref="SettingKind.ScreenLayout"/> field can select right now.</summary>
	/// <remarks>
	/// Returned by <see cref="IPluginSettingsProvider.DiscoverAsync"/>. The Player fills the picker from it, and the settings session
	/// checks each saved selection against it before applying settings.
	/// </remarks>
	public sealed class PluginChoiceCatalog
	{
		/// <summary>
		/// An opaque string that changes when the list changes. Providers in this codebase join the choice IDs with <c>|</c>. The host does
		/// not interpret it.
		/// </summary>
		public string Revision { get; }
		/// <summary>The items in display order, including unavailable ones.</summary>
		public IReadOnlyList<PluginChoice> Choices { get; }
		/// <summary>Explains how to get choices when none are available; shown instead of an empty selector.</summary>
		public string EmptyMessage { get; set; } = "";
		/// <summary>Optional guidance when selectable items exist, replacing empty-source instructions during launch setup.</summary>
		public string AvailableMessage { get; set; } = "";
		/// <summary>Explains a saved choice absent from an incomplete catalog, if the default offline-source message does not apply.</summary>
		public string MissingSelectionMessage { get; set; } = "";
		/// <summary>Optional HTTPS page where the user can obtain the items this catalog requires.</summary>
		public string SourceUrl { get; set; } = "";
		/// <summary>Label for the optional source-page button.</summary>
		public string SourceLabel { get; set; } = "Open download page";
		/// <summary>False when a runtime-only catalog cannot be inspected while its game is stopped.</summary>
		/// <remarks>
		/// A saved selection missing from an incomplete catalog is kept and not reported as an error. <see cref="PluginResourceChoices.Displays"/>
		/// returns an incomplete catalog when the game's controller is not running, because its displays are unknown then.
		/// </remarks>
		public bool IsComplete { get; }
		/// <summary>Creates a catalog.</summary>
		/// <param name="revision">An opaque string that identifies this version of the list.</param>
		/// <param name="choices">The items in display order.</param>
		/// <param name="isComplete">False when the list may lack items that exist only while the game runs.</param>
		public PluginChoiceCatalog(string revision, IReadOnlyList<PluginChoice> choices, bool isComplete = true) { Revision = revision; Choices = choices; IsComplete = isComplete; }
		/// <summary>Returns the first item with the given ID, or null when the catalog has none.</summary>
		/// <param name="id">The <see cref="PluginChoice.Id"/> to look for.</param>
		public PluginChoice? Find(string id) => Choices.FirstOrDefault(choice => choice.Id == id);
	}

	/// <summary>Progress of a settings action, with a fraction from zero to one and a user-facing message.</summary>
	public readonly struct PluginActionProgress
	{
		public double Fraction { get; }
		public string Message { get; }
		public PluginActionProgress(double fraction, string message) { Fraction = fraction; Message = message; }
	}

	/// <summary>The outcome of a settings action, a reconfiguration or a settings commit.</summary>
	/// <remarks>
	/// Plugins return it from <see cref="IPluginSettingsProvider.RunActionAsync"/>, <see cref="IPluginReconfiguration.ApplyAsync"/> and
	/// <see cref="IPluginSynchronousReconfiguration.Apply"/>. The host and the settings session return it when they save settings.
	/// </remarks>
	public readonly struct PluginActionResult
	{
		/// <summary>Whether the operation succeeded. A failed reconfiguration must leave the plugin as it was.</summary>
		public bool Success { get; }
		/// <summary>A short message for the user, shown on the settings page in both cases.</summary>
		public string Message { get; }
		/// <summary>
		/// For an action: values to put into the settings draft when <see cref="Success"/> is true, or null for none. Only fields
		/// editable in the current scope are taken, and the user still has to apply them. Ignored in other results.
		/// </summary>
		public PluginSettingsSnapshot? Changes { get; }
		/// <summary>Creates a result.</summary>
		/// <param name="success">Whether the operation succeeded.</param>
		/// <param name="message">A short message for the user.</param>
		/// <param name="changes">For an action, values to put into the draft; otherwise null.</param>
		public PluginActionResult(bool success, string message, PluginSettingsSnapshot? changes = null) { Success = success; Message = message; Changes = changes; }
	}

	/// <summary>
	/// Implemented by plugins whose settings need dynamic content: catalogs for choice fields, buttons that run actions, and checks
	/// that the schema alone cannot express.
	/// </summary>
	/// <remarks>
	/// A factory returns it from <see cref="IPlayerPluginFactory.SettingsProvider"/>, or null when the schema is enough (PinMAME's
	/// factory returns null). The PinUP and B2S factories implement it themselves and return <c>this</c>. Fields name what they need
	/// through <see cref="PluginField.Provider"/>; for example PinUP's <c>Pack</c> field asks for the <c>packs</c> catalog, which lists
	/// the pack folders of the configured library.
	/// <para>
	/// The provider is used while no plugin instance exists, so it must not rely on runtime state other than what the request carries.
	/// Calls can arrive on the main thread or on a worker thread: do not touch Unity objects, and move file system work off the
	/// calling thread, as PinUP does with <c>Task.Run</c>.
	/// </para>
	/// <example>
	/// <code>
	/// public Task&lt;PluginChoiceCatalog&gt; DiscoverAsync(string provider, PluginChoiceRequest request, CancellationToken cancellation)
	///     =&gt; Task.Run(() =&gt; {
	///         var choices = new List&lt;PluginChoice&gt; { new PluginChoice("auto", "Automatic") };
	///         string root = request.Settings.String("Library");
	///         if (provider == "items" &amp;&amp; Directory.Exists(root))
	///             foreach (string name in Directory.EnumerateDirectories(root).Select(Path.GetFileName))
	///                 choices.Add(new PluginChoice(name, name));
	///         return new PluginChoiceCatalog(string.Join("|", choices.Select(choice =&gt; choice.Id)), choices);
	///     }, cancellation);
	///
	/// public Task&lt;PluginActionResult&gt; RunActionAsync(string action, PluginChoiceRequest request, CancellationToken cancellation)
	///     =&gt; Task.FromResult(new PluginActionResult(false, "Unknown action."));
	///
	/// public IReadOnlyList&lt;PluginValidationIssue&gt; Validate(PluginChoiceRequest request)
	///     =&gt; Directory.Exists(request.Settings.String("Library")) ? Array.Empty&lt;PluginValidationIssue&gt;()
	///         : new[] { new PluginValidationIssue("Library", "This folder does not exist.") };
	/// </code>
	/// </example>
	/// </remarks>
	public interface IPluginSettingsProvider
	{
		/// <summary>
		/// Lists the items for a <see cref="SettingKind.Choice"/> or <see cref="SettingKind.ScreenLayout"/> field. Called when the settings
		/// page shows the field, and again when settings are applied to check the saved selections.
		/// </summary>
		/// <param name="provider">The field's <see cref="PluginField.Provider"/>.</param>
		/// <param name="request">The game, table, draft settings and published resources to list items for.</param>
		/// <param name="cancellation">
		/// Cancelled when the page is refreshed or closed or the apply is cancelled; stop work and throw
		/// <see cref="OperationCanceledException"/>.
		/// </param>
		/// <returns>The catalog; mark it incomplete when items may exist that cannot be seen now.</returns>
		Task<PluginChoiceCatalog> DiscoverAsync(string provider, PluginChoiceRequest request, CancellationToken cancellation);
		/// <summary>
		/// Runs the action of a <see cref="SettingKind.Action"/> field when the user presses its button, such as PinUP locating an
		/// installed LibVLC or B2S rescanning its folders.
		/// </summary>
		/// <param name="action">The field's <see cref="PluginField.Provider"/>.</param>
		/// <param name="request">The game, table and draft settings; <see cref="PluginChoiceRequest.Resources"/> is empty.</param>
		/// <param name="cancellation">Cancelled when the page is refreshed or closed.</param>
		/// <returns>
		/// The result to show; on success, <see cref="PluginActionResult.Changes"/> may carry values for the draft, which the user then
		/// applies.
		/// </returns>
		Task<PluginActionResult> RunActionAsync(string action, PluginChoiceRequest request, CancellationToken cancellation);
		/// <summary>
		/// Checks settings beyond what the schema checks, such as folders that must exist or fields that depend on each other. Called
		/// with the effective settings when the user applies them and when the host prepares a game; issues block saving and keep the
		/// plugin from starting that game. In the Player it runs synchronously on the main thread, so keep it fast.
		/// </summary>
		/// <param name="request">The game, table and effective settings to check.</param>
		/// <returns>The issues found; empty when the settings are valid.</returns>
		IReadOnlyList<PluginValidationIssue> Validate(PluginChoiceRequest request);
	}

	/// <summary>How the Player marks a <see cref="PluginInfo"/>.</summary>
	public enum PluginInfoTone
	{
		/// <summary>Plain information.</summary>
		Neutral,
		/// <summary>Everything is in place, such as an active configuration without warnings.</summary>
		Good,
		/// <summary>Works, but needs attention, such as an import with warnings.</summary>
		Warning,
		/// <summary>Does not work until the user acts, such as no configuration at all.</summary>
		Problem
	}

	/// <summary>What an <see cref="SettingKind.Info"/> field shows: a headline, its tone and label/value lines.</summary>
	public sealed class PluginInfo
	{
		/// <summary>The headline, such as the active configuration and when it was imported.</summary>
		public string Summary { get; }
		/// <summary>How the Player marks the headline.</summary>
		public PluginInfoTone Tone { get; }
		/// <summary>The details as label/value pairs, in order; may be empty.</summary>
		public IReadOnlyList<KeyValuePair<string, string>> Lines { get; }
		/// <summary>
		/// The ids of the actions attached to the field (<see cref="PluginField.AttachTo"/>) that have nothing to act on in this
		/// state, such as undo without a previous configuration. The Player shows their buttons disabled; other ids are ignored.
		/// </summary>
		public IReadOnlyCollection<string> UnavailableActions { get; }
		/// <summary>Creates the information of an <see cref="SettingKind.Info"/> field.</summary>
		/// <param name="summary">The headline.</param>
		/// <param name="tone">How the headline is marked.</param>
		/// <param name="lines">The label/value lines, or null for none.</param>
		/// <param name="unavailableActions">The ids of the attached actions that have nothing to act on, or null for none.</param>
		public PluginInfo(string summary, PluginInfoTone tone = PluginInfoTone.Neutral, IReadOnlyList<KeyValuePair<string, string>>? lines = null, IReadOnlyCollection<string>? unavailableActions = null)
		{
			Summary = summary ?? "";
			Tone = tone;
			Lines = lines ?? Array.Empty<KeyValuePair<string, string>>();
			UnavailableActions = unavailableActions ?? Array.Empty<string>();
		}
	}

	/// <summary>
	/// Implemented, in addition to <see cref="IPluginSettingsProvider"/>, by a settings provider whose schema has
	/// <see cref="SettingKind.Info"/> fields.
	/// </summary>
	/// <remarks>
	/// The same rules as for <see cref="IPluginSettingsProvider"/> apply: no plugin instance may exist, calls can come from any thread,
	/// and file system work belongs off the calling thread. The Player asks again each time it draws the page, after every action
	/// among others, so keep it quick: cache what is slow to find, such as connected devices, and refresh it in an action.
	/// </remarks>
	public interface IPluginSettingsInfoProvider
	{
		/// <summary>Works out what an <see cref="SettingKind.Info"/> field shows.</summary>
		/// <param name="provider">The field's <see cref="PluginField.Provider"/>.</param>
		/// <param name="request">The game, table, draft settings and published resources.</param>
		/// <param name="cancellation">Cancelled when the page is redrawn or closed; stop work and throw <see cref="OperationCanceledException"/>.</param>
		/// <returns>The information to show.</returns>
		Task<PluginInfo> DescribeAsync(string provider, PluginChoiceRequest request, CancellationToken cancellation);
	}

	/// <summary>The colour an output drives of an RGB toy, which takes three outputs in a row.</summary>
	public enum PluginOutputChannel
	{
		/// <summary>An output of its own, such as a solenoid or a single-colour light, or nothing.</summary>
		None,
		Red,
		Green,
		Blue
	}

	/// <summary>One output of a <see cref="PluginOutputBoard"/>, such as a port of a LedWiz.</summary>
	public sealed class PluginOutput
	{
		/// <summary>
		/// What <see cref="IPluginOutputMapProvider.TestOutputAsync"/> gets to test this output, unique in its map. Private to the plugin.
		/// </summary>
		public string Id { get; }
		/// <summary>The output's number on its board, as the board counts them (usually from 1).</summary>
		public int Number { get; }
		/// <summary>
		/// What is wired to it, such as <c>Flipper Left</c>, or empty when nothing is. Of an RGB toy, the first output names the toy
		/// and the other two may leave it empty.
		/// </summary>
		public string Label { get; }
		/// <summary>The colour it drives of an RGB toy, or <see cref="PluginOutputChannel.None"/>.</summary>
		public PluginOutputChannel Channel { get; }
		/// <summary>Creates an output.</summary>
		/// <param name="id">What the test gets; see <see cref="Id"/>.</param>
		/// <param name="number">The number on its board.</param>
		/// <param name="label">What is wired to it; null or empty for nothing.</param>
		/// <param name="channel">The colour it drives of an RGB toy.</param>
		public PluginOutput(string id, int number, string label, PluginOutputChannel channel = PluginOutputChannel.None)
		{ Id = id ?? ""; Number = number; Label = label ?? ""; Channel = channel; }
	}

	/// <summary>A board of a <see cref="PluginOutputMap"/> and its outputs.</summary>
	public sealed class PluginOutputBoard
	{
		/// <summary>The board's name, such as <c>LedWiz 1</c>.</summary>
		public string Title { get; }
		/// <summary>Where it is set up or how it is connected, such as the file that configures it; may be empty.</summary>
		public string Detail { get; }
		/// <summary>The outputs in board order.</summary>
		public IReadOnlyList<PluginOutput> Outputs { get; }
		/// <summary>Creates a board.</summary>
		/// <param name="title">The board's name.</param>
		/// <param name="detail">Where it is set up or how it is connected, or null for nothing.</param>
		/// <param name="outputs">The outputs in board order.</param>
		public PluginOutputBoard(string title, string detail, IReadOnlyList<PluginOutput> outputs)
		{ Title = title ?? ""; Detail = detail ?? ""; Outputs = outputs ?? Array.Empty<PluginOutput>(); }
	}

	/// <summary>What an <see cref="SettingKind.Outputs"/> field shows: the boards, and whether their outputs can be tested now.</summary>
	public sealed class PluginOutputMap
	{
		/// <summary>The boards in display order; may be empty.</summary>
		public IReadOnlyList<PluginOutputBoard> Boards { get; }
		/// <summary>Shown instead of the grid when there are no boards, such as how to get a configuration that names them.</summary>
		public string EmptyMessage { get; }
		/// <summary>
		/// Why no output can be tested now, such as a game that holds the boards; empty when they can. The Player shows it above the
		/// grid and turns the test buttons off.
		/// </summary>
		public string TestUnavailableReason { get; }
		/// <summary>Creates the map of an <see cref="SettingKind.Outputs"/> field.</summary>
		/// <param name="boards">The boards in display order.</param>
		/// <param name="emptyMessage">Shown when there are no boards.</param>
		/// <param name="testUnavailableReason">Why nothing can be tested now, or null when it can.</param>
		/// <exception cref="ArgumentException">Two outputs have the same <see cref="PluginOutput.Id"/>, so a test could not tell them apart.</exception>
		public PluginOutputMap(IReadOnlyList<PluginOutputBoard> boards, string emptyMessage = "", string testUnavailableReason = "")
		{
			Boards = boards ?? Array.Empty<PluginOutputBoard>(); EmptyMessage = emptyMessage ?? ""; TestUnavailableReason = testUnavailableReason ?? "";
			var ids = new HashSet<string>(StringComparer.Ordinal);
			foreach (var board in Boards)
				foreach (var output in board.Outputs)
					if (!ids.Add(output.Id)) throw new ArgumentException($"Output {output.Id} is in the map more than once.", nameof(boards));
		}
	}

	/// <summary>
	/// Implemented, in addition to <see cref="IPluginSettingsProvider"/>, by a settings provider whose schema has
	/// <see cref="SettingKind.Outputs"/> fields.
	/// </summary>
	/// <remarks>
	/// The same rules as for <see cref="IPluginSettingsProvider"/> apply: no plugin instance may exist, calls can come from any thread,
	/// and file system or hardware work belongs off the calling thread. The Player runs one test at a time per plugin.
	/// </remarks>
	public interface IPluginOutputMapProvider
	{
		/// <summary>Lists the boards and outputs an <see cref="SettingKind.Outputs"/> field shows.</summary>
		/// <param name="provider">The field's <see cref="PluginField.Provider"/>.</param>
		/// <param name="request">The game, table, draft settings and published resources.</param>
		/// <param name="cancellation">Cancelled when the page is redrawn or closed; stop work and throw <see cref="OperationCanceledException"/>.</param>
		/// <returns>The map to show.</returns>
		Task<PluginOutputMap> DescribeOutputsAsync(string provider, PluginChoiceRequest request, CancellationToken cancellation);
		/// <summary>
		/// Tests one output, such as turning it on for a moment, and says how it went. Returns when the test is over, so the Player can
		/// show the output as tested until then.
		/// </summary>
		/// <param name="provider">The field's <see cref="PluginField.Provider"/>.</param>
		/// <param name="output">The <see cref="PluginOutput.Id"/> of the output.</param>
		/// <param name="request">The game, table and draft settings; <see cref="PluginChoiceRequest.Resources"/> is empty.</param>
		/// <param name="cancellation">Cancelled when the page is closed; leave the output off and throw <see cref="OperationCanceledException"/>.</param>
		/// <returns>The result to show; <see cref="PluginActionResult.Changes"/> is ignored.</returns>
		Task<PluginActionResult> TestOutputAsync(string provider, string output, PluginChoiceRequest request, CancellationToken cancellation);
	}
}
