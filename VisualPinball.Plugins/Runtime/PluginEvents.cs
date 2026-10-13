// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Threading;

namespace VisualPinball.Plugins
{
	/// <summary>Typed control notifications delivered on the host owner. Borrowed frames use their source contracts instead.</summary>
	public sealed class PluginEvents
	{
		private interface IChannel { }
		private sealed class Channel<T> : IChannel where T : struct
		{
			internal readonly List<Action<T>> Subscribers = new List<Action<T>>();
			internal Action<T>[] Dispatch = Array.Empty<Action<T>>();
		}
		private readonly Dictionary<Type, IChannel> _channels = new Dictionary<Type, IChannel>();
		private readonly OwnerScheduler _scheduler;
		private readonly Action<Exception> _fault;
		public PluginEvents(OwnerScheduler scheduler, Action<Exception>? fault = null) { _scheduler = scheduler; _fault = fault ?? (_ => { }); }
		public IDisposable Subscribe<T>(Action<T> receive) where T : struct
		{
			_scheduler.VerifyOwner();
			if (!_channels.TryGetValue(typeof(T), out var found)) _channels.Add(typeof(T), found = new Channel<T>());
			var channel = (Channel<T>)found;
			channel.Subscribers.Add(receive); channel.Dispatch = channel.Subscribers.ToArray();
			return new Subscription(() => { _scheduler.VerifyOwner(); channel.Subscribers.Remove(receive); channel.Dispatch = channel.Subscribers.ToArray(); });
		}
		public void Publish<T>(in T value) where T : struct
		{
			_scheduler.VerifyOwner();
			if (!_channels.TryGetValue(typeof(T), out var found)) return;
			foreach (var receive in ((Channel<T>)found).Dispatch) try { receive(value); } catch (Exception error) { _fault(error); }
		}
		public void Post<T>(T value, CancellationToken lifetime = default) where T : struct => _scheduler.Post(() => {
			if (!lifetime.IsCancellationRequested) Publish(value);
		});
	}
}
