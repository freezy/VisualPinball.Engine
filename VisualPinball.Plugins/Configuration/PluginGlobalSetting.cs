// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// How the Player draws the editor of a numeric field (<see cref="SettingKind.Integer"/> or <see cref="SettingKind.Number"/>).
	/// </summary>
	/// <remarks>
	/// Set through <see cref="PluginField.Presentation"/>. Other kinds ignore it. Volume fields shown on the global volume mixer use
	/// <see cref="Slider"/>, as the PinMAME and PinUP volume settings do.
	/// </remarks>
	public enum PluginFieldPresentation
	{
		/// <summary>A plain number box. This is the default.</summary>
		Input,
		/// <summary>
		/// A slider with step buttons between <see cref="PluginField.Minimum"/> and <see cref="PluginField.Maximum"/>, with the value and
		/// <see cref="PluginField.Unit"/> printed next to it. Give the field both bounds when you use it.
		/// </summary>
		Slider
	}
	/// <summary>
	/// Identifiers of the shared Player pages that plugin fields can appear on through <see cref="PluginGlobalSetting"/>.
	/// </summary>
	/// <remarks>
	/// A shared page collects one kind of setting from every installed plugin, so the user can adjust them in one place instead of
	/// visiting each plugin's settings page.
	/// </remarks>
	public static class PluginGlobalSections
	{
		/// <summary>
		/// The Player's global volume mixer, which lists the table's mechanical sounds next to every plugin's volume. Fields exposed here
		/// must be <see cref="SettingKind.Integer"/> or <see cref="SettingKind.Number"/> with finite <see cref="PluginField.Minimum"/> and
		/// <see cref="PluginField.Maximum"/> (minimum below maximum); <see cref="PluginGlobalSettingBinding.Discover"/> skips any other field
		/// with a diagnostic. PinMAME's ROM volume and PinUP's media volume appear here.
		/// </summary>
		public const string VolumeMixer = "audio.mixer";
	}
	/// <summary>
	/// Asks the Player to also show a plugin field on a shared page, such as the global volume mixer, in addition to the plugin's own
	/// settings page.
	/// </summary>
	/// <remarks>
	/// The value is not copied: the shared page reads and writes the same global value of the same field in the plugin's own
	/// configuration (see <see cref="PluginGlobalSettingBinding"/>). Only the global value is shown there, so the field must not use
	/// <see cref="ConfigurationScope.GameOnly"/>. Combine it with <see cref="ConfigurationChange.Live"/> so that moving the mixer slider
	/// is heard immediately in a running game.
	/// <example>
	/// The PinMAME integration puts its ROM volume on the mixer:
	/// <code>
	/// new PluginField("Volume", "Volume", SettingKind.Integer) {
	///     Default = 100, Minimum = 0, Maximum = 100, Unit = "%",
	///     Change = ConfigurationChange.Live, Presentation = PluginFieldPresentation.Slider,
	///     GlobalSetting = new PluginGlobalSetting(PluginGlobalSections.VolumeMixer) { Order = 20 }
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginGlobalSetting
	{
		/// <summary>The shared page the field appears on, one of the <see cref="PluginGlobalSections"/> constants.</summary>
		public string Section { get; }
		/// <summary>
		/// Label shown on the shared page. Empty (the default) uses the plugin's <see cref="PluginDescriptor.Name"/>, which reads better
		/// on the mixer than a generic field label such as "Volume".
		/// </summary>
		public string Label { get; set; } = "";
		/// <summary>
		/// Sort key on the shared page; lower values come first, ties are sorted by <see cref="PluginGlobalSettingBinding.Id"/>. Defaults
		/// to 0. PinMAME uses 20 and PinUP 30.
		/// </summary>
		public int Order { get; set; }
		/// <summary>Creates an exposure on the given shared page.</summary>
		/// <param name="section">One of the <see cref="PluginGlobalSections"/> constants.</param>
		/// <exception cref="ArgumentNullException"><paramref name="section"/> is null.</exception>
		public PluginGlobalSetting(string section) => Section = section ?? throw new ArgumentNullException(nameof(section));
	}
	/// <summary>An alias identifies a field in its owning factory, never a second copy of its value.</summary>
	/// <remarks>
	/// One binding connects an entry on a shared Player page to the plugin field that declared a <see cref="PluginGlobalSetting"/>.
	/// The Player's volume mixer builds its sliders from <see cref="Discover"/>, reads each value from the plugin's effective global
	/// settings, and writes edits back with <see cref="PluginHost.CommitGlobalSettingAsync"/>, which saves the plugin's global value
	/// and, for <see cref="ConfigurationChange.Live"/> fields, applies it to the running plugin. Plugins never create bindings; they only
	/// declare <see cref="PluginField.GlobalSetting"/>.
	/// </remarks>
	public sealed class PluginGlobalSettingBinding
	{
		/// <summary>The plugin that owns and stores the field.</summary>
		public IPlayerPluginFactory Factory { get; }
		/// <summary>The exposed field. Its <see cref="PluginField.GlobalSetting"/> is never null.</summary>
		public PluginField Field { get; }
		/// <summary>
		/// Stable key of this entry, <c>pluginId/fieldId</c> (for example <c>org.visualpinball.pinup/Volume</c>). Unique across plugins;
		/// the Player keys its pending writes and controls by it.
		/// </summary>
		public string Id => Factory.Descriptor.Id + "/" + Field.Id;
		/// <summary>
		/// Text shown on the shared page: <see cref="PluginGlobalSetting.Label"/>, or the plugin's <see cref="PluginDescriptor.Name"/>
		/// when that is empty.
		/// </summary>
		public string Label => Field.GlobalSetting!.Label.Length == 0 ? Factory.Descriptor.Name : Field.GlobalSetting.Label;
		internal PluginGlobalSettingBinding(IPlayerPluginFactory factory, PluginField field) { Factory = factory; Field = field; }
		/// <summary>
		/// Finds every field the given plugins expose on a shared page, in display order.
		/// </summary>
		/// <remarks>
		/// Plugins that do not run on <paramref name="platform"/> are skipped. A field is included only when it is visible under the
		/// plugin's effective global settings (see <see cref="PluginField.Visible"/>). A field with <see cref="ConfigurationScope.GameOnly"/>,
		/// or a field on <see cref="PluginGlobalSections.VolumeMixer"/> that is not a bounded number, is reported to
		/// <paramref name="diagnostic"/> and skipped. A plugin whose saved settings cannot be read is reported and skipped as well, so one
		/// broken file does not empty the page. The result is sorted by <see cref="PluginGlobalSetting.Order"/>, then by
		/// <see cref="Id"/> (ordinal).
		/// <para>
		/// This reads configuration files, so the Player runs it on a worker thread.
		/// </para>
		/// </remarks>
		/// <param name="factories">The installed plugin factories, for example <see cref="PluginHost.Factories"/>.</param>
		/// <param name="store">The store the plugins' settings are read from.</param>
		/// <param name="platform">
		/// The current platform ID (<c>windows</c>, <c>macos</c>, <c>linux</c>), matched against <see cref="PluginDescriptor.Platforms"/>.
		/// </param>
		/// <param name="section">The shared page, one of the <see cref="PluginGlobalSections"/> constants.</param>
		/// <param name="diagnostic">Optional receiver of messages about skipped plugins and invalid exposures.</param>
		/// <returns>The bindings to show, possibly empty.</returns>
		public static IReadOnlyList<PluginGlobalSettingBinding> Discover(IEnumerable<IPlayerPluginFactory> factories, PluginConfigurationStore store, string platform, string section, Action<string>? diagnostic = null)
		{
			var bindings = new List<PluginGlobalSettingBinding>();
			foreach (var factory in factories) {
				if (factory.Descriptor.Platforms.Count > 0 && !factory.Descriptor.Platforms.Contains(platform)) continue;
				var fields = factory.Schema.Fields.Where(field => field.GlobalSetting?.Section == section).ToArray();
				if (fields.Length == 0) continue;
				PluginSettingsSnapshot effective;
				try { effective = store.Effective(factory.Descriptor, factory.Schema); }
				catch (Exception error) { diagnostic?.Invoke(factory.Descriptor.Name + ": " + error.Message); continue; }
				foreach (var field in fields) {
					if (!field.Visible(effective)) continue;
					if (field.Scope == ConfigurationScope.GameOnly || section == PluginGlobalSections.VolumeMixer && !BoundedNumber(field)) {
						diagnostic?.Invoke("Invalid global setting exposure: " + factory.Descriptor.Id + "/" + field.Id); continue;
					}
					bindings.Add(new PluginGlobalSettingBinding(factory, field));
				}
			}
			return bindings.OrderBy(binding => binding.Field.GlobalSetting!.Order).ThenBy(binding => binding.Id, StringComparer.Ordinal).ToArray();
		}
		private static bool BoundedNumber(PluginField field) => (field.Kind == SettingKind.Integer || field.Kind == SettingKind.Number)
			&& field.Minimum.HasValue && field.Maximum.HasValue && double.IsFinite(field.Minimum.Value) && double.IsFinite(field.Maximum.Value) && field.Minimum < field.Maximum;
	}
}
