// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// See LICENSE for the terms of use and distribution.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>Exclusive destination ownership prevents two plugins from opening the same physical device.</summary>
	public sealed class PluginOutputLeases
	{
		private readonly Dictionary<string, string> _owners = new Dictionary<string, string>(StringComparer.Ordinal);
		public IDisposable Acquire(string destination, string pluginId)
		{
			if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("A destination needs a stable ID.", nameof(destination));
			lock (_owners) {
				if (_owners.TryGetValue(destination, out var owner)) throw new InvalidOperationException(destination + " is already owned by " + owner + ".");
				_owners.Add(destination, pluginId);
			}
			return new Subscription(() => { lock (_owners) _owners.Remove(destination); });
		}
		public string? Owner(string destination) { lock (_owners) return _owners.TryGetValue(destination, out var owner) ? owner : null; }
	}
}
