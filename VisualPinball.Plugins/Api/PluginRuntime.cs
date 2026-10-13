// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VisualPinball.Plugins
{
	public interface IPlayerPluginFactory
	{
		PluginDescriptor Descriptor { get; }
		PluginSettingsSchema Schema { get; }
		IPluginSettingsProvider? SettingsProvider { get; }
		IPlayerPlugin Create(PluginApplicationContext context);
	}

	public interface IPlayerPlugin : IDisposable
	{
		Task InitializeAsync(CancellationToken cancellation);
		Task PrepareGameAsync(PluginGameContext game, CancellationToken cancellation);
		Task StartGameAsync(CancellationToken cancellation);
		void GameReady();
		void PauseChanged(bool paused);
		void FocusChanged(bool focused);
		Task StopGameAsync(CancellationToken cancellation);
		Task ShutdownAsync(CancellationToken cancellation);
	}
	public interface IPluginTick { void Tick(double unscaledSeconds); }
	public interface IPluginRender { void BeforeRender(); }
	public interface IPluginEmergencyStop { void EmergencyStop(); }
	/// <summary>After emergency stop, completes only when native workers can no longer use the plugin's resources.</summary>
	public interface IPluginDeferredCleanup { Task Quiescence { get; } }
	/// <summary>Reconfiguration is transactional: failure or cancellation must leave the previous runtime state intact.</summary>
	public interface IPluginReconfiguration { Task<PluginActionResult> ApplyAsync(PluginSettingsSnapshot settings, CancellationToken cancellation); }

	/// <summary>Implement only the hooks a plugin needs; the host still controls its lifetime.</summary>
	public abstract class PlayerPlugin : IPlayerPlugin
	{
		public virtual Task InitializeAsync(CancellationToken cancellation) => Task.CompletedTask;
		public virtual Task PrepareGameAsync(PluginGameContext game, CancellationToken cancellation) => Task.CompletedTask;
		public virtual Task StartGameAsync(CancellationToken cancellation) => Task.CompletedTask;
		public virtual void GameReady() { }
		public virtual void PauseChanged(bool paused) { }
		public virtual void FocusChanged(bool focused) { }
		public virtual Task StopGameAsync(CancellationToken cancellation) => Task.CompletedTask;
		public virtual Task ShutdownAsync(CancellationToken cancellation) => Task.CompletedTask;
		public virtual void Dispose() { }
	}

	public sealed class PluginApplicationContext
	{
		public PluginDescriptor Plugin { get; }
		public PluginConfigurationStore Configuration { get; }
		public OwnerScheduler Scheduler { get; }
		public PluginCapabilities Capabilities { get; }
		public PluginResourceRegistry Resources { get; }
		public ResourceScope Lifetime { get; }
		public Action<string> Log { get; }
		public PluginApplicationContext(PluginDescriptor plugin, PluginConfigurationStore configuration, OwnerScheduler scheduler,
			PluginCapabilities capabilities, PluginResourceRegistry resources, ResourceScope lifetime, Action<string> log)
		{ Plugin = plugin; Configuration = configuration; Scheduler = scheduler; Capabilities = capabilities; Resources = resources; Lifetime = lifetime; Log = log; }
	}

	public sealed class PluginGameContext
	{
		public GameIdentity Identity { get; }
		public string PackagePath { get; }
		public string Title { get; }
		public string ControllerId { get; }
		public long Session { get; }
		public PluginSettingsSnapshot Settings { get; }
		public PluginCapabilities Capabilities { get; }
		public PluginResourceRegistry Resources { get; }
		public ResourceScope Lifetime { get; }
		public CancellationToken Cancellation { get; }
		public PluginGameContext(GameIdentity identity, string packagePath, string title, string controllerId, long session,
			PluginSettingsSnapshot settings, PluginCapabilities capabilities, PluginResourceRegistry resources, ResourceScope lifetime, CancellationToken cancellation)
		{ Identity = identity; PackagePath = packagePath; Title = title; ControllerId = controllerId; Session = session; Settings = settings; Capabilities = capabilities; Resources = resources; Lifetime = lifetime; Cancellation = cancellation; }
	}

	public static class PlayerPluginRegistry
	{
		private static readonly Dictionary<string, IPlayerPluginFactory> Factories = new Dictionary<string, IPlayerPluginFactory>(StringComparer.Ordinal);
		public static IReadOnlyCollection<IPlayerPluginFactory> Installed => Factories.Values;
		public static void Reset() => Factories.Clear();
		public static void Register(IPlayerPluginFactory factory)
		{
			if (factory.Descriptor.ApiVersion != PluginDescriptor.HostApiVersion) throw new InvalidOperationException("Plugin API version is incompatible: " + factory.Descriptor.Id);
			if (Factories.ContainsKey(factory.Descriptor.Id)) throw new InvalidOperationException("Duplicate plugin ID: " + factory.Descriptor.Id);
			Factories.Add(factory.Descriptor.Id, factory);
		}
	}
}
