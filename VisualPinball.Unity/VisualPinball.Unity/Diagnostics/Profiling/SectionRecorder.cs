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
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;

namespace VisualPinball.Unity.Profiling
{
	/// <summary>
	/// What each <see cref="ProfileSection"/> costs on the main thread: per-frame totals, and
	/// the begin and end of every call that took 0.5 ms or more.
	/// </summary>
	///
	/// <remarks>
	/// Sections on other threads are ignored; those are covered by per-second aggregates.
	/// A section that begins again while it runs (recursion) counts once, from the outer
	/// begin. Each section also drives a <see cref="ProfilerMarker"/>, which only records in a
	/// development build or the editor.
	/// </remarks>
	internal static unsafe class SectionRecorder
	{
		public const int SlowCapacity = 1 << 16;
		private const double SlowMs = 0.5;

		public static readonly int Count = Enum.GetValues(typeof(ProfileSection)).Length;

		/// <summary>One call that took long enough to show as a slice in a trace.</summary>
		public struct SlowCall
		{
			public long StartTicks;
			public long EndTicks;
			public ProfileSection Section;
		}

		private static readonly long[] Started = new long[Count];
		private static readonly int[] Depth = new int[Count];
		private static readonly ProfilerMarker[] Markers = CreateMarkers();
		private static readonly long SlowTicks = (long)(SlowMs * Stopwatch.Frequency / 1000);

		/// <summary>Totals of the frame that runs now, in stopwatch ticks. Main thread only.</summary>
		public static readonly long[] FrameTicks = new long[Count];

		private static int _mainThreadId = -1;
		private static SlowCall* _slow;
		private static long _slowCount;

		/// <summary>Calls recorded so far; the ring holds the last <see cref="SlowCapacity"/>.</summary>
		public static long SlowCount => _slowCount;

		public static SlowCall* SlowCalls => _slow;

		public static void Install()
		{
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
			if (_slow == null) {
				_slow = (SlowCall*)UnsafeUtility.Malloc((long)SlowCapacity * sizeof(SlowCall), UnsafeUtility.AlignOf<SlowCall>(), Allocator.Persistent);
			}
			_slowCount = 0;
			Array.Clear(FrameTicks, 0, Count);
			Array.Clear(Depth, 0, Count);
		}

		/// <summary>
		/// Frees the ring, before a domain reload.
		/// </summary>
		public static void Free()
		{
			if (_slow != null) {
				UnsafeUtility.Free(_slow, Allocator.Persistent);
				_slow = null;
			}
			_slowCount = 0;
			_mainThreadId = -1;
		}

		public static void Begin(ProfileSection section)
		{
			if (Thread.CurrentThread.ManagedThreadId != _mainThreadId) {
				return;
			}
			var i = (int)section;
			if (Depth[i]++ == 0) {
				Started[i] = Stopwatch.GetTimestamp();
			}
			Markers[i].Begin();
		}

		public static void End(ProfileSection section)
		{
			if (Thread.CurrentThread.ManagedThreadId != _mainThreadId) {
				return;
			}
			var i = (int)section;
			Markers[i].End();
			if (Depth[i] == 0 || --Depth[i] > 0) {
				return;
			}
			var end = Stopwatch.GetTimestamp();
			var ticks = end - Started[i];
			FrameTicks[i] += ticks;
			if (ticks >= SlowTicks && _slow != null) {
				ref var slow = ref _slow[_slowCount % SlowCapacity];
				slow.StartTicks = Started[i];
				slow.EndTicks = end;
				slow.Section = section;
				_slowCount++;
			}
		}

		/// <summary>
		/// Adds time measured elsewhere, e.g. by the physics engine's own stopwatches.
		/// </summary>
		public static void Add(ProfileSection section, long ticks)
		{
			FrameTicks[(int)section] += ticks;
		}

		/// <summary>
		/// The frame's totals in µs into the given buffer, and starts the next frame at zero.
		/// </summary>
		public static void EndFrame(int* usec, int capacity)
		{
			var n = System.Math.Min(Count, capacity);
			for (var i = 0; i < n; i++) {
				var us = VpeClock.TicksToUsec(FrameTicks[i]);
				usec[i] = us > int.MaxValue ? int.MaxValue : (int)us;
				FrameTicks[i] = 0;
			}
		}

		public static string Name(ProfileSection section) => section switch {
			ProfileSection.PhysicsEventDrain => "Physics.EventDrain",
			ProfileSection.PhysicsKinematicScan => "Physics.KinematicScan",
			ProfileSection.PhysicsApplyMovements => "Physics.ApplyMovements",
			ProfileSection.SimClockSync => "Sim.ClockSync",
			ProfileSection.SimInputFlush => "Sim.InputFlush",
			ProfileSection.SimApplyState => "Sim.ApplyState",
			ProfileSection.GleUpdate => "Gle.Update",
			ProfileSection.GleLamps => "Gle.Lamps",
			ProfileSection.DisplayUpload => "Display.Upload",
			ProfileSection.LightsUpdate => "Lights.Update",
			ProfileSection.WireUpdate => "Wire.Update",
			ProfileSection.PlayerOnUpdate => "Player.OnUpdate",
			ProfileSection.SoundStart => "Sound.Start",
			ProfileSection.InputProcess => "Input.Process",
			ProfileSection.HdrpAutoFocus => "Hdrp.AutoFocus",
			ProfileSection.PluginsUpdate => "Plugins.Update",
			ProfileSection.UiOverlay => "Ui.Overlay",
			ProfileSection.PerfSelf => "Perf.Self",
			_ => section.ToString(),
		};

		private static ProfilerMarker[] CreateMarkers()
		{
			var markers = new ProfilerMarker[Count];
			for (var i = 0; i < Count; i++) {
				markers[i] = new ProfilerMarker(ProfilerCategory.Scripts, "VPE." + Name((ProfileSection)i));
			}
			return markers;
		}
	}
}
#endif
