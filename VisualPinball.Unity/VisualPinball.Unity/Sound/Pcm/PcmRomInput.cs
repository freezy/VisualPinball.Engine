// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Threading;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>Copies borrowed ROM frames before returning, and owns exactly one playback lease.</summary>
	internal sealed class PcmRomInput : IDisposable
	{
		private readonly object _gate = new();
		private readonly NativePcmSession _session;
		private readonly IPluginAudioPlaybackGainSource _gain;
		private IDisposable _subscription, _lease;
		private bool _accept, _paused, _disposed;
		private int _rate, _channels;
		private PcmEncoding _encoding;
		private string _fault;
		internal string Fault => Volatile.Read(ref _fault);
		internal PcmRomInput(NativePcmSession session, IPluginAudioSource source)
		{
			_session = session; _gain = source as IPluginAudioPlaybackGainSource;
			if (_gain == null) throw new InvalidOperationException("ROM provider has no playback-gain metadata; preserving integration playback.");
			if (!(source is IPluginAudioPlaybackControl control)) throw new InvalidOperationException("ROM playback is externally owned: no takeover capability.");
			try {
				PublishGain();
				_subscription = source.Subscribe(Receive);
				_lease = control.AcquirePlayback("vpe.audio");
				lock (_gate) { NativePcmSession.VpePcmEnableStream(_session.Context, 1); _accept = true; }
			} catch { Dispose(); throw; }
		}
		internal void PublishGain()
		{
			float gain = _gain?.PlaybackGain ?? 1;
			if (float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0 || gain > 1) throw new InvalidOperationException("The ROM integration supplied an invalid playback gain.");
			NativePcmSession.VpePcmSetStreamGain(_session.Context, gain);
		}
		private unsafe void Receive(PluginPcmFrame frame)
		{
			lock (_gate) {
				if (!_accept || _paused || _disposed || Fault != null || frame.Frames == 0) return;
				if ((frame.Channels != 1 && frame.Channels != 2) || frame.SampleRate < 8000 || frame.SampleRate > 192000 || frame.Frames > 8192 ||
					(!string.IsNullOrEmpty(frame.ChannelLayout) && !string.Equals(frame.ChannelLayout, frame.Channels == 1 ? "mono" : "stereo", StringComparison.OrdinalIgnoreCase) && frame.ChannelLayout != (frame.Channels == 1 ? "M" : "L,R")) ||
					(frame.Encoding != PcmEncoding.Float32 && frame.Encoding != PcmEncoding.Int16LittleEndian) ||
					(_rate != 0 && (_rate != frame.SampleRate || _channels != frame.Channels || _encoding != frame.Encoding))) {
					Volatile.Write(ref _fault, "Unsupported or changed ROM PCM format; restored integration playback."); return;
				}
				_rate = frame.SampleRate; _channels = frame.Channels; _encoding = frame.Encoding;
				PublishGain(); // Apply producer-side mute before publishing this block, without waiting for Update.
				fixed (byte* samples = frame.Samples.Span)
					NativePcmSession.VpePcmStreamPush(_session.Context, (IntPtr)samples, frame.Frames, frame.Channels, frame.SampleRate, (int)frame.Encoding, frame.Sequence);
			}
		}
		internal void SetPaused(bool paused)
		{
			lock (_gate) {
				if (_paused == paused || _disposed) return;
				_paused = paused; NativePcmSession.VpePcmFlushStream(_session.Context);
			}
		}
		public void Dispose()
		{
			IDisposable lease;
			lock (_gate) {
				if (_disposed) return;
				_disposed = true; _accept = false;
				_subscription?.Dispose(); _subscription = null;
				NativePcmSession.VpePcmEnableStream(_session.Context, 0);
				NativePcmSession.VpePcmFlushStream(_session.Context);
				lease = _lease; _lease = null;
			}
			// Publisher dispatch can hold the producer's handoff gate while entering Receive.
			// Never acquire that handoff gate while holding our receive gate.
			lease?.Dispose();
		}
	}
}
