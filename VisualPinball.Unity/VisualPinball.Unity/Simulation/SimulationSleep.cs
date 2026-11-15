// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NLog;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Simulation
{
	/// <summary>
	/// Sleeps for less than a millisecond, where the system can: the simulation thread waits for its
	/// next tick here instead of spinning through it.
	/// </summary>
	///
	/// <remarks>
	/// <para>
	/// <c>Thread.Sleep</c> only takes whole milliseconds, and a tick is one. So the simulation thread
	/// used to yield and spin through all of its wait, which keeps a core busy: on a machine with four
	/// cores, that is a quarter of it, taken from the main and the render thread.
	/// </para>
	/// <para>
	/// On Linux, <c>nanosleep</c> wakes a thread within some tens of microseconds once its timer slack
	/// (by default 50 µs, the time the kernel may add to coalesce wake-ups) is set to the minimum. The
	/// thread then sleeps until shortly before the tick and spins only for the rest. Elsewhere there is
	/// nothing to gain over what the thread did before: <see cref="IsSupported"/> is false.
	/// </para>
	/// </remarks>
	internal static class SimulationSleep
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>How long before the target the sleep ends, for the spin to be on time even when the wake is late.</summary>
		public const long MarginUsec = 80;

		private static bool _supported = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && IntPtr.Size == 8;

		/// <summary>Whether <see cref="Sleep"/> sleeps here; when it doesn't, the caller waits as before.</summary>
		public static bool IsSupported => _supported;

		/// <summary>
		/// Prepares the calling thread: the smallest timer slack, so its sleeps end when they should. Call
		/// once on the thread that sleeps.
		/// </summary>
		public static void SetUpThread()
		{
			if (!_supported) {
				return;
			}
			try {
				if (prctl(PrSetTimerSlack, 1, 0, 0, 0) != 0) {
					Logger.Info("[SimulationSleep] Cannot set the timer slack, the sleeps may end up to 50 µs late.");
				}
			} catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) {
				Logger.Info(e, "[SimulationSleep] No libc to sleep with, waiting by spinning.");
				_supported = false;
			}
		}

		/// <summary>
		/// Sleeps until <paramref name="targetTicks"/> (<see cref="Stopwatch"/> ticks) minus the margin, or
		/// returns at once when that is in the past.
		/// </summary>
		public static void Sleep(long targetTicks)
		{
			var remainingTicks = targetTicks - Stopwatch.GetTimestamp() - MarginUsec * Stopwatch.Frequency / 1_000_000;
			if (remainingTicks <= 0) {
				return;
			}
			var ns = remainingTicks * (1_000_000_000 / Stopwatch.Frequency);
			var request = new Timespec { Seconds = ns / 1_000_000_000, Nanoseconds = ns % 1_000_000_000 };
			try {
				// an interrupted sleep (a signal, such as the GC's) ends early, which the spin after it covers
				nanosleep(ref request, IntPtr.Zero);
			} catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) {
				_supported = false;
			}
		}

		private const int PrSetTimerSlack = 29;

		[StructLayout(LayoutKind.Sequential)]
		private struct Timespec
		{
			public long Seconds;
			public long Nanoseconds;
		}

		[DllImport("libc", SetLastError = true)]
		private static extern int nanosleep(ref Timespec request, IntPtr remaining);

		[DllImport("libc", SetLastError = true)]
		private static extern int prctl(int option, ulong arg2, ulong arg3, ulong arg4, ulong arg5);
	}
}
