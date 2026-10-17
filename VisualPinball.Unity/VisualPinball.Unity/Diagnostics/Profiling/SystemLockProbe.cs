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

#if VPE_PROFILE
using System.Diagnostics;
using System.Threading;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
#endif

namespace VisualPinball.Unity.Profiling
{
	/// <summary>
	/// Notices when another process holds Windows' window-manager lock, which stalls our
	/// frames from outside.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// While a process creates a console window (a script or tool starting a console program
	/// without a console of its own, for example), win32k is locked for about 85 ms in four
	/// slices of about 21 ms. Everything that needs it waits: Unity's display queries in
	/// TimeUpdate, <c>Present</c>, input polling. The frame then looks like a GPU or vsync
	/// problem, or like time nothing explains.
	/// </para>
	/// <para>
	/// A background thread times <c>GetDisplayConfigBufferSizes</c>, which takes the lock and
	/// otherwise returns in about 0.05 ms, every 10 ms, and keeps the calls that took longer
	/// than 10 ms (no ordinary preemption of an above-normal thread lasts that long). For
	/// 100 ms after a slow call it probes every millisecond, so it follows the lock through its
	/// slices: slices less than 10 ms apart become one stall. It waits on a high-resolution
	/// timer, so this works without raising the system's timer resolution. Calls a garbage
	/// collection ran into are dropped: it suspends this thread as well. A ring of the last <see cref="Capacity"/> stalls
	/// is read by the incident detector and the trace. Windows only; elsewhere nothing runs and
	/// the ring stays empty.
	/// </para>
	/// </remarks>
	internal static class SystemLockProbe
	{
		public const int Capacity = 256;
		private const int IntervalMs = 10;
		private const double FollowMs = 100;
		private const double MergeMs = 10;
		private const double StallMs = 10;

		/// <summary>A probe call that waited for the lock, in stopwatch ticks.</summary>
		public struct Stall
		{
			public long StartTicks;
			public long EndTicks;
		}

		private static readonly Stall[] Stalls = new Stall[Capacity];
		// per slot: the index + 1 of the stall in it, 0 while it's written
		private static readonly long[] Sequence = new long[Capacity];
		private static long _count;

		/// <summary>A worker's own stop flag: a stopped worker is never revived.</summary>
		private sealed class Worker
		{
			public volatile bool Stopped;
			public Thread Thread;
		}

		private static Worker _worker;
		// writes happen under it, after checking the writer wasn't stopped: a worker that's
		// still leaving a slow call when the next one starts can't write into the ring
		private static readonly object WriteGate = new();

		/// <summary>How many stalls were recorded so far.</summary>
		public static long Count => Volatile.Read(ref _count);

		public static void Start()
		{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
			if (_worker != null && !_worker.Stopped) {
				return;
			}
			var worker = new Worker();
			worker.Thread = new Thread(() => Run(worker)) {
				Name = "VPE system lock probe",
				IsBackground = true,
				Priority = ThreadPriority.AboveNormal,
			};
			_worker = worker;
			worker.Thread.Start();
#endif
		}

		/// <summary>
		/// Stops the thread, before a domain reload or when play ends. Waits up to a second for
		/// it to leave; one still stuck in the call leaves when it returns, without writing.
		/// </summary>
		public static void Stop()
		{
			var worker = _worker;
			if (worker == null) {
				return;
			}
			lock (WriteGate) {
				worker.Stopped = true;
			}
			worker.Thread.Join(1000);
		}

		/// <summary>
		/// A recorded stall, if it's still in the ring and wasn't being overwritten. Any thread.
		/// </summary>
		public static bool TryRead(long index, out Stall stall)
		{
			var slot = (int)(index % Capacity);
			// a few tries: the newest stall is rewritten while the lock is still held
			for (var attempt = 0; attempt < 3; attempt++) {
				var before = Volatile.Read(ref Sequence[slot]);
				stall = Stalls[slot];
				Interlocked.MemoryBarrier();
				if (before == index + 1 && Volatile.Read(ref Sequence[slot]) == before) {
					return true;
				}
				if (before != 0 && before != index + 1) {
					break; // lapped, or not written yet
				}
			}
			stall = default;
			return false;
		}

