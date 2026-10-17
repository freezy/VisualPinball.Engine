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
using System;
using System.Runtime.InteropServices;

namespace VisualPinball.Unity.Profiling
{
	/// <summary>
	/// CPU time of the current thread and of the process, in µs, without allocating
	/// (<c>Process.GetCurrentProcess()</c> allocates). -1 where the platform won't tell.
	/// </summary>
	internal static class CpuTime
	{
		private const int ClockProcessCpuTime = 2;
		private const int ClockThreadCpuTime = 3;

		private static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
		private static bool _failed;

		/// <summary>
		/// CPU time the calling thread used so far.
		/// </summary>
		public static long ThreadUsec()
		{
			if (_failed) {
				return -1;
			}
			try {
				if (IsWindows) {
					return GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user) ? (kernel + user) / 10 : -1;
				}
				return clock_gettime(ClockThreadCpuTime, out var ts) == 0 ? ts.Seconds * 1_000_000 + ts.Nanoseconds / 1000 : -1;
			} catch (Exception) {
				// no such entry point on this platform: don't try again
				_failed = true;
				return -1;
			}
		}

		/// <summary>
		/// CPU time all threads of the process used so far.
		/// </summary>
		public static long ProcessUsec()
		{
			if (_failed) {
				return -1;
			}
			try {
				if (IsWindows) {
					return GetProcessTimes(GetCurrentProcess(), out _, out _, out var kernel, out var user) ? (kernel + user) / 10 : -1;
				}
				return clock_gettime(ClockProcessCpuTime, out var ts) == 0 ? ts.Seconds * 1_000_000 + ts.Nanoseconds / 1000 : -1;
			} catch (Exception) {
				_failed = true;
				return -1;
			}
		}

		private const int ClockMonotonic = 1;

		/// <summary>
		/// The platform's raw monotonic clock and its frequency: QPC on Windows, CLOCK_MONOTONIC
		/// in ns elsewhere. Unity's frame timings use it; Mono's Stopwatch counts from the
		/// process' start instead. False if it can't be read.
		/// </summary>
		public static bool TryReadMonotonic(out long ticks, out long frequency)
		{
			ticks = frequency = 0;
			try {
				if (IsWindows) {
					return QueryPerformanceCounter(out ticks) && QueryPerformanceFrequency(out frequency);
				}
				if (clock_gettime(ClockMonotonic, out var ts) != 0) {
					return false;
				}
				ticks = ts.Seconds * 1_000_000_000 + ts.Nanoseconds;
				frequency = 1_000_000_000;
				return true;
			} catch (Exception) {
				return false;
			}
		}

		[DllImport("kernel32.dll")]
		private static extern bool QueryPerformanceCounter(out long count);

		[DllImport("kernel32.dll")]
		private static extern bool QueryPerformanceFrequency(out long frequency);

		[StructLayout(LayoutKind.Sequential)]
		private struct TimeSpec
		{
			public long Seconds;
			public long Nanoseconds;
		}

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentThread();

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentProcess();

		// FILETIME as 100 ns units
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

		[DllImport("libc", SetLastError = true)]
		private static extern int clock_gettime(int clock, out TimeSpec time);
	}
}
#endif
