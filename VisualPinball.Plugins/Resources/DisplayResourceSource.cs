// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Ready-made <see cref="IPluginDisplaySource"/> for code that produces display frames: publish it in the
	/// <see cref="PluginResourceRegistry"/>, then call <see cref="Publish"/> for every frame.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The Unity integration wraps every DMD of a running gamelogic engine, such as PinMAME, in one of these, and the DMD
	/// plugin publishes its colorized output through one as a <see cref="ResourceView.Presentation"/> resource derived from
	/// the original. Consumers such as the B2S and PinUP plugins bind to it through <see cref="PluginResourceRegistry.Bind{T}"/>.
	/// </para>
	/// <para>
	/// Delivery is synchronous and reuses a cached subscriber array, so publishing a frame allocates nothing once
	/// subscriptions are stable. The class does no locking: subscribe, publish and dispose on the owner thread. Stamp every
	/// frame with the <see cref="PluginResourceDescriptor.Instance"/> the source was published with, because
	/// <see cref="DisplayBindingExtensions.SubscribeFrames"/> drops frames of any other instance.
	/// </para>
	/// <example>
	/// The DMD plugin's presentation output:
	/// <code>
	/// _source = new DisplayResourceSource(fault: error =&gt; application.Log(error.Message));
	/// _publication = application.Resources.Publish(new PluginResourceDescriptor(original.Id + "/presentation/" + application.Plugin.Id,
	///     application.Plugin.Id, original.Controller, original.Label + " · DMD presentation", original.Role, ResourceKind.Display,
	///     original.Session, _instance, ResourceView.Presentation, DisplayEncoding.Rgb24, original.Id), _source);
	/// // each frame, on the owner thread:
	/// _source.Publish(new PluginDisplayFrame(pixels, width, height, width * 3, DisplayEncoding.Rgb24, ++_sequence, timestampUs, _instance));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class DisplayResourceSource : IPluginDisplaySource, IDisposable
	{
		private readonly List<Action<PluginDisplayFrame>> _subscribers = new List<Action<PluginDisplayFrame>>();
		private Action<PluginDisplayFrame>[] _dispatch = Array.Empty<Action<PluginDisplayFrame>>();
		private readonly Func<ResourceView, DisplayEncoding, IDisposable>? _negotiate;
		private readonly Action<Exception>? _fault;
		private bool _disposed;
		/// <summary>Always <see cref="ResourceKind.Display"/>.</summary>
		public ResourceKind Kind => ResourceKind.Display;
		/// <summary>Creates a display source.</summary>
		/// <param name="negotiate">
		/// Implements <see cref="AcquireView"/>: called with the consumer's intent and accepted formats, returns a lease that
		/// holds the producer's output format until disposed, and throws when the producer cannot satisfy the request. The
		/// Unity integration passes <see cref="DisplayFormatNegotiator.Acquire"/>, which switches the engine to raw Gray2/Gray4
		/// frames while identification consumers are bound. When null, every view request succeeds with a lease that does nothing.
		/// </param>
		/// <param name="fault">
		/// Receives exceptions thrown by subscribers, and delivery continues with the next subscriber. When null, a subscriber's
		/// exception propagates out of <see cref="Publish"/> and the remaining subscribers miss that frame.
		/// </param>
		public DisplayResourceSource(Func<ResourceView, DisplayEncoding, IDisposable>? negotiate = null, Action<Exception>? fault = null)
		{ _negotiate = negotiate; _fault = fault; }
		/// <summary>
		/// Adds a frame receiver until the returned handle is disposed. Consumers normally go through a
		/// <see cref="PluginResourceBinding{T}"/> and <see cref="DisplayBindingExtensions.SubscribeFrames"/> instead of calling this.
		/// </summary>
		/// <param name="receive">Called synchronously for every published frame; the pixels are only valid during the call.</param>
		/// <returns>A handle that removes the receiver.</returns>
		/// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
		public IDisposable Subscribe(Action<PluginDisplayFrame> receive)
		{
			if (_disposed) throw new ObjectDisposedException(nameof(DisplayResourceSource));
			_subscribers.Add(receive);
			_dispatch = _subscribers.ToArray();
			return new Subscription(() => { _subscribers.Remove(receive); _dispatch = _subscribers.ToArray(); });
		}
		/// <summary>
		/// Asks the producer for a view of the display, delegating to the <c>negotiate</c> function given to the constructor.
		/// A binding calls it when it binds and disposes the lease when it unbinds.
		/// </summary>
		/// <param name="intent">What the consumer wants, such as <see cref="ResourceView.Identification"/> for unmodified raw frames.</param>
		/// <param name="acceptedFormats">The encodings the consumer can read.</param>
		/// <returns>A lease that keeps the negotiated format until disposed.</returns>
		/// <exception cref="InvalidOperationException">Thrown by the negotiator when the producer cannot provide the requested view.</exception>
		public IDisposable AcquireView(ResourceView intent, DisplayEncoding acceptedFormats) => _negotiate?.Invoke(intent, acceptedFormats) ?? new Subscription(() => { });
		/// <summary>
		/// Delivers a frame synchronously to every subscriber, in subscription order. The frame's pixels may be reused as soon
		/// as the call returns. Does nothing after <see cref="Dispose"/>.
		/// </summary>
		/// <param name="frame">The frame; its <see cref="PluginDisplayFrame.Instance"/> must match the published descriptor.</param>
		public void Publish(in PluginDisplayFrame frame)
		{
			if (_disposed) return;
			foreach (var receive in _dispatch)
				try { receive(frame); } catch (Exception error) { if (_fault == null) throw; _fault(error); }
		}
		/// <summary>
		/// Drops all subscribers and stops delivery; later <see cref="Subscribe"/> calls throw. Does not withdraw the
		/// publication: dispose the handle returned by <see cref="PluginResourceRegistry.Publish{T}"/> as well.
		/// </summary>
		public void Dispose() { _disposed = true; _subscribers.Clear(); _dispatch = Array.Empty<Action<PluginDisplayFrame>>(); }
	}

	/// <summary>
	/// Ready-made <see cref="IPluginControllerSource"/> for code that produces controller events (switches, coils, lamps, GI):
	/// publish it in the <see cref="PluginResourceRegistry"/>, then call <see cref="Publish"/> for every change.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The Unity integration publishes two per running gamelogic engine: a <see cref="ResourceKind.Controller"/> source with
	/// the outputs the table maps, which PinUP forwards to its PUP pack, and, when the engine reports them, a
	/// <see cref="ResourceKind.Outputs"/> source with every output by native number, which the B2S plugin prefers for
	/// backglass lamps.
	/// </para>
	/// <para>
	/// Delivery is synchronous and allocation-free once subscriptions are stable. The class does no locking: subscribe,
	/// publish and dispose on the owner thread, and post events from other threads with <see cref="OwnerScheduler.Post"/>.
	/// </para>
	/// <example>
	/// <code>
	/// var source = scope.Own(new ControllerResourceSource(fault));
	/// scope.Own(registry.Publish(new PluginResourceDescriptor(controllerId, provider, controllerId, engine.Name, "controller",
	///     ResourceKind.Controller, session, instance), source));
	/// source.Publish(new PluginControllerEvent(ControllerEventKind.Coil, "28", 1, ++sequence, timestampUs));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class ControllerResourceSource : IPluginControllerSource, IDisposable
	{
		private readonly Action<Exception>? _fault;
		private readonly List<Action<PluginControllerEvent>> _subscribers = new List<Action<PluginControllerEvent>>();
		private Action<PluginControllerEvent>[] _dispatch = Array.Empty<Action<PluginControllerEvent>>();
		private bool _disposed;
		/// <summary>
		/// <see cref="ResourceKind.Controller"/> or <see cref="ResourceKind.Outputs"/>, as chosen at construction. Must match the
		/// descriptor this source is published with.
		/// </summary>
		public ResourceKind Kind { get; }
		/// <summary>Creates a source of mapped controller events (<see cref="ResourceKind.Controller"/>).</summary>
		/// <param name="fault">
		/// Receives exceptions thrown by subscribers, and delivery continues. When null, a subscriber's exception propagates out
		/// of <see cref="Publish"/>.
		/// </param>
		public ControllerResourceSource(Action<Exception>? fault = null) : this(ResourceKind.Controller, fault) { }
		/// <summary>Creates a controller event source of the given kind.</summary>
		/// <param name="kind">
		/// <see cref="ResourceKind.Controller"/> for mapped outputs, <see cref="ResourceKind.Outputs"/> for native ones.
		/// </param>
		/// <param name="fault">
		/// Receives exceptions thrown by subscribers, and delivery continues. When null, a subscriber's exception propagates out
		/// of <see cref="Publish"/>.
		/// </param>
		/// <exception cref="ArgumentException">
		/// <paramref name="kind"/> is neither <see cref="ResourceKind.Controller"/> nor <see cref="ResourceKind.Outputs"/>.
		/// </exception>
		public ControllerResourceSource(ResourceKind kind, Action<Exception>? fault = null)
		{
			if (kind != ResourceKind.Controller && kind != ResourceKind.Outputs) throw new ArgumentException("A controller source carries controller or output events.", nameof(kind));
			Kind = kind; _fault = fault;
		}
		/// <summary>
		/// Adds an event receiver until the returned handle is disposed. Consumers normally connect through a
		/// <see cref="PluginResourceBinding{T}"/> instead of calling this directly.
		/// </summary>
		/// <param name="receive">Called synchronously on the publishing (owner) thread for every event.</param>
		/// <returns>A handle that removes the receiver.</returns>
		/// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
		public IDisposable Subscribe(Action<PluginControllerEvent> receive)
		{
			if (_disposed) throw new ObjectDisposedException(nameof(ControllerResourceSource));
			_subscribers.Add(receive); _dispatch = _subscribers.ToArray();
			return new Subscription(() => { _subscribers.Remove(receive); _dispatch = _subscribers.ToArray(); });
		}
		/// <summary>
		/// Delivers an event synchronously to every subscriber, in subscription order. Does nothing after <see cref="Dispose"/>.
		/// </summary>
		/// <param name="value">The event.</param>
		public void Publish(in PluginControllerEvent value)
		{
			if (_disposed) return;
			foreach (var receive in _dispatch)
				try { receive(value); } catch (Exception error) { if (_fault == null) throw; _fault(error); }
		}
		/// <summary>
		/// Drops all subscribers and stops delivery; later <see cref="Subscribe"/> calls throw. Does not withdraw the
		/// publication: dispose the handle returned by <see cref="PluginResourceRegistry.Publish{T}"/> as well.
		/// </summary>
		public void Dispose() { _disposed = true; _subscribers.Clear(); _dispatch = Array.Empty<Action<PluginControllerEvent>>(); }
	}
}
