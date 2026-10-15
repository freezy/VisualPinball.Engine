// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Limits how often a log line in a hot path is written: the first few occurrences pass,
	/// then at most one per interval, reporting how many were suppressed in between.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// <see cref="ShouldLog"/> doesn't allocate or lock, so it's safe on the simulation
	/// thread and in native callbacks. Check it <i>before</i> calling the logger, so a
	/// suppressed line costs nothing:
	/// </para>
	/// <code>
	/// if (UnmappedCoilThrottle.ShouldLog(out var suppressed)) {
	///     Logger.Warn("Unmapped coil {CoilId} ({Suppressed} suppressed)", id, suppressed);
	/// }
	/// </code>
	/// <para>
	/// Throttles are reset at the start of each session.
	/// </para>
	/// </remarks>
	public sealed class LogThrottle
	{
		private static readonly List<WeakReference<LogThrottle>> All = new();

		private readonly int _first;
		private readonly long _intervalTicks;
		private readonly object _lock = new();
		private int _passed;
		private long _lastTicks;
		private int _suppressed;

		/// <param name="first">How many occurrences pass before throttling starts.</param>
		/// <param name="every">Minimal time between two lines after that.</param>
		public LogThrottle(int first, TimeSpan every)
		{
			_first = System.Math.Max(0, first);
			_intervalTicks = (long)(every.TotalSeconds * Stopwatch.Frequency);
			lock (All) {
				All.Add(new WeakReference<LogThrottle>(this));
			}
		}

		/// <summary>
		/// Returns whether this occurrence should be logged.
		/// </summary>
		/// <param name="suppressed">How many occurrences were suppressed since the last one
		/// that was logged.</param>
		public bool ShouldLog(out int suppressed)
		{
			var now = Stopwatch.GetTimestamp();

			// Fast path, no lock: the burst is used up and the interval hasn't elapsed. The
			// count and the timestamp are only ever changed together, under the lock. A zero
			// timestamp means nothing was logged yet (timestamps can be small right after the
			// process started, so "long ago" can't be told from the value alone).
			var last = Interlocked.Read(ref _lastTicks);
			if (Volatile.Read(ref _passed) >= _first && last != 0 && now - last < _intervalTicks) {
				Interlocked.Increment(ref _suppressed);
				suppressed = 0;
				return false;
			}

			lock (_lock) {
				last = Interlocked.Read(ref _lastTicks);
				if (_passed < _first || last == 0 || now - last >= _intervalTicks) {
					if (_passed < _first) {
						Volatile.Write(ref _passed, _passed + 1);
					}
					// never move the timestamp back if another thread got here with a later one
					if (now > last) {
						Interlocked.Exchange(ref _lastTicks, now);
					}
					suppressed = Interlocked.Exchange(ref _suppressed, 0);
					return true;
				}
			}
			Interlocked.Increment(ref _suppressed);
			suppressed = 0;
			return false;
		}

		/// <summary>
		/// Returns whether this occurrence should be logged.
		/// </summary>
		public bool ShouldLog() => ShouldLog(out _);

		/// <summary>
		/// Starts over as if nothing had been logged.
		/// </summary>
		public void Reset()
		{
			lock (_lock) {
				Volatile.Write(ref _passed, 0);
				Interlocked.Exchange(ref _lastTicks, 0);
				Interlocked.Exchange(ref _suppressed, 0);
			}
		}

		internal static void ResetAll()
		{
			lock (All) {
				for (var i = All.Count - 1; i >= 0; i--) {
					if (All[i].TryGetTarget(out var throttle)) {
						throttle.Reset();
					} else {
						All.RemoveAt(i);
					}
				}
			}
		}
	}

	/// <summary>
	/// Lets a log line through once per distinct key, e.g. once per unmapped coil, however
	/// often that coil changes. Past <see cref="MaxKeys"/> distinct keys it falls back to a
	/// <see cref="LogThrottle"/>, so a broken mapping can't grow memory without bound.
	/// </summary>
	public sealed class LogOncePerKey<TKey>
	{
		public const int MaxKeys = 256;

		private readonly ConcurrentDictionary<TKey, byte> _seen = new();
		private readonly LogThrottle _overflow = new LogThrottle(0, TimeSpan.FromSeconds(30));
		private int _version;

		/// <summary>
		/// Returns true the first time a key is seen in this session.
		/// </summary>
		public bool ShouldLog(TKey key)
		{
			var version = LogOncePerKeyVersion.Current;
			if (Volatile.Read(ref _version) != version) {
				lock (_seen) {
					if (_version != version) {
						_seen.Clear();
						Volatile.Write(ref _version, version);
					}
				}
			}
			if (_seen.ContainsKey(key)) {
				return false;
			}
			if (_seen.Count >= MaxKeys) {
				return _overflow.ShouldLog();
			}
			return _seen.TryAdd(key, 0);
		}
	}

	internal static class LogOncePerKeyVersion
	{
		private static int _current;

		public static int Current => Volatile.Read(ref _current);

		public static void Next() => Interlocked.Increment(ref _current);
	}
}
