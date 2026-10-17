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
using UnityEngine;

namespace VisualPinball.Unity.Profiling
{
	/// <summary>
	/// Statistics of ten seconds of play, for metrics (plan §6.1).
	/// </summary>
	public struct MetricsWindow
	{
		public double Seconds;
		public int Frames;
		public double Fps;
		public double FrameP50Ms, FrameP99Ms, FrameMaxMs;
		public double PresentStddevMs;
		public double TickBusyP99Us;
		public double CpuMainMs, CpuRenderMs, GpuMs;
		public double SimRatio;
		public double ProcessCpuShare;
		public double RamMb, VramMb;
		public int Stutters, Hitches, PacingIncidents, SimStalls;
		public int GcCollections;
		public int DroppedBacklogMs;
	}

	/// <summary>
	/// How a table played, from start to exit, for the <c>table.play</c> transaction.
	/// </summary>
	public struct PlayStats
	{
		public double PlayedSeconds;
		public double AverageFps;
		public double FrameP99Ms;
		public double Worst1PercentMs;
		public double StuttersPerMinute;
		public int Hitches;
		public double MinSimRatio;
		public long DroppedBacklogMs;
		public int GcStalls;
		public long PeakVramMb;
	}

	/// <summary>
	/// Collects the finalized frames' statistics into ten-second windows of play, and the whole
	/// play into <see cref="PlayStats"/>. Main thread, allocation-free.
	/// </summary>
	public static class ProfileMetrics
	{
		private const double WindowSeconds = 10;
		private const int Buckets = 2000; // 0.25 ms up to 500 ms
		private const int TickBuckets = 1000; // 10 µs up to 10 ms

		/// <summary>Ten seconds of play are over. Main thread.</summary>
		public static event Action<MetricsWindow> WindowClosed;

		/// <summary>The table was exited. Main thread.</summary>
		public static event Action<PlayStats> PlayEnded;

		private static readonly int[] Window = new int[Buckets];
		private static readonly int[] Play = new int[Buckets];
		private static readonly int[] TickWindow = new int[TickBuckets];
		private const int MaxTimed = 4096;
		private static readonly float[] CpuMain = new float[MaxTimed];
		private static readonly float[] CpuRender = new float[MaxTimed];
		private static readonly float[] Gpu = new float[MaxTimed];
		private static int _timed;
		private static int _windowFrames;
		private static long _playFrames;
		private static double _windowSeconds;
		private static double _playSeconds;
		private static float _windowMax;
		private static double _presentSum, _presentSquares;
		private static int _presents;
		private static int _ticks;
		private static int _stutters, _hitches, _pacing, _stalls, _gcStalls;
		private static int _playStutters, _playHitches;
		private static int _gcStart = -1;
		private static int _droppedUsec;
		private static long _playDroppedUsec;
		private static long _cpuStart = -1;
		private static double _cpuWallStart;

		internal static void Reset()
		{
			ClearWindow();
			Array.Clear(Play, 0, Play.Length);
			_playFrames = 0;
			_playSeconds = 0;
			_playStutters = _playHitches = _gcStalls = 0;
			_playDroppedUsec = 0;
		}

		private static void ClearWindow()
		{
			Array.Clear(Window, 0, Window.Length);
			Array.Clear(TickWindow, 0, TickWindow.Length);
			_windowFrames = 0;
			_windowSeconds = 0;
			_windowMax = 0;
			_timed = 0;
			_presentSum = _presentSquares = 0;
			_presents = 0;
			_ticks = 0;
			_stutters = _hitches = _pacing = _stalls = 0;
			_gcStart = -1;
			_droppedUsec = 0;
			_cpuStart = CpuTime.ProcessUsec();
			_cpuWallStart = Time.realtimeSinceStartupAsDouble;
		}

		/// <summary>A played (not paused) frame's interval.</summary>
		internal static void OnFrame(float frameMs, float presentMs, int gcCount, ref FrameSample f)
		{
			if (f.Timing == TimingState.Matched && _timed < MaxTimed) {
				CpuMain[_timed] = f.CpuMainMs;
				CpuRender[_timed] = f.CpuRenderMs;
				Gpu[_timed] = f.GpuMs;
				_timed++;
			}
			var bucket = (int)(frameMs * 4);
			bucket = bucket < Buckets ? bucket : Buckets - 1;
			Window[bucket]++;
			Play[bucket]++;
			_windowFrames++;
			_playFrames++;
			_windowSeconds += frameMs / 1000.0;
			_playSeconds += frameMs / 1000.0;
			if (frameMs > _windowMax) {
				_windowMax = frameMs;
			}
			if (presentMs > 0) {
				_presentSum += presentMs;
				_presentSquares += presentMs * presentMs;
				_presents++;
			}
			if (_gcStart < 0) {
				_gcStart = gcCount;
			}
			if (_windowSeconds >= WindowSeconds) {
				Close(gcCount);
			}
		}

