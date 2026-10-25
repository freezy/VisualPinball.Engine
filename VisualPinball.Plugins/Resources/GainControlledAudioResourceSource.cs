// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Threading;

namespace VisualPinball.Plugins
{
	/// <summary>Raw PCM resource with a separate integration-owned output gain. Published frames remain unchanged.</summary>
	public sealed class GainControlledAudioResourceSource : IPluginAudioSource, IPluginAudioPlaybackControl, IPluginAudioPlaybackGainSource, IDisposable
	{
		private readonly AudioResourceSource _source;
		private readonly object _handoff = new();
		private float _gain = 1;
		public ResourceKind Kind => ResourceKind.Audio;
		public bool HasSubscribers => _source.HasSubscribers;
		public bool PlaybackReplaced => _source.PlaybackReplaced;
		public float PlaybackGain {
			get => Volatile.Read(ref _gain);
			set {
				if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
				Volatile.Write(ref _gain, value);
			}
		}
		public GainControlledAudioResourceSource(Action<Exception>? fault = null) { _source = new AudioResourceSource(fault); }
		public IDisposable Subscribe(Action<PluginPcmFrame> receive) => _source.Subscribe(receive);
		public IDisposable AcquirePlayback(string pluginId)
		{
			lock (_handoff) {
				var lease = _source.AcquirePlayback(pluginId);
				return new Subscription(() => { lock (_handoff) lease.Dispose(); });
			}
		}
		/// <summary>Snapshot ownership for this frame under the same gate as lease changes. Producers use the result instead of a later PlaybackReplaced read.</summary>
		public bool PublishForPlayback(in PluginPcmFrame frame)
		{
			lock (_handoff) { _source.Publish(frame); return _source.PlaybackReplaced; }
		}
		public void Publish(in PluginPcmFrame frame) => PublishForPlayback(frame);
		/// <summary>
		/// For producers that drain a queue only on the mixer's behalf: reads a block and publishes it while the mixer holds playback,
		/// both under the gate lease changes take, so a block read for the mixer is never lost to a lease that ended in between.
		/// Returns false, without reading, when playback isn't replaced.
		/// </summary>
		public bool PublishWhileReplaced(Func<PluginPcmFrame?> read)
		{
			lock (_handoff) {
				if (!_source.PlaybackReplaced) return false;
				var frame = read();
				if (frame.HasValue) _source.Publish(frame.Value);
				return true;
			}
		}
		public void Dispose() => _source.Dispose();
	}
}
