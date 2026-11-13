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
using System.Collections.Generic;

namespace VisualPinball.Unity.Profiling
{
	/// <summary>
	/// How a stretch of frames ran: the statistics a benchmark step is judged and reported by.
	/// Times are in milliseconds; a value that couldn't be measured is -1.
	/// </summary>
	public sealed class FrameWindowStats
	{
		/// <summary>Frames counted, and the time they took together.</summary>
		public int Frames;
		public double Seconds;

		public double AverageFps;
		/// <summary>Frame intervals, from one frame's start to the next one's.</summary>
		public double FrameAvgMs, FrameP50Ms, FrameP95Ms, FrameP99Ms, FrameMaxMs, FrameStdDevMs;
		/// <summary>The frame rate of the slowest 1 % of the frames.</summary>
		public double Low1PercentFps;

		/// <summary>Frames Unity timed (it doesn't on every platform, and not every frame).</summary>
		public int TimedFrames;
		/// <summary>Medians of what Unity timed: the main thread without its wait for the present, the render thread, the GPU.</summary>
		public double CpuMainMs = -1, CpuRenderMs = -1, GpuMs = -1, PresentWaitMs = -1;
		public double GpuP95Ms = -1;
		/// <summary>How evenly the frames were handed to the display: the standard deviation of the interval between presents.</summary>
		public double PresentStdDevMs = -1;
		/// <summary>From a frame's start until the GPU finished it.</summary>
		public double RenderLatencyAvgMs = -1, RenderLatencyP95Ms = -1;
		/// <summary>How old the simulation's state was when a frame started that showed it.</summary>
		public double SnapshotAgeAvgMs = -1;

		public int GcCollections;
		public double GcAllocatedPerFrameKb = -1;
		public int MaxBalls;

		/// <summary>What each part of VPE cost per frame on the main thread, on average, by name. Only the parts that ran.</summary>
		public readonly List<KeyValuePair<string, double>> SectionMs = new();
		/// <summary>How long the main thread spent in each top-level PlayerLoop phase per frame, on average.</summary>
		public readonly List<KeyValuePair<string, double>> PhaseMs = new();
	}

	/// <summary>
	/// Statistics over the frames the <see cref="FrameRecorder"/> recorded between two moments. For a
	/// benchmark, which measures one stretch per setting. Needs the instrumentation on
	/// (<see cref="VpeProfiler.Start"/>). Main thread.
	/// </summary>
	public static unsafe class FrameWindow
	{
		/// <summary>
		/// Marks now: the frames after it belong to the next window. -1 while nothing records.
		/// </summary>
		public static long Mark() => VpeProfiler.IsEnabled && FrameRecorder.Frames != null ? FrameRecorder.Count : -1;

		/// <summary>
		/// How many frames were recorded since a mark.
		/// </summary>
		public static int FramesSince(long mark) => mark < 0 || !VpeProfiler.IsEnabled ? 0 : (int)System.Math.Max(0, FrameRecorder.Count - mark);

		/// <summary>
		/// The statistics of the frames between two marks. Null when there are too few of them, or
		/// when they're no longer in the recorder's ring. Paused frames and the instrumentation's
		/// own are left out. Unity's timings arrive a few frames late: collect a few frames after
		/// the end mark for them to cover the whole window.
		/// </summary>
		public static FrameWindowStats Collect(long fromMark, long toMark)
		{
			var stats = Collect(FrameRecorder.Frames, FrameRecorder.Capacity, FrameRecorder.Count, fromMark, toMark, FrameRecorder.CountsAllocations, out var sections, out var phases);
			if (stats == null) {
				return null;
			}
			var sectionCount = System.Math.Min(SectionRecorder.Count, sections.Length);
			for (var s = 0; s < sectionCount; s++) {
				if (sections[s] > 0) {
					stats.SectionMs.Add(new KeyValuePair<string, double>(SectionRecorder.Name((ProfileSection)s), sections[s]));
				}
			}
			var names = FrameRecorder.PhaseNames;
			for (var p = 0; p < phases.Length && p < names.Length - 1; p++) {
				stats.PhaseMs.Add(new KeyValuePair<string, double>(names[p], phases[p]));
			}
			return stats;
		}