		internal static void OnTick(int totalUsec, int droppedUsec)
		{
			var bucket = totalUsec / 10;
			TickWindow[bucket < TickBuckets ? bucket : TickBuckets - 1]++;
			_ticks++;
			_droppedUsec += droppedUsec;
			_playDroppedUsec += droppedUsec;
		}

		internal static void OnIncident(in Incident incident)
		{
			switch (incident.Type) {
				case IncidentType.Stutter:
					_stutters++;
					_playStutters++;
					break;
				case IncidentType.Hitch:
					_hitches++;
					_playHitches++;
					break;
				case IncidentType.Pacing:
					_pacing++;
					break;
				case IncidentType.SimStall:
					_stalls++;
					break;
			}
			if (incident.Cause == IncidentCause.GcPause) {
				_gcStalls++;
			}
		}

		private static void Close(int gcCount)
		{
			var last = PerformanceMonitor.Last;
			var timed = _timed;
			var wall = Time.realtimeSinceStartupAsDouble - _cpuWallStart;
			var cpu = CpuTime.ProcessUsec();
			var window = new MetricsWindow {
				Seconds = _windowSeconds,
				Frames = _windowFrames,
				Fps = _windowSeconds > 0 ? _windowFrames / _windowSeconds : 0,
				FrameP50Ms = Percentile(Window, _windowFrames, 0.5) / 4.0,
				FrameP99Ms = Percentile(Window, _windowFrames, 0.99) / 4.0,
				FrameMaxMs = _windowMax,
				PresentStddevMs = _presents > 1 ? System.Math.Sqrt(System.Math.Max(0, _presentSquares / _presents - (_presentSum / _presents) * (_presentSum / _presents))) : 0,
				TickBusyP99Us = _ticks > 0 ? Percentile(TickWindow, _ticks, 0.99) * 10.0 : 0,
				// over the same ten seconds as the frame times
				CpuMainMs = timed > 0 ? Median(CpuMain, timed) : -1,
				CpuRenderMs = timed > 0 ? Median(CpuRender, timed) : -1,
				GpuMs = timed > 0 ? Median(Gpu, timed) : -1,
				SimRatio = last.SimRatio,
				ProcessCpuShare = cpu >= 0 && _cpuStart >= 0 && wall > 0 ? (cpu - _cpuStart) / 1e6 / wall / SystemInfo.processorCount : -1,
				RamMb = last.RamBytes / 1048576.0,
				VramMb = last.VramUsage / 1048576.0,
				Stutters = _stutters,
				Hitches = _hitches,
				PacingIncidents = _pacing,
				SimStalls = _stalls,
				GcCollections = _gcStart >= 0 ? gcCount - _gcStart : 0,
				DroppedBacklogMs = _droppedUsec / 1000,
			};
			ClearWindow();
			WindowClosed?.Invoke(window);
		}

		/// <summary>
		/// The table was exited. Called by <see cref="PerformanceMonitor"/>, which adds its own
		/// session statistics.
		/// </summary>
		internal static void EndPlay(double averageFps, double minSimRatio, long peakVramBytes)
		{
			if (_playFrames == 0) {
				return;
			}
			var stats = new PlayStats {
				PlayedSeconds = _playSeconds,
				AverageFps = averageFps,
				FrameP99Ms = Percentile(Play, _playFrames, 0.99) / 4.0,
				Worst1PercentMs = SlowestShareMeanMs(Play, _playFrames, 0.01),
				StuttersPerMinute = _playSeconds > 0 ? _playStutters / (_playSeconds / 60) : 0,
				Hitches = _playHitches,
				MinSimRatio = minSimRatio,
				DroppedBacklogMs = _playDroppedUsec / 1000,
				GcStalls = _gcStalls,
				PeakVramMb = peakVramBytes > 0 ? peakVramBytes / 1048576 : -1,
			};
			Reset();
			PlayEnded?.Invoke(stats);
		}

		private static double Median(float[] values, int count)
		{
			Array.Sort(values, 0, count);
			return (count & 1) == 1 ? values[count / 2] : (values[count / 2 - 1] + values[count / 2]) * 0.5;
		}

		/// <summary>The mean frame time of the slowest given share of the frames, from the histogram.</summary>
		private static double SlowestShareMeanMs(int[] histogram, long total, double share)
		{
			var wanted = System.Math.Max(1, (long)System.Math.Ceiling(total * share));
			long taken = 0;
			var sum = 0.0;
			for (var i = histogram.Length - 1; i >= 0 && taken < wanted; i--) {
				var n = System.Math.Min(histogram[i], wanted - taken);
				sum += n * (i + 0.5) / 4.0;
				taken += n;
			}
			return taken > 0 ? sum / taken : 0;
		}

		/// <summary>The bucket below which the given share of the counts lie.</summary>
		private static int Percentile(int[] histogram, long total, double share)
		{
			var target = (long)System.Math.Ceiling(total * share);
			long seen = 0;
			for (var i = 0; i < histogram.Length; i++) {
				seen += histogram[i];
				if (seen >= target) {
					return i;
				}
			}
			return histogram.Length - 1;
		}
	}
}
#endif
