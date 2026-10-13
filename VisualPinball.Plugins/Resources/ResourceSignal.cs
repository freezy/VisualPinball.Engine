// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Threading;

namespace VisualPinball.Plugins
{
	/// <summary>Subscription changes take a lock; synchronous producer dispatch reads a cached array.</summary>
	internal sealed class ResourceSignal<T> : IDisposable where T : struct
	{
		private readonly List<Action<T>> _subscribers = new List<Action<T>>();
		private readonly Action<Exception> _fault;
		private Action<T>[] _dispatch = Array.Empty<Action<T>>();
		private bool _disposed;
		internal ResourceSignal(Action<Exception>? fault = null) { _fault = fault ?? (_ => { }); }
		internal bool HasSubscribers => Volatile.Read(ref _dispatch).Length > 0;
		internal IDisposable Subscribe(Action<T> receive)
		{
			lock (_subscribers) {
				if (_disposed) throw new ObjectDisposedException(nameof(ResourceSignal<T>));
				_subscribers.Add(receive); Volatile.Write(ref _dispatch, _subscribers.ToArray());
			}
			return new Subscription(() => { lock (_subscribers) { _subscribers.Remove(receive); Volatile.Write(ref _dispatch, _subscribers.ToArray()); } });
		}
		internal void Publish(in T frame) { foreach (var receive in Volatile.Read(ref _dispatch)) try { receive(frame); } catch (Exception error) { _fault(error); } }
		public void Dispose() { lock (_subscribers) { _disposed = true; _subscribers.Clear(); Volatile.Write(ref _dispatch, Array.Empty<Action<T>>()); } }
	}
	public sealed class AudioResourceSource : IPluginAudioSource, IPluginAudioPlaybackControl, IDisposable
	{
		private readonly ResourceSignal<PluginPcmFrame> _signal;
		private readonly PluginOutputLeases _output = new PluginOutputLeases();
		public ResourceKind Kind => ResourceKind.Audio;
		public bool HasSubscribers => _signal.HasSubscribers;
		public bool PlaybackReplaced => _output.Owner("playback") != null;
		public AudioResourceSource(Action<Exception>? fault = null) { _signal = new ResourceSignal<PluginPcmFrame>(fault); }
		public IDisposable Subscribe(Action<PluginPcmFrame> receive) => _signal.Subscribe(receive);
		public IDisposable AcquirePlayback(string pluginId) => _output.Acquire("playback", pluginId);
		public void Publish(in PluginPcmFrame frame) => _signal.Publish(frame);
		public void Dispose() => _signal.Dispose();
	}
	public sealed class SegmentResourceSource : IPluginSegmentSource, IDisposable
	{
		private readonly ResourceSignal<PluginSegmentFrame> _signal;
		public ResourceKind Kind => ResourceKind.Segments;
		public SegmentResourceSource(Action<Exception>? fault = null) { _signal = new ResourceSignal<PluginSegmentFrame>(fault); }
		public IDisposable Subscribe(Action<PluginSegmentFrame> receive) => _signal.Subscribe(receive);
		public void Publish(in PluginSegmentFrame frame) => _signal.Publish(frame);
		public void Dispose() => _signal.Dispose();
	}
	public sealed class InputResourceSource : IPluginInputSource, IDisposable
	{
		private readonly ResourceSignal<PluginInputEvent> _signal;
		public ResourceKind Kind => ResourceKind.Input;
		public InputResourceSource(Action<Exception>? fault = null) { _signal = new ResourceSignal<PluginInputEvent>(fault); }
		public IDisposable Subscribe(Action<PluginInputEvent> receive) => _signal.Subscribe(receive);
		public void Publish(in PluginInputEvent value) => _signal.Publish(value);
		public void Dispose() => _signal.Dispose();
	}
	public sealed class TriggerResourceSource : IPluginTriggerSource, IDisposable
	{
		private readonly ResourceSignal<PluginTriggerEvent> _signal;
		public ResourceKind Kind => ResourceKind.Triggers;
		public TriggerResourceSource(Action<Exception>? fault = null) { _signal = new ResourceSignal<PluginTriggerEvent>(fault); }
		public IDisposable Subscribe(Action<PluginTriggerEvent> receive) => _signal.Subscribe(receive);
		public void Publish(in PluginTriggerEvent value) => _signal.Publish(value);
		public void Dispose() => _signal.Dispose();
	}
}
