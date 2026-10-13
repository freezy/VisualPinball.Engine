// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// See LICENSE for the terms of use and distribution.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>Releases every owned resource in reverse order, even if one cleanup fails.</summary>
	public sealed class ResourceScope : IDisposable
	{
		private readonly List<IDisposable> _resources = new List<IDisposable>();
		private bool _disposed;
		public T Own<T>(T resource) where T : IDisposable
		{
			if (_disposed) { resource.Dispose(); throw new ObjectDisposedException(nameof(ResourceScope)); }
			_resources.Add(resource);
			return resource;
		}
		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			List<Exception>? errors = null;
			for (int i = _resources.Count - 1; i >= 0; i--)
				try { _resources[i].Dispose(); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
			_resources.Clear();
			if (errors != null) throw new AggregateException("Some plugin resources could not be released.", errors);
		}
	}

	public sealed class Subscription : IDisposable
	{
		private Action? _release;
		public Subscription(Action release) { _release = release; }
		public void Dispose() => System.Threading.Interlocked.Exchange(ref _release, null)?.Invoke();
	}
}
