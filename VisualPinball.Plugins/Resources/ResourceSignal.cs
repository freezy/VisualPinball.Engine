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
	/// Thread-safe fan-out of values to subscribers. Subscription changes take a lock; synchronous producer dispatch reads a
	/// cached array, so publishing allocates nothing and may happen on any thread, such as an emulator's audio thread.
	/// </summary>
	internal sealed class ResourceSignal<T> : IDisposable where T : struct
	{
		private readonly List<Action<T>> _subscribers = new List<Action<T>>();
		private readonly Action<Exception> _fault;
		private Action<T>[] _dispatch = Array.Empty<Action<T>>();
		private bool _disposed;
		/// <param name="fault">Receives exceptions thrown by subscribers; null ignores them.</param>
		internal ResourceSignal(Action<Exception>? fault = null) { _fault = fault ?? (_ => { }); }
		/// <summary>Whether at least one subscriber is registered. Safe to read from any thread.</summary>
		internal bool HasSubscribers => Volatile.Read(ref _dispatch).Length > 0;
		/// <summary>Adds a subscriber; disposing the result removes it.</summary>
		/// <exception cref="ObjectDisposedException">The signal was disposed.</exception>
		internal IDisposable Subscribe(Action<T> receive)
		{
			lock (_subscribers) {
				if (_disposed) throw new ObjectDisposedException(nameof(ResourceSignal<T>));
				_subscribers.Add(receive); Volatile.Write(ref _dispatch, _subscribers.ToArray());
			}
			return new Subscription(() => { lock (_subscribers) { _subscribers.Remove(receive); Volatile.Write(ref _dispatch, _subscribers.ToArray()); } });
		}
		/// <summary>Calls every subscriber on the calling thread; an exception goes to the fault handler and dispatch continues.</summary>
		internal void Publish(in T frame) { foreach (var receive in Volatile.Read(ref _dispatch)) try { receive(frame); } catch (Exception error) { _fault(error); } }
		/// <summary>Drops all subscribers and refuses new ones.</summary>
		public void Dispose() { lock (_subscribers) { _disposed = true; _subscribers.Clear(); Volatile.Write(ref _dispatch, Array.Empty<Action<T>>()); } }
	}
	/// <summary>
	/// Ready-made <see cref="ResourceKind.Audio"/> source for producers: it dispatches <see cref="PluginPcmFrame"/>s to
	/// subscribers and lets one plugin take over playback.
	/// </summary>
	/// <remarks>
	/// Publishing and subscribing are thread-safe, so a producer can publish from its audio thread. PinMAME owns one,
	/// exposes it through the Unity integration's <c>IGamelogicAudioResourceProvider</c> so VPE publishes it as
	/// <c>&lt;controller&gt;/audio/rom</c>, copies samples only when <see cref="HasSubscribers"/> is true, and mutes its own
	/// output while <see cref="PlaybackReplaced"/> is true. The owner disposes it; disposing does not unpublish it.
	/// <example>
	/// <code>
	/// private readonly AudioResourceSource _audio = new AudioResourceSource(error =&gt; Debug.LogException(error));
	///
	/// private void OnAudio(byte[] buffer, int length, int sampleRate, int channels, int frames)
	/// {
	///     if (_audio.HasSubscribers)
	///         _audio.Publish(new PluginPcmFrame(buffer.AsMemory(0, length), PcmEncoding.Float32, sampleRate, channels, frames, ++_sequence));
	///     if (!_audio.PlaybackReplaced) PlayLocally(buffer, length);
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class AudioResourceSource : IPluginAudioSource, IPluginAudioPlaybackControl, IDisposable
	{
		private readonly ResourceSignal<PluginPcmFrame> _signal;
		private readonly PluginOutputLeases _output = new PluginOutputLeases();
		/// <summary>Always <see cref="ResourceKind.Audio"/>.</summary>
		public ResourceKind Kind => ResourceKind.Audio;
		/// <summary>
		/// Whether at least one receiver is subscribed. Producers check it to skip copying samples nobody reads. Safe to read
		/// from any thread.
		/// </summary>
		public bool HasSubscribers => _signal.HasSubscribers;
		/// <summary>
		/// Whether a plugin currently holds the playback lease from <see cref="AcquirePlayback"/>. While true, the producer
		/// should stop playing the sound itself but keep publishing it. Safe to read from any thread.
		/// </summary>
		public bool PlaybackReplaced => _output.Owner("playback") != null;
		/// <summary>Creates the source.</summary>
		/// <param name="fault">
		/// Receives exceptions thrown by a receiver, on the publishing thread; the remaining receivers still get the block.
		/// Null ignores them.
		/// </param>
		public AudioResourceSource(Action<Exception>? fault = null) { _signal = new ResourceSignal<PluginPcmFrame>(fault); }
		/// <summary>Registers a receiver for every block; it is called on the publishing thread. Thread-safe.</summary>
		/// <param name="receive">Called with each block.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		/// <exception cref="ObjectDisposedException">The source was disposed.</exception>
		public IDisposable Subscribe(Action<PluginPcmFrame> receive) => _signal.Subscribe(receive);
		/// <summary>
		/// Takes over playback until the returned lease is disposed; <see cref="PlaybackReplaced"/> is true meanwhile. Only
		/// one plugin can hold it at a time.
		/// </summary>
		/// <param name="pluginId">ID of the plugin taking over, named in the error another plugin gets.</param>
		/// <returns>The lease.</returns>
		/// <exception cref="InvalidOperationException">Another plugin already holds the lease.</exception>
		public IDisposable AcquirePlayback(string pluginId) => _output.Acquire("playback", pluginId);
		/// <summary>
		/// Delivers a block to every receiver, synchronously on the calling thread. Safe to call from any thread; does
		/// nothing after <see cref="Dispose"/>.
		/// </summary>
		/// <param name="frame">The block; its samples only need to stay valid until the call returns.</param>
		public void Publish(in PluginPcmFrame frame) => _signal.Publish(frame);
		/// <summary>
		/// Drops all receivers and refuses new ones. It does not unpublish the resource or release a playback lease.
		/// </summary>
		public void Dispose() => _signal.Dispose();
	}
	/// <summary>
	/// Ready-made <see cref="ResourceKind.Segments"/> source for producers: it carries the display's announced place and
	/// size and dispatches <see cref="PluginSegmentFrame"/>s to subscribers.
	/// </summary>
	/// <remarks>
	/// VPE creates one per segment display the gamelogic engine announces, numbered in announcement order, with the layout
	/// and hardware hint the engine declares (PinMAME maps its display types onto <c>seg7</c>, <c>seg9</c>, <c>seg14</c> and
	/// <c>seg16</c>). Publishing and subscribing are thread-safe. The owner disposes it; disposing does not unpublish it.
	/// <example>
	/// <code>
	/// var source = new SegmentResourceSource(order: 0, elements: 16, layout: "seg14", hardware: "", fault: Debug.LogException);
	/// var publication = registry.Publish(new PluginResourceDescriptor(controllerId + "/segments/display0", "pinmame", controllerId,
	///     "display0", "segments", ResourceKind.Segments, session, instance), source);
	/// source.Publish(new PluginSegmentFrame("seg14", 16, 16, brightness, ""));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class SegmentResourceSource : IPluginSegmentSource, IDisposable
	{
		private readonly ResourceSignal<PluginSegmentFrame> _signal;
		/// <summary>Always <see cref="ResourceKind.Segments"/>.</summary>
		public ResourceKind Kind => ResourceKind.Segments;
		/// <inheritdoc/>
		public int Order { get; }
		/// <inheritdoc/>
		public int Elements { get; }
		/// <inheritdoc/>
		public string Layout { get; }
		/// <inheritdoc/>
		public string Hardware { get; }
		/// <summary>
		/// Creates a source that announces nothing: order 0, no elements, and empty layout and hardware. Consumers then learn
		/// the size from the first frame. Prefer the constructor that announces the display.
		/// </summary>
		/// <param name="fault">Receives exceptions thrown by a receiver; null ignores them.</param>
		public SegmentResourceSource(Action<Exception>? fault = null) : this(0, 0, "", "", fault) { }
		/// <summary>Creates a source that announces the display's place, size and appearance.</summary>
		/// <param name="order">Position among the controller's segment displays, from 0.</param>
		/// <param name="elements">Number of elements (digits).</param>
		/// <param name="layout">Segment arrangement, such as <c>seg7</c>; null becomes empty.</param>
		/// <param name="hardware">Technology hint as in <see cref="PluginSegmentFrame.Hardware"/>; null becomes empty.</param>
		/// <param name="fault">
		/// Receives exceptions thrown by a receiver, on the publishing thread; the remaining receivers still get the frame.
		/// Null ignores them.
		/// </param>
		public SegmentResourceSource(int order, int elements, string layout, string hardware, Action<Exception>? fault = null)
		{
			Order = order; Elements = elements; Layout = layout ?? ""; Hardware = hardware ?? "";
			_signal = new ResourceSignal<PluginSegmentFrame>(fault);
		}
		/// <summary>Registers a receiver for every frame; it is called on the publishing thread. Thread-safe.</summary>
		/// <param name="receive">Called with each frame.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		/// <exception cref="ObjectDisposedException">The source was disposed.</exception>
		public IDisposable Subscribe(Action<PluginSegmentFrame> receive) => _signal.Subscribe(receive);
		/// <summary>
		/// Delivers a frame to every receiver, synchronously on the calling thread; does nothing after <see cref="Dispose"/>.
		/// </summary>
		/// <param name="frame">The frame; its brightness only needs to stay valid until the call returns.</param>
		public void Publish(in PluginSegmentFrame frame) => _signal.Publish(frame);
		/// <summary>Drops all receivers and refuses new ones. It does not unpublish the resource.</summary>
		public void Dispose() => _signal.Dispose();
	}
	/// <summary>
	/// Ready-made <see cref="ResourceKind.Input"/> source for producers of <see cref="PluginInputEvent"/>s. Publishing and
	/// subscribing are thread-safe. Nothing in VPE publishes input resources yet.
	/// </summary>
	public sealed class InputResourceSource : IPluginInputSource, IDisposable
	{
		private readonly ResourceSignal<PluginInputEvent> _signal;
		/// <summary>Always <see cref="ResourceKind.Input"/>.</summary>
		public ResourceKind Kind => ResourceKind.Input;
		/// <summary>Creates the source.</summary>
		/// <param name="fault">
		/// Receives exceptions thrown by a receiver, on the publishing thread; the remaining receivers still get the event.
		/// Null ignores them.
		/// </param>
		public InputResourceSource(Action<Exception>? fault = null) { _signal = new ResourceSignal<PluginInputEvent>(fault); }
		/// <summary>Registers a receiver for every event; it is called on the publishing thread. Thread-safe.</summary>
		/// <param name="receive">Called with each event.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		/// <exception cref="ObjectDisposedException">The source was disposed.</exception>
		public IDisposable Subscribe(Action<PluginInputEvent> receive) => _signal.Subscribe(receive);
		/// <summary>
		/// Delivers an event to every receiver, synchronously on the calling thread; does nothing after <see cref="Dispose"/>.
		/// </summary>
		/// <param name="value">The event.</param>
		public void Publish(in PluginInputEvent value) => _signal.Publish(value);
		/// <summary>Drops all receivers and refuses new ones. It does not unpublish the resource.</summary>
		public void Dispose() => _signal.Dispose();
	}
	/// <summary>
	/// Ready-made <see cref="ResourceKind.Triggers"/> source for producers of <see cref="PluginTriggerEvent"/>s. Publishing
	/// and subscribing are thread-safe. Nothing in VPE publishes trigger resources yet.
	/// </summary>
	public sealed class TriggerResourceSource : IPluginTriggerSource, IDisposable
	{
		private readonly ResourceSignal<PluginTriggerEvent> _signal;
		/// <summary>Always <see cref="ResourceKind.Triggers"/>.</summary>
		public ResourceKind Kind => ResourceKind.Triggers;
		/// <summary>Creates the source.</summary>
		/// <param name="fault">
		/// Receives exceptions thrown by a receiver, on the publishing thread; the remaining receivers still get the event.
		/// Null ignores them.
		/// </param>
		public TriggerResourceSource(Action<Exception>? fault = null) { _signal = new ResourceSignal<PluginTriggerEvent>(fault); }
		/// <summary>Registers a receiver for every event; it is called on the publishing thread. Thread-safe.</summary>
		/// <param name="receive">Called with each event.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		/// <exception cref="ObjectDisposedException">The source was disposed.</exception>
		public IDisposable Subscribe(Action<PluginTriggerEvent> receive) => _signal.Subscribe(receive);
		/// <summary>
		/// Delivers an event to every receiver, synchronously on the calling thread; does nothing after <see cref="Dispose"/>.
		/// </summary>
		/// <param name="value">The event.</param>
		public void Publish(in PluginTriggerEvent value) => _signal.Publish(value);
		/// <summary>Drops all receivers and refuses new ones. It does not unpublish the resource.</summary>
		public void Dispose() => _signal.Dispose();
	}
}
