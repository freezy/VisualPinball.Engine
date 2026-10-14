// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// The directory of live resources (displays, controller events, audio, segment displays, surfaces…) that producers
	/// publish and consumers bind to by description rather than by reference.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The Player owns one registry and passes it to plugins as <see cref="PluginApplicationContext.Resources"/> and
	/// <see cref="PluginGameContext.Resources"/>. For each game the Unity integration publishes the gamelogic engine's
	/// controller, native outputs, audio, DMDs and segment displays under <c>&lt;game id&gt;/controller/0</c>. Plugins
	/// publish their own: the DMD plugin a colorized presentation of a DMD, PinUP a surface per screen, the B2S plugin its
	/// backglass surface. Consumers describe what they want with a <see cref="PluginResourceSelection"/> and
	/// <see cref="Bind{T}"/> to it; the binding follows resources as they come and go, so producers and consumers can start
	/// in any order.
	/// </para>
	/// <para>
	/// A resource can be derived from others (<see cref="PluginResourceDescriptor.Parents"/>). It stays visible only while
	/// every parent is still published with the same <see cref="PluginResourceDescriptor.Instance"/> and the same
	/// <see cref="PluginResourceDescriptor.Session"/>, so a presentation built on a DMD disappears when that DMD is replaced.
	/// </para>
	/// <para>Not thread-safe: publish, bind and observe on the owner thread.</para>
	/// <example>
	/// A plugin publishes a surface for the running game and withdraws it with the game:
	/// <code>
	/// game.Lifetime.Own(game.Resources.Publish(new PluginResourceDescriptor(game.ControllerId + "/surface/my-screen", _application.Plugin.Id,
	///     game.ControllerId, "My screen", "backglass", ResourceKind.Surface, game.Session, Interlocked.Increment(ref _instances)), surface));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginResourceRegistry
	{
		internal sealed class Entry
		{
			internal readonly PluginResourceDescriptor Descriptor;
			internal readonly IPluginResourceSource Source;
			internal readonly Dictionary<string, long> ParentInstances;
			internal Entry(PluginResourceDescriptor descriptor, IPluginResourceSource source, Dictionary<string, long> parents) { Descriptor = descriptor; Source = source; ParentInstances = parents; }
		}
		private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
		private readonly List<Action<PluginResourceChange>> _observers = new List<Action<PluginResourceChange>>();
		private readonly Action<Exception> _fault;
		/// <summary>Creates an empty registry. The Player creates one for the application.</summary>
		/// <param name="fault">
		/// Receives exceptions thrown by observers, binding <c>Changed</c> handlers and attachment cleanup, which are caught so
		/// one faulty plugin cannot break the others. When null, they are silently dropped.
		/// </param>
		public PluginResourceRegistry(Action<Exception>? fault = null) { _fault = fault ?? (_ => { }); }
		internal void Report(Exception error) => _fault(error);
		/// <summary>
		/// Returns the descriptors of all visible resources, sorted by ID. Derived resources whose parents are gone or were
		/// replaced are left out.
		/// </summary>
		/// <remarks>
		/// The Player passes this list to settings providers as <see cref="PluginChoiceRequest.Resources"/>, so choices such as
		/// <see cref="PluginResourceChoices.Displays"/> can list the running game's displays.
		/// </remarks>
		public IReadOnlyList<PluginResourceDescriptor> Snapshot() => _entries.Values.Where(entry => Reachable(entry, new HashSet<string>())).Select(entry => entry.Descriptor).OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal).ToArray();
		/// <summary>
		/// Returns the descriptors a binding with <paramref name="selection"/> would consider, sorted by ID: none means a
		/// binding would wait, more than one means it would be blocked until the user picks one.
		/// </summary>
		/// <remarks>
		/// Useful to probe before binding. The B2S plugin checks whether the controller reports native outputs and binds to
		/// <see cref="ResourceKind.Outputs"/> if so, otherwise to the mapped <see cref="ResourceKind.Controller"/> events.
		/// Unlike a binding, this does not filter by source interface.
		/// </remarks>
		/// <param name="selection">What to look for.</param>
		public IReadOnlyList<PluginResourceDescriptor> Snapshot(PluginResourceSelection selection) => Candidates(selection).Select(entry => entry.Descriptor).ToArray();
		internal IReadOnlyList<Entry> Candidates(PluginResourceSelection selection)
		{
			var found = _entries.Values.Where(entry => selection.Matches(entry.Descriptor) && Reachable(entry, new HashSet<string>())).OrderBy(entry => entry.Descriptor.Id, StringComparer.Ordinal).ToList();
			if (selection.Intent == ResourceView.Presentation && found.Any(entry => entry.Descriptor.View == ResourceView.Presentation))
				found.RemoveAll(entry => entry.Descriptor.View != ResourceView.Presentation);
			return found;
		}
		/// <summary>
		/// Publishes a resource until the returned handle is disposed, and notifies observers and bindings with
		/// <see cref="ResourceChangeKind.Added"/>. Disposing the handle removes it again with
		/// <see cref="ResourceChangeKind.Removed"/>.
		/// </summary>
		/// <remarks>
		/// The source must implement the payload contract of the descriptor's kind: <see cref="IPluginDisplaySource"/> for
		/// <see cref="ResourceKind.Display"/>, <see cref="IPluginControllerSource"/> for <see cref="ResourceKind.Controller"/>
		/// and <see cref="ResourceKind.Outputs"/>, <see cref="IPluginAudioSource"/>, <see cref="IPluginSegmentSource"/>,
		/// <see cref="IPluginInputSource"/>, <see cref="IPluginSurface"/> or <see cref="IPluginTriggerSource"/> for the others.
		/// Parents must be published first. To replace a resource, dispose the old publication and publish again with a new
		/// <see cref="PluginResourceDescriptor.Instance"/>, so bindings rebind and derived resources built on the old one
		/// disappear. Publishing does not take ownership of the source; dispose it separately.
		/// </remarks>
		/// <typeparam name="T">The source type.</typeparam>
		/// <param name="descriptor">Identity and metadata of the resource. Its ID must be unique in the registry.</param>
		/// <param name="source">The object consumers receive; its <see cref="IPluginResourceSource.Kind"/> must equal the descriptor's.</param>
		/// <returns>The publication handle; disposing it more than once does nothing.</returns>
		/// <exception cref="ArgumentException">
		/// The source's kind differs from the descriptor's, or the source does not implement the kind's contract.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The ID is already published, a parent is not published, or the parents would form a cycle.
		/// </exception>
		public IDisposable Publish<T>(PluginResourceDescriptor descriptor, T source) where T : IPluginResourceSource
		{
			if (descriptor.Kind != source.Kind) throw new ArgumentException("Resource kind and VPE source interface disagree.");
			if (descriptor.Kind == ResourceKind.Display && !(source is IPluginDisplaySource)
				|| (descriptor.Kind == ResourceKind.Controller || descriptor.Kind == ResourceKind.Outputs) && !(source is IPluginControllerSource)
				|| descriptor.Kind == ResourceKind.Audio && !(source is IPluginAudioSource)
				|| descriptor.Kind == ResourceKind.Segments && !(source is IPluginSegmentSource)
				|| descriptor.Kind == ResourceKind.Input && !(source is IPluginInputSource)
				|| descriptor.Kind == ResourceKind.Surface && !(source is IPluginSurface)
				|| descriptor.Kind == ResourceKind.Triggers && !(source is IPluginTriggerSource)) throw new ArgumentException("The resource must implement its VPE payload contract.");
			if (_entries.ContainsKey(descriptor.Id)) throw new InvalidOperationException("Resource already published: " + descriptor.Id);
			foreach (string parent in descriptor.Parents)
				if (!_entries.ContainsKey(parent)) throw new InvalidOperationException("Publish the source before its derived resource: " + parent);
			foreach (string parent in descriptor.Parents)
				if (parent == descriptor.Id || Reaches(parent, descriptor.Id, new HashSet<string>())) throw new InvalidOperationException("Resource derivation cycle: " + descriptor.Id);
			var parentInstances = descriptor.Parents.ToDictionary(id => id, id => _entries.TryGetValue(id, out var parent) ? parent.Descriptor.Instance : 0L, StringComparer.Ordinal);
			var entry = new Entry(descriptor, source, parentInstances);
			_entries.Add(descriptor.Id, entry);
			Notify(new PluginResourceChange(ResourceChangeKind.Added, descriptor));
			return new Subscription(() => {
				if (_entries.TryGetValue(descriptor.Id, out var current) && ReferenceEquals(current, entry)) {
					_entries.Remove(descriptor.Id); Notify(new PluginResourceChange(ResourceChangeKind.Removed, descriptor));
				}
			});
		}
		private bool Reaches(string from, string target, HashSet<string> visited) => visited.Add(from) && _entries.TryGetValue(from, out var entry)
			&& entry.Descriptor.Parents.Any(parent => parent == target || Reaches(parent, target, visited));
		private bool Reachable(Entry entry, HashSet<string> path)
		{
			if (!path.Add(entry.Descriptor.Id)) return false;
			foreach (string parentId in entry.Descriptor.Parents) {
				if (!_entries.TryGetValue(parentId, out var parent) || !Reachable(parent, path)) return false;
				if (parent.Descriptor.Session != entry.Descriptor.Session) return false;
				long expected = entry.ParentInstances[parentId];
				if (expected != parent.Descriptor.Instance) return false;
				path.Remove(parentId);
			}
			return true;
		}
		/// <summary>
		/// Tells observers and bindings that a published resource changed in a way that may let a refused consumer bind
		/// now, for example when a display gave up the identification encoding another consumer could not accept.
		/// Bindings that are blocked or waiting on the resource retry; bound bindings are unaffected.
		/// </summary>
		/// <param name="resourceId">The resource's <see cref="PluginResourceDescriptor.Id"/>; unknown IDs are ignored.</param>
		public void NotifyChanged(string resourceId)
		{
			if (_entries.TryGetValue(resourceId, out var entry)) Notify(new PluginResourceChange(ResourceChangeKind.Changed, entry.Descriptor));
		}
		/// <summary>
		/// Calls <paramref name="changed"/> on every publication, withdrawal and change notice until the returned handle is
		/// disposed: <see cref="ResourceChangeKind.Added"/>, <see cref="ResourceChangeKind.Removed"/>, and
		/// <see cref="ResourceChangeKind.Changed"/> from <see cref="NotifyChanged(string)"/>. Existing resources
		/// are not replayed; read <see cref="Snapshot(PluginResourceSelection)"/> first.
		/// </summary>
		/// <remarks>
		/// Bindings use this internally. Use it directly to follow a varying number of resources, as the B2S plugin does to
		/// attach to every segment display of the game. Exceptions from the callback go to the registry's fault handler.
		/// </remarks>
		/// <param name="changed">Called synchronously on the publishing thread.</param>
		/// <returns>A handle that stops the observation.</returns>
		public IDisposable Observe(Action<PluginResourceChange> changed)
		{
			_observers.Add(changed);
			return new Subscription(() => _observers.Remove(changed));
		}
		private void Notify(PluginResourceChange change) { foreach (var observer in _observers.ToArray()) try { observer(change); } catch (Exception error) { Report(error); } }
		/// <summary>
		/// Creates a binding that tracks the single resource matching <paramref name="selection"/> whose source implements
		/// <typeparamref name="T"/>. The binding resolves immediately and again on every registry change; dispose it to stop.
		/// </summary>
		/// <typeparam name="T">The payload contract the consumer needs, such as <see cref="IPluginDisplaySource"/>.</typeparam>
		/// <param name="selection">What to bind to. It is copied, so later changes to it have no effect.</param>
		/// <returns>The binding; own it by the game's <see cref="ResourceScope"/>.</returns>
		/// <remarks>
		/// <example>
		/// PinUP binds the game's DMD for identification frames:
		/// <code>
		/// var displays = new PluginResourceSelection(ResourceKind.Display) { Controller = game.ControllerId, Role = "dmd", Session = game.Session,
		///     Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray2 | DisplayEncoding.Gray4 };
		/// var dmd = game.Lifetime.Own(game.Resources.Bind&lt;IPluginDisplaySource&gt;(displays));
		/// game.Lifetime.Own(dmd.SubscribeFrames(frame =&gt; RenderDmdShades(frame)));
		/// </code>
		/// </example>
		/// </remarks>
		public PluginResourceBinding<T> Bind<T>(PluginResourceSelection selection) where T : class, IPluginResourceSource => new PluginResourceBinding<T>(this, selection);
	}

	/// <summary>
	/// A consumer's live connection to the one resource that matches a selection. It rebinds automatically when that resource
	/// is published, replaced or withdrawn, and for displays it holds the negotiated view while bound.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Created by <see cref="PluginResourceRegistry.Bind{T}"/>. The binding is <see cref="ResourceBindingState.Waiting"/>
	/// while nothing matches, <see cref="ResourceBindingState.Bound"/> when exactly one source matches,
	/// <see cref="ResourceBindingState.Blocked"/> when several match (the user must choose one) or the source refused the
	/// requested view, and <see cref="ResourceBindingState.Disposed"/> after <see cref="Dispose"/>. <see cref="Diagnostic"/>
	/// explains the non-bound states; plugins log it or show it in their status.
	/// </para>
	/// <para>
	/// When a display binds, it calls <see cref="IPluginDisplaySource.AcquireView"/> with the selection's
	/// <see cref="PluginResourceSelection.Intent"/> and <see cref="PluginResourceSelection.Accepts"/> and keeps the lease
	/// until it unbinds. For an identification binding on a PinMAME DMD this switches the ROM's frames to raw shades.
	/// </para>
	/// <para>Not thread-safe: use it on the owner thread, where registry changes and frames arrive.</para>
	/// <example>
	/// The B2S plugin connects to controller events and the source is re-attached whenever the binding rebinds:
	/// <code>
	/// var binding = game.Lifetime.Own(game.Resources.Bind&lt;IPluginControllerSource&gt;(selection));
	/// game.Lifetime.Own(binding.Connect(source =&gt; source.Subscribe(ControllerEvent)));
	/// </code>
	/// </example>
	/// </remarks>
	/// <typeparam name="T">The payload contract the consumer uses.</typeparam>
	public sealed class PluginResourceBinding<T> : IDisposable where T : class, IPluginResourceSource
	{
		private readonly PluginResourceRegistry _registry;
		private readonly PluginResourceSelection _selection;
		private readonly IDisposable _watch;
		private IDisposable? _view;
		private readonly List<Func<T, IDisposable>> _receivers = new List<Func<T, IDisposable>>();
		private readonly List<IDisposable> _attached = new List<IDisposable>();
		/// <summary>The bound source, or null unless <see cref="State"/> is <see cref="ResourceBindingState.Bound"/>.</summary>
		public T? Source { get; private set; }
		/// <summary>
		/// Descriptor of the bound resource, or null unless bound. The DMD plugin reads it to derive its presentation resource
		/// from the bound DMD.
		/// </summary>
		public PluginResourceDescriptor? Descriptor { get; private set; }
		/// <summary>Whether the binding is waiting, bound, blocked or disposed; see the class remarks.</summary>
		public ResourceBindingState State { get; private set; }
		/// <summary>
		/// Human-readable reason when the binding is not bound or the bound display sends frames in an encoding the selection
		/// does not accept, such as <c>Select a resource: …</c> or <c>Missing resource: …</c>. Empty when everything is fine.
		/// </summary>
		public string Diagnostic { get; private set; } = "";
		/// <summary>
		/// Encoding of the most recent frame received through <see cref="DisplayBindingExtensions.SubscribeFrames"/>, whether it
		/// was accepted or not. <see cref="DisplayEncoding.None"/> before the first frame and after unbinding.
		/// </summary>
		public DisplayEncoding ActualEncoding { get; private set; }
		/// <summary>The encodings this binding accepts, from <see cref="PluginResourceSelection.Accepts"/>.</summary>
		public DisplayEncoding AcceptedFormats => _selection.Accepts;
		/// <summary>
		/// Raised on the owner thread when the binding re-resolves (bound, unbound, blocked, rebound to a new instance) and when
		/// <see cref="ActualEncoding"/> or <see cref="Diagnostic"/> change because of a frame. Handler exceptions go to the
		/// registry's fault handler. All handlers are dropped on <see cref="Dispose"/>.
		/// </summary>
		/// <remarks>
		/// The B2S plugin clears the live DMD on the glass here, so a lost source does not leave its last frame behind; the DMD
		/// plugin logs <see cref="Diagnostic"/> when the binding is not bound.
		/// </remarks>
		public event Action? Changed;
		internal PluginResourceBinding(PluginResourceRegistry registry, PluginResourceSelection selection)
		{
			_registry = registry; _selection = selection.Copy();
			_watch = registry.Observe(Refresh);
			Refresh();
		}
		internal bool AcceptFrame(in PluginDisplayFrame frame)
		{
			if (State != ResourceBindingState.Bound || Descriptor == null || frame.Instance != Descriptor.Instance) return false;
			bool compatible = (frame.Encoding & _selection.Accepts) != 0;
			string diagnostic = compatible ? "" : "Source is producing " + frame.Encoding + "; accepted formats: " + _selection.Accepts + ".";
			if (ActualEncoding != frame.Encoding || Diagnostic != diagnostic) {
				ActualEncoding = frame.Encoding; Diagnostic = diagnostic;
				try { Changed?.Invoke(); } catch (Exception error) { _registry.Report(error); }
			}
			return compatible;
		}
		/// <summary>
		/// Attaches a receiver to whatever source is bound, now and after every rebind. <paramref name="subscribe"/> is called
		/// with the current source and returns the subscription to dispose when that source goes away.
		/// </summary>
		/// <remarks>
		/// Connecting or disconnecting a receiver re-attaches all receivers of this binding. For display frames use
		/// <see cref="DisplayBindingExtensions.SubscribeFrames"/>, which also checks the frame's instance and encoding.
		/// </remarks>
		/// <param name="subscribe">Subscribes to a source and returns the handle that undoes it.</param>
		/// <returns>A handle that detaches the receiver.</returns>
		/// <exception cref="ObjectDisposedException">The binding has been disposed.</exception>
		public IDisposable Connect(Func<T, IDisposable> subscribe)
		{
			if (State == ResourceBindingState.Disposed) throw new ObjectDisposedException(nameof(PluginResourceBinding<T>));
			_receivers.Add(subscribe);
			RefreshAttachments();
			return new Subscription(() => { _receivers.Remove(subscribe); RefreshAttachments(); });
		}
		private bool _refreshing, _refreshAgain;
		// ID of the resource whose view this binding is releasing, while it does.
		private string? _releasingId;
		private void Refresh(PluginResourceChange change)
		{
			// A change notice for a resource this binding does not select cannot affect it, and the one caused by releasing its
			// own view says nothing new to it. Retrying on either would repeat a failed attempt forever, for example when an
			// engine-wide format lease notifies every display of the engine. Publications and withdrawals always count.
			if (change.Kind == ResourceChangeKind.Changed && (change.Descriptor.Id == _releasingId || !_selection.Matches(change.Descriptor))) return;
			Refresh();
		}
		private void Refresh()
		{
			// Notices during a pass are handled by one more pass afterwards, which reads the candidates again.
			if (_refreshing) { _refreshAgain = true; return; }
			_refreshing = true;
			try {
				do { _refreshAgain = false; RefreshOnce(); } while (_refreshAgain && State != ResourceBindingState.Disposed);
			} finally { _refreshing = false; }
		}
		private void RefreshOnce()
		{
			if (State == ResourceBindingState.Disposed) return;
			var candidates = _registry.Candidates(_selection).Where(entry => entry.Source is T).ToArray();
			if (candidates.Length == 1 && ReferenceEquals(Source, candidates[0].Source) && Descriptor?.Instance == candidates[0].Descriptor.Instance) return;
			Detach();
			// Releasing the old view runs observers, which may have disposed this binding.
			if (State == ResourceBindingState.Disposed) return;
			if (candidates.Length == 0) { State = ResourceBindingState.Waiting; Diagnostic = _selection.ResourceId.Length == 0 ? "Waiting for a compatible resource." : "Missing resource: " + _selection.ResourceId; }
			else if (candidates.Length > 1) { State = ResourceBindingState.Blocked; Diagnostic = "Select a resource: " + string.Join(", ", candidates.Select(entry => entry.Descriptor.Id)); }
			else {
				Source = (T)candidates[0].Source;
				Descriptor = candidates[0].Descriptor;
				try {
					_view = (Source as IPluginDisplaySource)?.AcquireView(_selection.Intent, _selection.Accepts);
					State = ResourceBindingState.Bound; Diagnostic = ""; RefreshAttachments();
				} catch (Exception error) {
					Detach();
					if (State == ResourceBindingState.Disposed) return;
					State = ResourceBindingState.Blocked; Diagnostic = error.Message;
				}
			}
			try { Changed?.Invoke(); } catch (Exception error) { _registry.Report(error); }
		}
		private void RefreshAttachments()
		{
			ClearAttachments();
			if (Source != null && State != ResourceBindingState.Disposed)
				foreach (var receive in _receivers) _attached.Add(receive(Source));
		}
		private void Detach()
		{
			ClearAttachments();
			_releasingId = Descriptor?.Id;
			try { _view?.Dispose(); } catch (Exception error) { _registry.Report(error); } finally { _releasingId = null; }
			_view = null; Source = null; Descriptor = null; ActualEncoding = DisplayEncoding.None;
		}
		private void ClearAttachments()
		{
			foreach (var attached in _attached) try { attached.Dispose(); } catch (Exception error) { _registry.Report(error); }
			_attached.Clear();
		}
		/// <summary>
		/// Stops following the registry, detaches all receivers, releases the view lease and sets <see cref="State"/> to
		/// <see cref="ResourceBindingState.Disposed"/>. Does not raise <see cref="Changed"/>. Later calls do nothing.
		/// </summary>
		public void Dispose()
		{
			if (State == ResourceBindingState.Disposed) return;
			// Disposed first, so a refresh that is running further up the stack does not revive the binding.
			State = ResourceBindingState.Disposed;
			_watch.Dispose(); Detach(); _receivers.Clear(); Changed = null;
		}
	}

	/// <summary>Display-specific helpers for <see cref="PluginResourceBinding{T}"/>.</summary>
	public static class DisplayBindingExtensions
	{
		/// <summary>
		/// Receives the frames of whatever display the binding is bound to, across rebinds. Frames from a previous instance and
		/// frames in an encoding the selection does not accept are dropped; the latter set <see cref="PluginResourceBinding{T}.Diagnostic"/>.
		/// </summary>
		/// <remarks>
		/// The frame and its pixels are borrowed: copy what you need before returning. Called synchronously on the thread that
		/// publishes the frame, the owner thread for VPE's sources.
		/// <example>
		/// <code>
		/// game.Lifetime.Own(binding.SubscribeFrames(frame =&gt; _bridge.FeedFrame(frame, displayId)));
		/// </code>
		/// </example>
		/// </remarks>
		/// <param name="binding">A display binding.</param>
		/// <param name="receive">Called for every accepted frame.</param>
		/// <returns>A handle that stops delivery; own it by the same scope as the binding.</returns>
		/// <exception cref="ObjectDisposedException">The binding has been disposed.</exception>
		public static IDisposable SubscribeFrames(this PluginResourceBinding<IPluginDisplaySource> binding, Action<PluginDisplayFrame> receive)
			=> binding.Connect(source => source.Subscribe(frame => { if (binding.AcceptFrame(frame)) receive(frame); }));
	}
}
