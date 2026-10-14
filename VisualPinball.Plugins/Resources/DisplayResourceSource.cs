// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>Cached subscriber arrays make steady-state borrowed frame dispatch allocation-free.</summary>
	public sealed class DisplayResourceSource : IPluginDisplaySource, IDisposable
	{
		private readonly List<Action<PluginDisplayFrame>> _subscribers = new List<Action<PluginDisplayFrame>>();
		private Action<PluginDisplayFrame>[] _dispatch = Array.Empty<Action<PluginDisplayFrame>>();
		private readonly Func<ResourceView, DisplayEncoding, IDisposable>? _negotiate;
		private readonly Action<Exception>? _fault;
		private bool _disposed;
		public ResourceKind Kind => ResourceKind.Display;
		public DisplayResourceSource(Func<ResourceView, DisplayEncoding, IDisposable>? negotiate = null, Action<Exception>? fault = null)
		{ _negotiate = negotiate; _fault = fault; }
		public IDisposable Subscribe(Action<PluginDisplayFrame> receive)
		{
			if (_disposed) throw new ObjectDisposedException(nameof(DisplayResourceSource));
			_subscribers.Add(receive);
			_dispatch = _subscribers.ToArray();
			return new Subscription(() => { _subscribers.Remove(receive); _dispatch = _subscribers.ToArray(); });
		}
		public IDisposable AcquireView(ResourceView intent, DisplayEncoding acceptedFormats) => _negotiate?.Invoke(intent, acceptedFormats) ?? new Subscription(() => { });
		public void Publish(in PluginDisplayFrame frame)
		{
			if (_disposed) return;
			foreach (var receive in _dispatch)
				try { receive(frame); } catch (Exception error) { if (_fault == null) throw; _fault(error); }
		}
		public void Dispose() { _disposed = true; _subscribers.Clear(); _dispatch = Array.Empty<Action<PluginDisplayFrame>>(); }
	}

	public sealed class ControllerResourceSource : IPluginControllerSource, IDisposable
	{
		private readonly Action<Exception>? _fault;
		private readonly List<Action<PluginControllerEvent>> _subscribers = new List<Action<PluginControllerEvent>>();
		private Action<PluginControllerEvent>[] _dispatch = Array.Empty<Action<PluginControllerEvent>>();
		private bool _disposed;
		public ResourceKind Kind { get; }
		public ControllerResourceSource(Action<Exception>? fault = null) : this(ResourceKind.Controller, fault) { }
		/// <param name="kind"><see cref="ResourceKind.Controller"/> for mapped outputs, <see cref="ResourceKind.Outputs"/> for native ones.</param>
		public ControllerResourceSource(ResourceKind kind, Action<Exception>? fault = null)
		{
			if (kind != ResourceKind.Controller && kind != ResourceKind.Outputs) throw new ArgumentException("A controller source carries controller or output events.", nameof(kind));
			Kind = kind; _fault = fault;
		}
		public IDisposable Subscribe(Action<PluginControllerEvent> receive)
		{
			if (_disposed) throw new ObjectDisposedException(nameof(ControllerResourceSource));
			_subscribers.Add(receive); _dispatch = _subscribers.ToArray();
			return new Subscription(() => { _subscribers.Remove(receive); _dispatch = _subscribers.ToArray(); });
		}
		public void Publish(in PluginControllerEvent value)
		{
			if (_disposed) return;
			foreach (var receive in _dispatch)
				try { receive(value); } catch (Exception error) { if (_fault == null) throw; _fault(error); }
		}
		public void Dispose() { _disposed = true; _subscribers.Clear(); _dispatch = Array.Empty<Action<PluginControllerEvent>>(); }
	}
}
