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
	public enum PluginFieldPresentation { Input, Slider }
	public static class PluginGlobalSections { public const string VolumeMixer = "audio.mixer"; }
	public sealed class PluginGlobalSetting
	{
		public string Section { get; }
		public string Label { get; set; } = "";
		public int Order { get; set; }
		public PluginGlobalSetting(string section) => Section = section ?? throw new ArgumentNullException(nameof(section));
	}
	/// <summary>An alias identifies a field in its owning factory, never a second copy of its value.</summary>
	public sealed class PluginGlobalSettingBinding
	{
		public IPlayerPluginFactory Factory { get; }
		public PluginField Field { get; }
		public string Id => Factory.Descriptor.Id + "/" + Field.Id;
		public string Label => Field.GlobalSetting!.Label.Length == 0 ? Factory.Descriptor.Name : Field.GlobalSetting.Label;
		internal PluginGlobalSettingBinding(IPlayerPluginFactory factory, PluginField field) { Factory = factory; Field = field; }
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
