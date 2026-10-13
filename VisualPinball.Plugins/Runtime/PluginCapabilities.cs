// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>Versioned service contracts, separate from concrete display/audio resources.</summary>
	public sealed class PluginCapabilities
	{
		private readonly Dictionary<string, object> _services = new Dictionary<string, object>(StringComparer.Ordinal);
		public IEnumerable<string> Ids => _services.Keys;
		public IDisposable Publish<T>(string id, T service) where T : class
		{
			if (_services.ContainsKey(id)) throw new InvalidOperationException("Capability already provided: " + id);
			_services.Add(id, service);
			return new Subscription(() => { if (_services.TryGetValue(id, out var current) && ReferenceEquals(current, service)) _services.Remove(id); });
		}
		public T? Find<T>(string id) where T : class => _services.TryGetValue(id, out var value) ? value as T : null;
		public T Require<T>(string id) where T : class => Find<T>(id) ?? throw new InvalidOperationException("Required capability is unavailable: " + id);
	}
}
