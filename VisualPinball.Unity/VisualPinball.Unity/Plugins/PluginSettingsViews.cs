// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	public sealed class PluginSettingsViewContext
	{
		public PluginSettingsSession Session { get; }
		public PluginField Field { get; }
		public PluginHost Host { get; }
		public Action Changed { get; }
		public PluginSettingsViewContext(PluginSettingsSession session, PluginField field, PluginHost host, Action changed)
		{ Session = session; Field = field; Host = host; Changed = changed; }
	}
	public interface IPluginSettingsViewFactory
	{
		string PluginId { get; }
		string WidgetId { get; }
		IDisposable Build(VisualElement parent, PluginSettingsViewContext context);
	}
	public readonly struct PluginSettingsNavigationItem
	{
		public VisualElement Element { get; }
		public Action Activate { get; }
		public PluginSettingsNavigationItem(VisualElement element, Action activate) { Element = element; Activate = activate; }
	}
	public interface IPluginSettingsNavigation
	{
		IReadOnlyList<PluginSettingsNavigationItem> Navigation { get; }
	}
	public static class PluginSettingsViews
	{
		private static readonly Dictionary<string, IPluginSettingsViewFactory> Factories = new Dictionary<string, IPluginSettingsViewFactory>(StringComparer.Ordinal);
		public static void Reset() => Factories.Clear();
		public static void Register(IPluginSettingsViewFactory factory) => Factories.Add(factory.PluginId + "/" + factory.WidgetId, factory);
		public static IPluginSettingsViewFactory Find(string pluginId, string widgetId) => Factories.TryGetValue(pluginId + "/" + widgetId, out var factory) ? factory : null;
	}
}
