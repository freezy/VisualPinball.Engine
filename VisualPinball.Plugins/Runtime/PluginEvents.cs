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
	/// <summary>
	/// Typed publish/subscribe bus for small control notifications between plugins, delivered on the host's owner thread.
	/// One channel exists per struct type: subscribers of <c>T</c> receive every published <c>T</c>.
	/// </summary>
	/// <remarks>
	/// The Player publishes one instance as the <c>vpe.events.v1</c> capability (<c>UnityPluginCapability.Events</c>), with
	/// subscriber exceptions going to Unity's log. Plugins define their own notification structs and agree on them; none of the
	/// bundled plugins uses the bus yet. Streams of frames or samples do not belong here: they are borrowed buffers and go
	/// through the resource contracts in <see cref="PluginResourceRegistry"/>.
	/// <example>
	/// A plugin-defined notification:
	/// <code>
	/// public readonly struct AttractModeChanged { public bool Active { get; } public AttractModeChanged(bool active) =&gt; Active = active; }
	///
	/// var events = game.Capabilities.Require&lt;PluginEvents&gt;(UnityPluginCapability.Events);
	/// game.Lifetime.Own(events.Subscribe&lt;AttractModeChanged&gt;(change =&gt; _attract = change.Active));
	/// events.Post(new AttractModeChanged(true), game.Cancellation); // from any thread
	/// </code>
	/// </example>
	/// </remarks>
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
		/// <summary>Creates an event bus bound to an owner thread. Created by the Player; plugins get it as a capability.</summary>
		/// <param name="scheduler">The owner thread that subscriptions, publishing and delivery belong to.</param>
		/// <param name="fault">
		/// Receives exceptions thrown by subscribers, so one failing subscriber does not stop delivery to the others. When null,
		/// such exceptions are silently dropped.
		/// </param>
		public PluginEvents(OwnerScheduler scheduler, Action<Exception>? fault = null) { _scheduler = scheduler; _fault = fault ?? (_ => { }); }
		/// <summary>
		/// Starts receiving every published <typeparamref name="T"/> until the returned handle is disposed. Owner thread only. A
		/// subscriber added while an event is being delivered receives the next one, not the current one.
		/// </summary>
		/// <typeparam name="T">The notification type; a struct, so each subscriber gets its own copy.</typeparam>
		/// <param name="receive">Called on the owner thread for each event.</param>
		/// <returns>
		/// A handle that unsubscribes. Dispose it on the owner thread, typically through <see cref="PluginGameContext.Lifetime"/>.
		/// </returns>
		/// <exception cref="InvalidOperationException">Called off the owner thread.</exception>
		public IDisposable Subscribe<T>(Action<T> receive) where T : struct
		{
			_scheduler.VerifyOwner();
			if (!_channels.TryGetValue(typeof(T), out var found)) _channels.Add(typeof(T), found = new Channel<T>());
			var channel = (Channel<T>)found;
			channel.Subscribers.Add(receive); channel.Dispatch = channel.Subscribers.ToArray();
			return new Subscription(() => { _scheduler.VerifyOwner(); channel.Subscribers.Remove(receive); channel.Dispatch = channel.Subscribers.ToArray(); });
		}
		/// <summary>
		/// Delivers an event synchronously to the current subscribers of <typeparamref name="T"/>, in subscription order. Owner
		/// thread only; use <see cref="Post{T}"/> from other threads. Subscriber exceptions go to the fault handler.
		/// </summary>
		/// <typeparam name="T">The notification type.</typeparam>
		/// <param name="value">The event to deliver.</param>
		/// <exception cref="InvalidOperationException">Called off the owner thread.</exception>
		public void Publish<T>(in T value) where T : struct
		{
			_scheduler.VerifyOwner();
			if (!_channels.TryGetValue(typeof(T), out var found)) return;
			foreach (var receive in ((Channel<T>)found).Dispatch) try { receive(value); } catch (Exception error) { _fault(error); }
		}
		/// <summary>
		/// Queues an event for delivery on the owner thread during the next scheduler pump. Thread-safe. The event is dropped if
		/// <paramref name="lifetime"/> is cancelled by the time it would be delivered, so pass the game's cancellation token to
		/// keep a stopped game's events from reaching the next one.
		/// </summary>
		/// <typeparam name="T">The notification type.</typeparam>
		/// <param name="value">The event to deliver.</param>
		/// <param name="lifetime">Cancels delivery if it has not happened yet; the default never cancels.</param>
		public void Post<T>(T value, CancellationToken lifetime = default) where T : struct => _scheduler.Post(() => {
			if (!lifetime.IsCancellationRequested) Publish(value);
		});
	}
}