		/// <summary>
		/// How long the probe waited for the lock between the two times, in stopwatch ticks.
		/// </summary>
		public static long BlockedTicks(long fromTicks, long toTicks)
		{
			var blocked = 0L;
			var count = Count;
			for (var i = count - 1; i >= 0 && i >= count - Capacity; i--) {
				if (!TryRead(i, out var s)) {
					continue;
				}
				if (s.EndTicks < fromTicks) {
					break; // the stalls are in time order
				}
				var overlap = System.Math.Min(s.EndTicks, toTicks) - System.Math.Max(s.StartTicks, fromTicks);
				if (overlap > 0) {
					blocked += overlap;
				}
			}
			return blocked;
		}

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
		private const uint QdcOnlyActivePaths = 2;

		private const uint CreateWaitableTimerHighResolution = 0x2;
		private const uint TimerAllAccess = 0x1F0003;

		[DllImport("user32.dll")]
		private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

		[DllImport("kernel32.dll")]
		private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, IntPtr name, uint flags, uint access);

		[DllImport("kernel32.dll")]
		private static extern bool SetWaitableTimer(IntPtr timer, ref long dueTime, int period, IntPtr completion, IntPtr argument, bool resume);

		[DllImport("kernel32.dll")]
		private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

		[DllImport("kernel32.dll")]
		private static extern bool CloseHandle(IntPtr handle);

		private static void Run(Worker worker)
		{
			var threshold = (long)(StallMs * Stopwatch.Frequency / 1000);
			var merge = (long)(MergeMs * Stopwatch.Frequency / 1000);
			var follow = (long)(FollowMs * Stopwatch.Frequency / 1000);
			var lastEnd = 0L; // 0: no stall yet
			// Windows 10 1803 and later; Sleep's resolution depends on what others asked for
			var timer = CreateWaitableTimerExW(IntPtr.Zero, IntPtr.Zero, CreateWaitableTimerHighResolution, TimerAllAccess);
			try {
				while (!worker.Stopped) {
					var gc = GC.CollectionCount(0);
					var start = Stopwatch.GetTimestamp();
					GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out _, out _);
					var end = Stopwatch.GetTimestamp();
					// a collection suspends this thread too: that wait isn't the window manager's
					if (end - start <= threshold || GC.CollectionCount(0) != gc) {
						// right after a stall, the next slice of the lock is a few ms away
						Wait(timer, lastEnd != 0 && end - lastEnd < follow ? 1 : IntervalMs);
						continue;
					}
					lock (WriteGate) {
						if (worker.Stopped) {
							break;
						}
						// the lock comes in slices with short gaps: one stall while it's held
						if (lastEnd != 0 && start - lastEnd <= merge) {
							Extend(end);
						} else {
							Record(start, end);
						}
					}
					lastEnd = end;
				}
			} finally {
				if (timer != IntPtr.Zero) {
					CloseHandle(timer);
				}
			}
		}

		private static void Wait(IntPtr timer, int milliseconds)
		{
			var due = -milliseconds * 10_000L; // relative, in 100 ns
			if (timer == IntPtr.Zero || !SetWaitableTimer(timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, false)) {
				Thread.Sleep(milliseconds);
				return;
			}
			WaitForSingleObject(timer, uint.MaxValue);
		}

		private static void Record(long start, long end)
		{
			var index = _count;
			var slot = (int)(index % Capacity);
			Interlocked.Exchange(ref Sequence[slot], 0);
			Stalls[slot].StartTicks = start;
			Stalls[slot].EndTicks = end;
			Volatile.Write(ref Sequence[slot], index + 1);
			Volatile.Write(ref _count, index + 1);
		}

		private static void Extend(long end)
		{
			var index = _count - 1;
			var slot = (int)(index % Capacity);
			Interlocked.Exchange(ref Sequence[slot], 0);
			Stalls[slot].EndTicks = end;
			Volatile.Write(ref Sequence[slot], index + 1);
		}
#endif
	}
}
#endif
