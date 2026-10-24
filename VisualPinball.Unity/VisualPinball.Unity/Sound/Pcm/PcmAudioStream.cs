// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A live PCM input of the central mixer, for audio a plugin produces itself, such as a video's soundtrack. It plays
	/// on one sound bus through the mixer's output (shared, exclusive or ASIO), behind a small jitter buffer.
	/// </summary>
	/// <remarks>
	/// Open it with <see cref="PcmAudioRenderer.OpenStream"/>. <see cref="Push(ReadOnlySpan{float}, int, int, int)"/>
	/// has a single producer and may run on any thread; the rest is main thread only. When the mixer goes away (the table
	/// unloads, or the player switches to Standard playback) pushes return false and <see cref="IsOpen"/> turns false,
	/// so the producer can fall back to its own playback.
	/// </remarks>
	public sealed class PcmAudioStream : IDisposable
	{
		private readonly NativePcmSession _session;
		private readonly int _id;
		private long _sequence;
		private int _closed, _calls;

		internal PcmAudioStream(NativePcmSession session, int id) { _session = session; _id = id; }

		/// <summary>Whether the mixer still plays this stream.</summary>
		public bool IsOpen => Volatile.Read(ref _closed) == 0 && _session.Context != IntPtr.Zero;

		/// <summary>Pushes interleaved float samples (mono or stereo). False when the mixer is gone or the block is refused.</summary>
		/// <remarks>The sample rate must stay the same for the stream's lifetime.</remarks>
		public unsafe bool Push(ReadOnlySpan<float> samples, int frames, int channels, int sampleRate)
		{
			if (frames <= 0 || samples.Length < frames * channels) return false;
			fixed (float* data = samples) return Push((IntPtr)data, frames, channels, sampleRate, 1);
		}

		/// <summary>Pushes interleaved 16-bit samples (mono or stereo). False when the mixer is gone or the block is refused.</summary>
		public unsafe bool Push(ReadOnlySpan<short> samples, int frames, int channels, int sampleRate)
		{
			if (frames <= 0 || samples.Length < frames * channels) return false;
			fixed (short* data = samples) return Push((IntPtr)data, frames, channels, sampleRate, 0);
		}

		/// <summary>Pushes interleaved 16-bit samples from native memory, for decoders that hand out pointers.</summary>
		public bool PushInt16(IntPtr samples, int frames, int channels, int sampleRate)
		{
			if (samples == IntPtr.Zero || frames <= 0) return false;
			return Push(samples, frames, channels, sampleRate, 0);
		}

		private bool Push(IntPtr samples, int frames, int channels, int sampleRate, int encoding)
		{
			if (!Enter()) return false;
			try { return _session.PushStream(_id, samples, frames, channels, sampleRate, encoding, ++_sequence); }
			finally { Exit(); }
		}

		/// <summary>The stream's own gain, 0..1, on top of its bus level.</summary>
		public float Gain { set { if (!Enter()) return; try { _session.SetStreamGain(_id, Mathf01(value)); } finally { Exit(); } } }

		/// <summary>Drops what is queued, for example when the producer seeks or pauses. Any thread.</summary>
		public void Flush() { if (!Enter()) return; try { _session.FlushStream(_id); } finally { Exit(); } }

		// A call counts itself in before it checks that the stream is open, so Dispose either sees it or it sees the close.
		private bool Enter()
		{
			Interlocked.Increment(ref _calls);
			if (Volatile.Read(ref _closed) == 0) return true;
			Interlocked.Decrement(ref _calls);
			return false;
		}
		private void Exit() => Interlocked.Decrement(ref _calls);

		/// <summary>Frames queued before playing after start, flush or underflow; -1 (the default) sizes it from the pushed blocks.</summary>
		public int BufferFrames { set { if (!Enter()) return; try { _session.SetStreamBuffer(_id, value); } finally { Exit(); } } }

		/// <summary>
		/// Closes the stream. Main thread. A producer still calling from its own thread is refused from now on, and the
		/// slot is only released once a call already under way returned, so it can't reach a stream opened later.
		/// </summary>
		public void Dispose()
		{
			if (Interlocked.Exchange(ref _closed, 1) != 0) return;
			SpinWait.SpinUntil(() => Volatile.Read(ref _calls) == 0);
			_session.CloseStream(_id);
		}

		internal void Detach() => Volatile.Write(ref _closed, 1);

		private static float Mathf01(float value) => float.IsNaN(value) ? 0 : value < 0 ? 0 : value > 1 ? 1 : value;
	}
}
