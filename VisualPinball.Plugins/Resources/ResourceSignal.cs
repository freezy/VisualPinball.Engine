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
	/// <summary>
	/// Ready-made <see cref="ResourceKind.RealtimeOutputs"/> source: it gives every new receiver its own initial snapshot
	/// and then the changes, on the producer's thread, without allocating.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Receivers subscribe from any thread. Exactly one thread produces at a time (VPE's simulation thread, or the frame
	/// update when the game runs without it); it calls, in this order, on each delivery: changes that precede the snapshot
	/// with <see cref="Publish"/>, then, when <see cref="BeginSnapshot"/> returns true, the current value of every element
	/// with <see cref="PublishSnapshot"/> and <see cref="EndSnapshot"/>, then the remaining changes with
	/// <see cref="Publish"/>. Receivers that subscribed since the last snapshot get the snapshot and its
	/// <see cref="RealtimeOutputKind.SnapshotBoundary"/>, and from then on the changes; receivers that already had theirs
	/// get only changes. A receiver therefore never sees a change before its snapshot, or one that its snapshot already
	/// contains.
	/// </para>
	/// <para>
	/// Delivery reads a cached array, so it allocates nothing once subscriptions are stable; subscribing and unsubscribing
	/// take a lock and allocate on the subscribing thread. The owner disposes the source; disposing does not unpublish it.
	/// </para>
	/// <para>
	/// A receiver never goes live on an incomplete snapshot. A producer that cannot finish one calls
	/// <see cref="AbortSnapshot"/> (or simply begins the next), and a receiver that throws during its snapshot gets another
	/// one; either way the receiver gets <see cref="RealtimeOutputKind.SnapshotAborted"/> first. A producer that lost values, such as a full queue, publishes <see cref="RealtimeOutputKind.Overflow"/> before it
	/// resynchronises, so receivers know that transitions were skipped.
	/// </para>
	/// <example>
	/// The Unity integration forwards an engine's real-time listener calls:
	/// <code>
	/// var source = scope.Own(new RealtimeOutputResourceSource(RealtimeOutputNumbering.PinMame, fault));
	/// scope.Own(registry.Publish(new PluginResourceDescriptor(controllerId + "/outputs/realtime", provider, controllerId,
	///     "PinMAME real-time outputs", "realtime-outputs", ResourceKind.RealtimeOutputs, session, instance), source));
	/// // on the producing thread, once per poll:
	/// source.Publish(RealtimeOutputKind.Solenoid, 28, 1);
	/// if (source.BeginSnapshot()) {
	///     foreach (var (number, value) in solenoids) source.PublishSnapshot(RealtimeOutputKind.Solenoid, number, value);
	///     source.EndSnapshot();
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class RealtimeOutputResourceSource : IPluginRealtimeOutputSource, IDisposable
	{
		/// <summary>How many snapshots in a row a receiver may fail (by throwing) before it is dropped.</summary>
		public const int MaxSnapshotAttempts = 3;
		private const int Waiting = 0, InSnapshot = 1, Live = 2, Removed = 3;
		private sealed class Receiver
		{
			internal readonly RealtimeOutputReceiver Receive;
			/// <summary><see cref="Waiting"/> for its snapshot, <see cref="InSnapshot"/>, <see cref="Live"/> or <see cref="Removed"/>.</summary>
			internal int State;
			/// <summary>The receiver threw during its current snapshot, so it must not go live on it. Producing thread only.</summary>
			internal bool SnapshotFailed;
			/// <summary>Snapshots in a row the receiver failed. Producing thread only.</summary>
			internal int FailedSnapshots;
			/// <summary>1 once the receiver's first exception was reported.</summary>
			internal int Reported;
			internal Receiver(RealtimeOutputReceiver receive) { Receive = receive; }
		}
		private readonly List<Receiver> _receivers = new List<Receiver>();
		private readonly Action<Exception>? _fault;
		private readonly Action<bool>? _receiversChanged;
		private Receiver[] _dispatch = Array.Empty<Receiver>();
		private int _snapshotRequested;
		// producing thread only: a snapshot began and has not ended
		private bool _snapshotOpen;
		private bool _disposed;
		/// <summary>Always <see cref="ResourceKind.RealtimeOutputs"/>.</summary>
		public ResourceKind Kind => ResourceKind.RealtimeOutputs;
		/// <inheritdoc/>
		public string Numbering { get; }
		/// <summary>Creates the source.</summary>
		/// <param name="numbering">The numbering the values follow, such as <see cref="RealtimeOutputNumbering.PinMame"/>.</param>
		/// <param name="fault">
		/// Receives the first exception each receiver throws, and one when a receiver is dropped for failing its snapshots,
		/// on the producing thread. Later exceptions of the same receiver are not reported again, so a broken receiver does
		/// not cost an allocation per value. The remaining receivers still get the value. Null ignores them. An exception it
		/// throws itself is ignored, so it cannot keep the other receivers from their values or their snapshot's end.
		/// </param>
		/// <param name="receiversChanged">
		/// Called with true when the first receiver subscribes and with false when the last one leaves or the source is
		/// disposed, on the thread that subscribed or unsubscribed and outside any lock. It is a signal: when a subscription
		/// and the last unsubscription race on two threads, their calls can arrive in either order, so read
		/// <see cref="HasReceivers"/> in it rather than keeping the value. If the call with true throws, that subscription
		/// fails: its receiver is removed again (reported with false when no other receiver came meanwhile) and the
		/// exception is rethrown. VPE uses it to bind the engine's real-time listener only while somebody listens.
		/// </param>
		/// <exception cref="ArgumentException"><paramref name="numbering"/> is null or empty.</exception>
		public RealtimeOutputResourceSource(string numbering, Action<Exception>? fault = null, Action<bool>? receiversChanged = null)
		{
			if (string.IsNullOrEmpty(numbering)) throw new ArgumentException("Real-time outputs need a numbering.", nameof(numbering));
			Numbering = numbering; _fault = fault; _receiversChanged = receiversChanged;
		}
		/// <summary>Whether at least one receiver is subscribed. Safe to read from any thread.</summary>
		public bool HasReceivers => Volatile.Read(ref _dispatch).Length > 0;
		/// <summary>
		/// Whether a receiver waits for its snapshot: it subscribed, or its last snapshot was aborted or failed, since the
		/// last <see cref="BeginSnapshot"/>. Safe to read from any thread.
		/// </summary>
		public bool SnapshotRequested => Volatile.Read(ref _snapshotRequested) != 0;
		/// <summary>
		/// Adds a receiver until the returned handle is disposed. Thread-safe. The receiver gets nothing until the producer's
		/// next snapshot, which starts with its first value.
		/// </summary>
		/// <param name="receive">Called on the producing thread with each value.</param>
		/// <returns>A handle that removes the receiver.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="receive"/> is null.</exception>
		/// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
		public IDisposable Subscribe(RealtimeOutputReceiver receive)
		{
			if (receive == null) throw new ArgumentNullException(nameof(receive));
			var receiver = new Receiver(receive);
			bool first;
			lock (_receivers) {
				if (_disposed) throw new ObjectDisposedException(nameof(RealtimeOutputResourceSource));
				_receivers.Add(receiver); first = _receivers.Count == 1;
				Volatile.Write(ref _dispatch, _receivers.ToArray());
			}
			// after the receiver is in the array, so a producer that sees the request also sees the receiver
			Volatile.Write(ref _snapshotRequested, 1);
			if (first && _receiversChanged != null) {
				try {
					_receiversChanged(true);
				} catch {
					// nobody could remove a receiver whose subscription failed; the cleanup's own failure must not hide why
					try {
						Unsubscribe(receiver);
					} catch {
						// the exception below says what went wrong
					}
					throw;
				}
			}
			return new Subscription(() => Unsubscribe(receiver));
		}
		private bool Unsubscribe(Receiver receiver)
		{
			bool last;
			lock (_receivers) {
				if (!_receivers.Remove(receiver)) return false;
				Volatile.Write(ref receiver.State, Removed);
				last = _receivers.Count == 0;
				Volatile.Write(ref _dispatch, _receivers.ToArray());
			}
			if (last) _receiversChanged?.Invoke(false);
			return true;
		}
		// A fault reporter that throws has nowhere to report to: it must not stop the delivery that called it.
		private void ReportFault(Exception error)
		{
			try {
				_fault?.Invoke(error);
			} catch {
				// ignored, see the constructor
			}
		}
		/// <summary>
		/// Producing thread: starts the snapshot of every receiver that waits for one. When it returns true, call
		/// <see cref="PublishSnapshot"/> for the current value of every element and then <see cref="EndSnapshot"/>, before
		/// the next <see cref="Publish"/>. A snapshot that began and never ended (the producer threw) counts as aborted: its
		/// receivers get this one.
		/// </summary>
		/// <returns>True when at least one receiver waits for its snapshot.</returns>
		public bool BeginSnapshot()
		{
			if (_snapshotOpen) AbortSnapshot();
			// cleared before the receivers are read: a receiver that subscribes from now on requests the next snapshot
			if (Interlocked.Exchange(ref _snapshotRequested, 0) == 0) return false;
			bool any = false;
			foreach (var receiver in Volatile.Read(ref _dispatch)) {
				if (Interlocked.CompareExchange(ref receiver.State, InSnapshot, Waiting) != Waiting) continue;
				receiver.SnapshotFailed = false;
				any = true;
			}
			_snapshotOpen = any;
			return any;
		}
		/// <summary>
		/// Producing thread: abandons the snapshot that <see cref="BeginSnapshot"/> started, because the producer could not
		/// finish it. Its receivers get <see cref="RealtimeOutputKind.SnapshotAborted"/> instead of a boundary and wait for
		/// the next snapshot, which is requested at once. They already got the abandoned attempt's values, and the next
		/// attempt sends the current state again, so a consumer that must not act on them stages them until the boundary.
		/// </summary>
		public void AbortSnapshot()
		{
			if (!_snapshotOpen) return;
			_snapshotOpen = false;
			foreach (var receiver in Volatile.Read(ref _dispatch)) {
				if (Volatile.Read(ref receiver.State) != InSnapshot) continue;
				Call(receiver, RealtimeOutputKind.SnapshotAborted, 0, 0);
				Interlocked.CompareExchange(ref receiver.State, Waiting, InSnapshot);
			}
			Volatile.Write(ref _snapshotRequested, 1);
		}
		/// <summary>Producing thread: one element's current value, delivered only to the receivers in their snapshot.</summary>
		/// <param name="kind">What the value reports; not <see cref="RealtimeOutputKind.SnapshotBoundary"/>.</param>
		/// <param name="number">The element's number.</param>
		/// <param name="value">The element's current value.</param>
		public void PublishSnapshot(RealtimeOutputKind kind, int number, int value) => Deliver(InSnapshot, kind, number, value);
		/// <summary>
		/// Producing thread: ends the snapshot. The receivers in their snapshot get a
		/// <see cref="RealtimeOutputKind.SnapshotBoundary"/> and receive every change from now on. A receiver that threw on
		/// one of its snapshot values (or on the boundary) gets nothing more of that attempt and does not go live: it gets
		/// <see cref="RealtimeOutputKind.SnapshotAborted"/> and another snapshot, and after
		/// <see cref="MaxSnapshotAttempts"/> failed ones in a row it is dropped and reported, as
		/// if it had unsubscribed (the last one leaving is reported to <c>receiversChanged</c>).
		/// </summary>
		public void EndSnapshot()
		{
			if (!_snapshotOpen) return;
			_snapshotOpen = false;
			bool retry = false;
			foreach (var receiver in Volatile.Read(ref _dispatch)) {
				if (Volatile.Read(ref receiver.State) != InSnapshot) continue;
				if (!receiver.SnapshotFailed) Call(receiver, RealtimeOutputKind.SnapshotBoundary, 0, 0);
				if (!receiver.SnapshotFailed) {
					receiver.FailedSnapshots = 0;
					Interlocked.CompareExchange(ref receiver.State, Live, InSnapshot);
					continue;
				}
				// the attempt it failed is not a state to act on: told while it is still in its snapshot, before it waits for
				// the next one or is dropped
				Call(receiver, RealtimeOutputKind.SnapshotAborted, 0, 0);
				if (++receiver.FailedSnapshots < MaxSnapshotAttempts) {
					Interlocked.CompareExchange(ref receiver.State, Waiting, InSnapshot);
					retry = true;
				} else if (Unsubscribe(receiver)) {
					// Known limitation: this takes the receivers' lock (and may call receiversChanged) on the producing thread,
					// which can then wait for a thread that subscribes. It only happens after MaxSnapshotAttempts failures in a
					// row, and the source has no owner thread to hand the removal to.
					ReportFault(new InvalidOperationException("A real-time output receiver failed " + MaxSnapshotAttempts + " snapshots in a row and receives nothing anymore."));
				}
			}
			if (retry) Volatile.Write(ref _snapshotRequested, 1);
		}
		/// <summary>
		/// Producing thread: delivers a change to every receiver that has had its snapshot, synchronously and in subscription
		/// order. Does nothing after <see cref="Dispose"/>.
		/// </summary>
		/// <param name="kind">What the value reports; not <see cref="RealtimeOutputKind.SnapshotBoundary"/>.</param>
		/// <param name="number">The element's number.</param>
		/// <param name="value">The element's new value.</param>
		public void Publish(RealtimeOutputKind kind, int number, int value) => Deliver(Live, kind, number, value);
		private void Deliver(int state, RealtimeOutputKind kind, int number, int value)
		{
			foreach (var receiver in Volatile.Read(ref _dispatch)) {
				if (Volatile.Read(ref receiver.State) != state) continue;
				// the rest of an attempt the receiver failed is of no use to it
				if (state == InSnapshot && receiver.SnapshotFailed) continue;
				Call(receiver, kind, number, value);
			}
		}
		private void Call(Receiver receiver, RealtimeOutputKind kind, int number, int value)
		{
			try {
				receiver.Receive(kind, number, value);
			} catch (Exception error) {
				if (Volatile.Read(ref receiver.State) == InSnapshot) receiver.SnapshotFailed = true;
				if (Interlocked.Exchange(ref receiver.Reported, 1) == 0) ReportFault(error);
			}
		}
		/// <summary>Drops all receivers and refuses new ones. It does not unpublish the resource.</summary>
		public void Dispose()
		{
			bool had;
			lock (_receivers) {
				if (_disposed) return;
				_disposed = true; had = _receivers.Count > 0;
				foreach (var receiver in _receivers) Volatile.Write(ref receiver.State, Removed);
				_receivers.Clear(); Volatile.Write(ref _dispatch, Array.Empty<Receiver>());
			}
			if (had) _receiversChanged?.Invoke(false);
		}
	}
}
