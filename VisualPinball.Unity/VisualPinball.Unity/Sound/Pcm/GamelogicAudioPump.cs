// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Threading;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Hands an emulator's audio queue to VPE's central mixer, for gamelogic engines that otherwise play it themselves in
	/// <c>OnAudioFilterRead</c>. The engine exposes <see cref="Source"/> through <see cref="IGamelogicAudioResourceProvider"/>;
	/// while the mixer holds the playback lease, a thread drains the queue and publishes 16-bit blocks, and the engine's own
	/// filter stays silent.
	/// </summary>
	/// <remarks>
	/// The engine's filter reads its queue under <see cref="ReadLock"/> and only when <see cref="LocalPlayback"/> is true,
	/// so exactly one side consumes the queue. Without the mixer (Standard playback) nothing changes for the engine.
	/// <example>
	/// <code>
	/// _audioPump = new GamelogicAudioPump((buffer, frames) => (int)Native.ReadAudio(_emu, buffer, (UIntPtr)frames), () => Native.GetAudioRate(_emu));
	/// IPluginAudioSource IGamelogicAudioResourceProvider.AudioSource => _audioPump.Source;
	/// void OnAudioFilterRead(float[] data, int channels) { if (!_audioPump.LocalPlayback) return; lock (_audioPump.ReadLock) { ... } }
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class GamelogicAudioPump : IDisposable
	{
		/// <summary>Reads up to the given number of stereo frames into the buffer and returns how many it read.</summary>
		public delegate int Reader(short[] interleaved, int frames);

		private readonly Reader _read;
		private readonly Func<int> _sampleRate;
		private readonly GainControlledAudioResourceSource _source;
		private readonly Thread _thread;
		private readonly short[] _samples;
		private readonly byte[] _bytes;
		private readonly int _intervalMs;
		private long _sequence;
		private int _rate;
		private volatile bool _running = true;

		/// <summary>The audio stream to hand out as <see cref="IGamelogicAudioResourceProvider.AudioSource"/>.</summary>
		public IPluginAudioSource Source => _source;

		/// <summary>Held while either side reads the emulator's queue.</summary>
		public object ReadLock { get; } = new object();

		/// <summary>Whether the engine plays its own sound: false while the central mixer took over.</summary>
		public bool LocalPlayback => !_source.PlaybackReplaced;

		/// <summary>The engine's volume and mute for the mixer's copy, 0..1.</summary>
		public float Gain { get => _source.PlaybackGain; set => _source.PlaybackGain = float.IsNaN(value) ? 0 : System.Math.Max(0, System.Math.Min(1, value)); }

		/// <param name="read">Reads interleaved stereo 16-bit frames from the emulator's queue.</param>
		/// <param name="sampleRate">The queue's current rate, or 0 while it isn't known yet. A changed rate ends the handover.</param>
		/// <param name="frameCapacity">Largest block read at once.</param>
		/// <param name="intervalMs">How often the queue is drained while the mixer plays it.</param>
		/// <param name="name">Thread name, for diagnostics.</param>
		public GamelogicAudioPump(Reader read, Func<int> sampleRate, int frameCapacity = 2048, int intervalMs = 4, string name = "VPE gamelogic audio")
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_sampleRate = sampleRate ?? throw new ArgumentNullException(nameof(sampleRate));
			_samples = new short[frameCapacity * 2];
			_bytes = new byte[frameCapacity * 4];
			_intervalMs = System.Math.Max(1, intervalMs);
			_source = new GainControlledAudioResourceSource(error => NLog.LogManager.GetCurrentClassLogger().Warn(error, "Gamelogic audio subscriber failed."));
			_thread = new Thread(Run) { IsBackground = true, Name = name, Priority = ThreadPriority.AboveNormal };
			_thread.Start();
		}

		private void Run()
		{
			while (_running) {
				try {
					if (_source.PlaybackReplaced && _source.HasSubscribers) Pump();
				} catch (Exception error) {
					NLog.LogManager.GetCurrentClassLogger().Warn(error, "Gamelogic audio handover failed.");
				}
				Thread.Sleep(_intervalMs);
			}
		}

		private void Pump()
		{
			int rate, frames;
			lock (ReadLock) {
				// both touch the engine's native handle, which its shutdown clears under this lock
				rate = _sampleRate();
				if (rate <= 0) return;
				frames = _read(_samples, _samples.Length / 2);
			}
			if (_rate == 0) _rate = rate;
			if (frames <= 0) return;
			Buffer.BlockCopy(_samples, 0, _bytes, 0, frames * 4);
			// A rate change is published as is; the mixer then refuses the format and the engine plays on its own again.
			_source.PublishForPlayback(new PluginPcmFrame(_bytes.AsMemory(0, frames * 4), PcmEncoding.Int16LittleEndian, rate, 2, frames, ++_sequence));
		}

		/// <summary>Stops the thread. The engine disposes it when it stops.</summary>
		public void Dispose()
		{
			_running = false;
			if (_thread.IsAlive && Thread.CurrentThread != _thread) _thread.Join(200);
			_source.Dispose();
		}
	}
}
