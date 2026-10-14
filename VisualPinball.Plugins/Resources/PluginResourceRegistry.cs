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
		public PluginResourceRegistry(Action<Exception>? fault = null) { _fault = fault ?? (_ => { }); }
		internal void Report(Exception error) => _fault(error);
		public IReadOnlyList<PluginResourceDescriptor> Snapshot() => _entries.Values.Where(entry => Reachable(entry, new HashSet<string>())).Select(entry => entry.Descriptor).OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal).ToArray();
		public IReadOnlyList<PluginResourceDescriptor> Snapshot(PluginResourceSelection selection) => Candidates(selection).Select(entry => entry.Descriptor).ToArray();
		internal IReadOnlyList<Entry> Candidates(PluginResourceSelection selection)
		{
			var found = _entries.Values.Where(entry => selection.Matches(entry.Descriptor) && Reachable(entry, new HashSet<string>())).OrderBy(entry => entry.Descriptor.Id, StringComparer.Ordinal).ToList();
			if (selection.ResourceId.Length == 0 && selection.Intent == ResourceView.Presentation && found.Any(entry => entry.Descriptor.View == ResourceView.Presentation))
				found.RemoveAll(entry => entry.Descriptor.View != ResourceView.Presentation);
			return found;
		}
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
		public IDisposable Observe(Action<PluginResourceChange> changed)
		{
			_observers.Add(changed);
			return new Subscription(() => _observers.Remove(changed));
		}
		private void Notify(PluginResourceChange change) { foreach (var observer in _observers.ToArray()) try { observer(change); } catch (Exception error) { Report(error); } }
		public PluginResourceBinding<T> Bind<T>(PluginResourceSelection selection) where T : class, IPluginResourceSource => new PluginResourceBinding<T>(this, selection);
	}

	/// <summary>A binding follows its selected logical resource and owns view negotiation for the current instance.</summary>
	public sealed class PluginResourceBinding<T> : IDisposable where T : class, IPluginResourceSource
	{
		private readonly PluginResourceRegistry _registry;
		private readonly PluginResourceSelection _selection;
		private readonly IDisposable _watch;
		private IDisposable? _view;
		private readonly List<Func<T, IDisposable>> _receivers = new List<Func<T, IDisposable>>();
		private readonly List<IDisposable> _attached = new List<IDisposable>();
		public T? Source { get; private set; }
		public PluginResourceDescriptor? Descriptor { get; private set; }
		public ResourceBindingState State { get; private set; }
		public string Diagnostic { get; private set; } = "";
		public DisplayEncoding ActualEncoding { get; private set; }
		public DisplayEncoding AcceptedFormats => _selection.Accepts;
		public event Action? Changed;
		internal PluginResourceBinding(PluginResourceRegistry registry, PluginResourceSelection selection)
		{
			_registry = registry; _selection = selection.Copy();
			_watch = registry.Observe(_ => Refresh());
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
		public IDisposable Connect(Func<T, IDisposable> subscribe)
		{
			if (State == ResourceBindingState.Disposed) throw new ObjectDisposedException(nameof(PluginResourceBinding<T>));
			_receivers.Add(subscribe);
			RefreshAttachments();
			return new Subscription(() => { _receivers.Remove(subscribe); RefreshAttachments(); });
		}
		private void Refresh()
		{
			if (State == ResourceBindingState.Disposed) return;
			var candidates = _registry.Candidates(_selection).Where(entry => entry.Source is T).ToArray();
			if (candidates.Length == 1 && ReferenceEquals(Source, candidates[0].Source) && Descriptor?.Instance == candidates[0].Descriptor.Instance) return;
			Detach();
			if (candidates.Length == 0) { State = ResourceBindingState.Waiting; Diagnostic = _selection.ResourceId.Length == 0 ? "Waiting for a compatible resource." : "Missing resource: " + _selection.ResourceId; }
			else if (candidates.Length > 1) { State = ResourceBindingState.Blocked; Diagnostic = "Select a resource: " + string.Join(", ", candidates.Select(entry => entry.Descriptor.Id)); }
			else {
				Source = (T)candidates[0].Source;
				Descriptor = candidates[0].Descriptor;
				try {
					_view = (Source as IPluginDisplaySource)?.AcquireView(_selection.Intent, _selection.Accepts);
					State = ResourceBindingState.Bound; Diagnostic = ""; RefreshAttachments();
				} catch (Exception error) { Detach(); State = ResourceBindingState.Blocked; Diagnostic = error.Message; }
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
			try { _view?.Dispose(); } catch (Exception error) { _registry.Report(error); }
			_view = null; Source = null; Descriptor = null; ActualEncoding = DisplayEncoding.None;
		}
		private void ClearAttachments()
		{
			foreach (var attached in _attached) try { attached.Dispose(); } catch (Exception error) { _registry.Report(error); }
			_attached.Clear();
		}
		public void Dispose() { if (State == ResourceBindingState.Disposed) return; _watch.Dispose(); Detach(); _receivers.Clear(); State = ResourceBindingState.Disposed; Changed = null; }
	}

	public static class DisplayBindingExtensions
	{
		/// <summary>Checks the concrete encoding and generation before delivering borrowed pixels.</summary>
		public static IDisposable SubscribeFrames(this PluginResourceBinding<IPluginDisplaySource> binding, Action<PluginDisplayFrame> receive)
			=> binding.Connect(source => source.Subscribe(frame => { if (binding.AcceptFrame(frame)) receive(frame); }));
	}
}
