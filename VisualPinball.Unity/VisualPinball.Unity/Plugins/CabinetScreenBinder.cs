// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Shows the surfaces plugins publish on the table's <see cref="CabinetScreenComponent"/>s while a game runs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every cabinet screen follows the <see cref="ResourceKind.Surface"/> published for its role. When several plugins
	/// publish the same role, the one listed first in the provider order wins; providers not listed come after, by ID.
	/// The surface's texture is written into the screen's material with a property block, and the screen's hide root is
	/// activated. Without a surface, or before its texture exists, the material's own texture is put back and the hide
	/// root is deactivated, so screens without a provider are hidden from the start when the binder is created before the
	/// table is activated. The property block of the screen's material slot belongs to the screen: it takes the place of
	/// renderer-wide overrides while a surface is shown, and is removed when the surface goes away.
	/// </para>
	/// <para>
	/// The game's own DMD takes precedence: a <see cref="SurfaceRoles.Dmd"/> screen only shows a surface once the gamelogic
	/// engine has started without announcing a DMD, and drops it again if the engine announces one later. Until then, and
	/// while the engine's DMD is shown, its hide root stays active, since the engine's display component may live under it.
	/// </para>
	/// <para>Not thread-safe: create, use and dispose it on the main thread, where plugins publish their surfaces.</para>
	/// </remarks>
	public sealed class CabinetScreenBinder : IDisposable
	{
		/// <summary>
		/// How long after the gamelogic engine started a DMD it announces still counts as the engine's own, in seconds.
		/// Engines such as PinMAME announce their displays right after starting.
		/// </summary>
		private const float DmdAnnouncementGrace = 0.5f;

		private static readonly string[] UnlitProperties = { "_UnlitColorMap" };
		private static readonly string[] BaseProperties = { "_BaseColorMap", "_BaseMap", "_MainTex" };

		private sealed class Screen
		{
			internal readonly CabinetScreenComponent Component;
			internal readonly Renderer Renderer;
			internal readonly int MaterialIndex;
			internal readonly string Role;
			internal readonly int Property;
			internal readonly GameObject HideRoot;
			internal bool Applied;
			internal PluginResourceBinding<IUnityTextureResource> Binding;
			internal IDisposable Connection;
			internal string ResourceId;

			internal Screen(CabinetScreenComponent component, Renderer renderer, int property)
			{
				Component = component;
				Renderer = renderer;
				MaterialIndex = component.MaterialIndex;
				Role = component.RoleName;
				Property = property;
				HideRoot = component.HideRoot;
			}
		}

		private readonly List<Screen> _screens = new List<Screen>();
		private readonly IGamelogicEngine _engine;
		private readonly PluginResourceRegistry _registry;
		private readonly string _controller;
		private readonly long _session;
		private readonly IReadOnlyList<string> _providers;
		private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
		private readonly IDisposable _observation;
		private bool _engineDmd;
		private bool _dmdDecided;
		private bool _disposed;

		/// <summary>
		/// Starts showing surfaces on the cabinet screens of <paramref name="table"/>, including inactive ones.
		/// </summary>
		/// <param name="table">The table's root object.</param>
		/// <param name="engine">The table's gamelogic engine, or null if it has none.</param>
		/// <param name="registry">The registry plugins publish their surfaces to.</param>
		/// <param name="controller">The game's controller ID; only surfaces of this controller are shown.</param>
		/// <param name="session">The game session; only surfaces of this session are shown.</param>
		/// <param name="providers">Plugin IDs in order of preference, for roles several plugins provide.</param>
		public CabinetScreenBinder(GameObject table, IGamelogicEngine engine, PluginResourceRegistry registry, string controller, long session, IReadOnlyList<string> providers)
		{
			_engine = engine;
			_registry = registry;
			_controller = controller ?? string.Empty;
			_session = session;
			_providers = providers?.ToArray() ?? Array.Empty<string>();

			foreach (var component in table.GetComponentsInChildren<CabinetScreenComponent>(true)) {
				var renderer = component.TargetRenderer;
				if (renderer == null) {
					Debug.LogWarning($"Cabinet screen \"{component.name}\" has no renderer to show the screen on.");
					continue;
				}
				if (string.IsNullOrEmpty(component.RoleName)) {
					Debug.LogWarning($"Cabinet screen \"{component.name}\" has no role.");
					continue;
				}
				var materials = renderer.sharedMaterials;
				var material = component.MaterialIndex >= 0 && component.MaterialIndex < materials.Length ? materials[component.MaterialIndex] : null;
				var property = TextureProperty(material, component.TextureProperty);
				if (property == null) {
					Debug.LogWarning($"Cabinet screen \"{component.name}\": material slot {component.MaterialIndex} of \"{renderer.name}\" has no texture property to show the screen in.");
					continue;
				}
				_screens.Add(new Screen(component, renderer, Shader.PropertyToID(property)));
			}
			if (_screens.Count == 0) {
				return;
			}

			_dmdDecided = engine == null;
			if (engine != null) {
				engine.OnDisplaysRequested += DisplaysRequested;
				engine.OnStarted += EngineStarted;
			}
			_observation = registry.Observe(RegistryChanged);
			foreach (var screen in _screens) {
				Present(screen, null);
			}
			Refresh();
		}

		/// <summary>
		/// Picks the texture property of a material: the given one if the material has it, otherwise the unlit color map, the
		/// emissive color map if the material samples it, or the base color map.
		/// </summary>
		public static string TextureProperty(Material material, string requested)
		{
			if (material == null) {
				return null;
			}
			if (!string.IsNullOrWhiteSpace(requested)) {
				return material.HasProperty(requested.Trim()) ? requested.Trim() : null;
			}
			foreach (var property in UnlitProperties) {
				if (material.HasProperty(property)) {
					return property;
				}
			}
			if (material.HasProperty("_EmissiveColorMap") && material.IsKeywordEnabled("_EMISSIVE_COLOR_MAP")) {
				return "_EmissiveColorMap";
			}
			foreach (var property in BaseProperties) {
				if (material.HasProperty(property)) {
					return property;
				}
			}
			return null;
		}

		private void RegistryChanged(PluginResourceChange change)
		{
			if (change.Descriptor.Kind == ResourceKind.Surface && change.Kind != ResourceChangeKind.Changed) {
				Refresh();
			}
		}

		private void DisplaysRequested(object sender, RequestedDisplays displays)
		{
			if (_engineDmd || displays?.Displays == null || !displays.Displays.Any(display => display != null && display.Height > 1)) {
				return;
			}
			_engineDmd = true;
			Refresh();
		}

		private async void EngineStarted(object sender, EventArgs args)
		{
			if (_dmdDecided) {
				return;
			}
			try {
				await Awaitable.WaitForSecondsAsync(DmdAnnouncementGrace);
			} catch (OperationCanceledException) {
				return;
			}
			if (_disposed || _dmdDecided) {
				return;
			}
			_dmdDecided = true;
			Refresh();
		}

		/// <summary>
		/// Binds every screen to the surface it should show now, and rebinds those whose choice changed.
		/// </summary>
		private void Refresh()
		{
			if (_disposed) {
				return;
			}
			foreach (var screen in _screens) {
				var resourceId = Choose(screen.Role);
				if (resourceId == screen.ResourceId) {
					Present(screen, screen.Binding?.Source); // the engine's DMD may have changed the screen's visibility
					continue;
				}
				Release(screen);
				screen.ResourceId = resourceId;
				if (resourceId == null) {
					Present(screen, null);
					continue;
				}
				var binding = _registry.Bind<IUnityTextureResource>(new PluginResourceSelection(ResourceKind.Surface) {
					ResourceId = resourceId, Controller = _controller, Session = _session,
				});
				screen.Binding = binding;
				binding.Changed += () => Present(screen, binding.Source);
				screen.Connection = binding.Connect(source => {
					Action<Texture> changed = _ => Present(screen, source);
					source.Changed += changed;
					return new Subscription(() => source.Changed -= changed);
				});
				Present(screen, binding.Source);
			}
		}

		/// <summary>
		/// The ID of the surface a role shows: the one of the most preferred provider, or null for none.
		/// </summary>
		private string Choose(string role)
		{
			if (role == SurfaceRoles.Dmd && (_engineDmd || !_dmdDecided)) {
				return null;
			}
			var candidates = _registry.Snapshot(new PluginResourceSelection(ResourceKind.Surface) {
				Role = role, Controller = _controller, Session = _session,
			});
			return candidates
				.OrderBy(descriptor => Rank(descriptor.Provider))
				.ThenBy(descriptor => descriptor.Provider, StringComparer.Ordinal)
				.ThenBy(descriptor => descriptor.Id, StringComparer.Ordinal)
				.FirstOrDefault()?.Id;
		}

		private int Rank(string provider)
		{
			for (var i = 0; i < _providers.Count; i++) {
				if (string.Equals(_providers[i], provider, StringComparison.Ordinal)) {
					return i;
				}
			}
			return int.MaxValue;
		}

		/// <summary>
		/// Shows the source's texture on the screen, or restores the authored texture and hides the screen when there is
		/// none. A DMD screen stays visible while the engine's DMD may still claim it, so the engine's display component
		/// under its hide root is active when the table activates and keeps receiving frames.
		/// </summary>
		private void Present(Screen screen, IUnityTextureResource source)
		{
			if (_disposed || screen.Renderer == null) {
				return;
			}
			var texture = source?.Texture;
			if (texture != null) {
				screen.Renderer.GetPropertyBlock(_properties, screen.MaterialIndex);
				_properties.SetTexture(screen.Property, texture);
				screen.Renderer.SetPropertyBlock(_properties, screen.MaterialIndex);
				screen.Applied = true;
				SetVisible(screen, true);
				return;
			}
			if (screen.Applied) {
				// The slot's block belongs to the screen; removing it brings back the material's current texture, such as
				// the one a DisplayComponent assigns when the engine takes the DMD over, and renderer-wide overrides.
				screen.Renderer.SetPropertyBlock(null, screen.MaterialIndex);
				screen.Applied = false;
			}
			SetVisible(screen, screen.Role == SurfaceRoles.Dmd && (_engineDmd || !_dmdDecided));
		}

		private static void SetVisible(Screen screen, bool visible)
		{
			if (screen.HideRoot != null && screen.HideRoot.activeSelf != visible) {
				screen.HideRoot.SetActive(visible);
			}
		}

		private static void Release(Screen screen)
		{
			screen.Connection?.Dispose();
			screen.Connection = null;
			screen.Binding?.Dispose();
			screen.Binding = null;
			screen.ResourceId = null;
		}

		/// <summary>
		/// Stops following the surfaces. Screens go back to their authored texture, and hide roots of screens without the
		/// engine's DMD are hidden.
		/// </summary>
		public void Dispose()
		{
			if (_disposed) {
				return;
			}
			foreach (var screen in _screens) {
				Release(screen);
				Present(screen, null);
			}
			_disposed = true;
			_observation?.Dispose();
			if (_engine != null && _screens.Count > 0) {
				_engine.OnDisplaysRequested -= DisplaysRequested;
				_engine.OnStarted -= EngineStarted;
			}
		}
	}
}
