// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// See LICENSE for the terms of use and distribution.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	public enum PluginPhase { Initialize, Prepare, Start, Ready, Pause, Resume, Focus, Tick, Render, Stop, Shutdown }
	public enum PluginState { Available, Disabled, Unsupported, Blocked, Starting, Running, Stopping, Faulted, Unresponsive }
	public enum ConfigurationScope { GlobalOnly, GameOnly, GlobalAndGame }
	public enum ConfigurationChange { Live, NextGame, ApplicationRestart }

	public sealed class PluginOrder
	{
		public int Priority { get; set; }
		public IReadOnlyList<string> Before { get; set; } = Array.Empty<string>();
		public IReadOnlyList<string> After { get; set; } = Array.Empty<string>();
	}

	public sealed class PluginDescriptor
	{
		public const int HostApiVersion = 1;
		public string Id { get; }
		public string Name { get; }
		public string Description { get; }
		public string Version { get; set; } = "0.1.0";
		public int ApiVersion { get; set; } = HostApiVersion;
		public bool EnabledByDefault { get; set; }
		public string Category { get; set; } = "Integrations";
		public int Order { get; set; }
		public IReadOnlyList<string> DependsOn { get; set; } = Array.Empty<string>();
		public IReadOnlyList<string> Before { get; set; } = Array.Empty<string>();
		public IReadOnlyList<string> After { get; set; } = Array.Empty<string>();
		public IReadOnlyList<string> Provides { get; set; } = Array.Empty<string>();
		public IReadOnlyList<string> Requires { get; set; } = Array.Empty<string>();
		public IReadOnlyDictionary<PluginPhase, PluginOrder> PhaseOrder { get; set; } = new Dictionary<PluginPhase, PluginOrder>();
		public IReadOnlyList<string> Platforms { get; set; } = Array.Empty<string>();

		public PluginDescriptor(string id, string name, string description)
		{
			if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A plugin needs a stable ID.", nameof(id));
			foreach (char character in id)
				if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '.' && character != '-')
					throw new ArgumentException("Plugin IDs use lowercase letters, digits, dots and hyphens.", nameof(id));
			if (id == "." || id == "..") throw new ArgumentException("Invalid plugin ID.", nameof(id));
			Id = id;
			Name = name;
			Description = description;
		}
	}

	public sealed class PluginStatus
	{
		public PluginState State { get; internal set; } = PluginState.Available;
		public string Message { get; internal set; } = "";
		public PluginPhase? Phase { get; internal set; }
	}

	public readonly struct GameIdentity : IEquatable<GameIdentity>
	{
		public string Provider { get; }
		public string Key { get; }
		public string Id => Provider + ":" + Key;
		public GameIdentity(string provider, string key)
		{
			if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A game identity needs a provider and key.");
			Provider = provider.Trim().ToLowerInvariant();
			Key = key.Trim().ToLowerInvariant();
		}
		public bool Equals(GameIdentity other) => Provider == other.Provider && Key == other.Key;
		public override bool Equals(object? other) => other is GameIdentity identity && Equals(identity);
		public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);
		public override string ToString() => Id;
	}
}