		/// <summary>
		/// The statistics of the frames between two marks in a ring of frames, without the names of the
		/// sections and phases: their times per frame come back by index.
		/// </summary>
		/// <param name="frames">The ring</param>
		/// <param name="capacity">How many frames the ring holds</param>
		/// <param name="count">How many frames were written to it so far</param>
		/// <param name="countsAllocations">Whether the frames have their allocated bytes</param>
		internal static FrameWindowStats Collect(FrameSample* frames, int capacity, long count, long fromMark, long toMark, bool countsAllocations, out double[] sectionMs, out double[] phaseMs)
		{
			sectionMs = phaseMs = null;
			if (frames == null || fromMark < 0 || toMark < 0) {
				return null;
			}
			// an interval needs the next frame's start
			toMark = System.Math.Min(toMark, count - 1);
			if (toMark - fromMark < 2 || fromMark < count - capacity) {
				return null;
			}

			var n = (int)(toMark - fromMark);
			var intervals = new double[n];
			var cpuMain = new double[n];
			var cpuRender = new double[n];
			var gpu = new double[n];
			var wait = new double[n];
			var latency = new double[n];
			var sections = new double[FrameSample.MaxSections];
			var phases = new double[FrameSample.Phases - 1];
			var used = 0;
			var timed = 0;
			var latencies = 0;
			double presentSum = 0, presentSquares = 0;
			var presents = 0;
			long previousPresent = 0;
			double ageSum = 0;
			var ages = 0;
			double allocated = 0;
			var allocations = 0;
			var maxBalls = 0;
			var stats = new FrameWindowStats();
			var firstGc = -1;
			var lastGc = -1;

			for (var i = fromMark; i < toMark; i++) {
				ref var f = ref frames[i % capacity];
				ref var next = ref frames[(i + 1) % capacity];
				// the interval runs to the next frame's start: neither end may be paused or the instrumentation's own
				if (f.Paused != 0 || next.Paused != 0 || f.SelfInduced != 0 || next.SelfInduced != 0) {
					previousPresent = 0;
					continue;
				}
				intervals[used++] = Ticks.ToMs(next.StartTicks - f.StartTicks);
				if (firstGc < 0) {
					firstGc = f.GcCount;
				}
				lastGc = f.GcCount;
				// a frame that allocated nothing counts too; the newest two don't have their count yet
				if (countsAllocations && i < count - 2) {
					allocated += f.GcAllocatedBytes;
					allocations++;
				}
				maxBalls = System.Math.Max(maxBalls, f.Balls);
				if (f.SnapshotPublishUsec > 0 && f.SnapshotAgeUsec >= 0) {
					ageSum += f.SnapshotAgeUsec / 1000.0;
					ages++;
				}
				for (var s = 0; s < FrameSample.MaxSections; s++) {
					sections[s] += f.SectionUsec[s] / 1000.0;
				}
				for (var p = 0; p < FrameSample.Phases - 1; p++) {
					phases[p] += Ticks.ToMs(f.Phase[p + 1] - f.Phase[p]);
				}
				if (f.Timing != TimingState.Matched) {
					previousPresent = 0;
					continue;
				}
				cpuMain[timed] = f.CpuMainMs;
				cpuRender[timed] = f.CpuRenderMs;
				gpu[timed] = f.GpuMs;
				wait[timed] = f.PresentWaitMs;
				timed++;
				// where Unity doesn't report when the GPU was done, there's no latency to tell
				if (f.FrameCompleteTicks > f.StartTicks) {
					latency[latencies++] = Ticks.ToMs(f.FrameCompleteTicks - f.StartTicks);
				}
				if (f.PresentCalledTicks > 0) {
					if (previousPresent > 0) {
						var interval = Ticks.ToMs(f.PresentCalledTicks - previousPresent);
						presentSum += interval;
						presentSquares += interval * interval;
						presents++;
					}
					previousPresent = f.PresentCalledTicks;
				}
			}
			if (used < 2) {
				return null;
			}

			double sum = 0, squares = 0;
			for (var i = 0; i < used; i++) {
				sum += intervals[i];
				squares += intervals[i] * intervals[i];
			}
			Array.Sort(intervals, 0, used);
			stats.Frames = used;
			stats.Seconds = sum / 1000.0;
			stats.AverageFps = sum > 0 ? used * 1000.0 / sum : 0;
			stats.FrameAvgMs = sum / used;
			stats.FrameP50Ms = Percentile(intervals, used, 0.5);
			stats.FrameP95Ms = Percentile(intervals, used, 0.95);
			stats.FrameP99Ms = Percentile(intervals, used, 0.99);
			stats.FrameMaxMs = intervals[used - 1];
			stats.FrameStdDevMs = System.Math.Sqrt(System.Math.Max(0, squares / used - stats.FrameAvgMs * stats.FrameAvgMs));
			var slowest = System.Math.Max(1, used / 100);
			double slowSum = 0;
			for (var i = used - slowest; i < used; i++) {
				slowSum += intervals[i];
			}
			stats.Low1PercentFps = slowSum > 0 ? slowest * 1000.0 / slowSum : 0;

			stats.TimedFrames = timed;
			if (timed > 0) {
				Array.Sort(gpu, 0, timed);
				stats.CpuMainMs = Median(cpuMain, timed);
				stats.CpuRenderMs = Median(cpuRender, timed);
				// some drivers don't time the GPU: all zeros
				stats.GpuMs = gpu[timed - 1] > 0 ? Percentile(gpu, timed, 0.5) : -1;
				stats.GpuP95Ms = gpu[timed - 1] > 0 ? Percentile(gpu, timed, 0.95) : -1;
				stats.PresentWaitMs = Median(wait, timed);
			}
			if (latencies > 0) {
				double latencySum = 0;
				for (var i = 0; i < latencies; i++) {
					latencySum += latency[i];
				}
				Array.Sort(latency, 0, latencies);
				stats.RenderLatencyAvgMs = latencySum / latencies;
				stats.RenderLatencyP95Ms = Percentile(latency, latencies, 0.95);
			}
			if (presents > 1) {
				var mean = presentSum / presents;
				stats.PresentStdDevMs = System.Math.Sqrt(System.Math.Max(0, presentSquares / presents - mean * mean));
			}
			if (ages > 0) {
				stats.SnapshotAgeAvgMs = ageSum / ages;
			}
			stats.GcCollections = firstGc >= 0 ? lastGc - firstGc : 0;
			if (allocations > 0) {
				stats.GcAllocatedPerFrameKb = allocated / allocations / 1024.0;
			}
			stats.MaxBalls = maxBalls;

			for (var s = 0; s < sections.Length; s++) {
				sections[s] /= used;
			}
			for (var p = 0; p < phases.Length; p++) {
				phases[p] /= used;
			}
			sectionMs = sections;
			phaseMs = phases;
			return stats;
		}

		private static double Median(double[] values, int count)
		{
			Array.Sort(values, 0, count);
			return Percentile(values, count, 0.5);
		}

		/// <summary>The value below which the given share of the sorted values lie (nearest rank).</summary>
		internal static double Percentile(double[] sorted, int count, double share)
		{
			if (count == 0) {
				return 0;
			}
			var rank = (int)System.Math.Ceiling(share * count) - 1;
			return sorted[System.Math.Min(count - 1, System.Math.Max(0, rank))];
		}
	}
}
