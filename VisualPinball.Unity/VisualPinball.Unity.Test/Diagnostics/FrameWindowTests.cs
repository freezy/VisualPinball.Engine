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

using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Unity.Profiling;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The statistics a benchmark compares its steps by, from frames with known times.
	/// </summary>
	public unsafe class FrameWindowTests
	{
		private const double Precision = 1e-6;
		private static readonly long TicksPerMs = Stopwatch.Frequency / 1000;

		private delegate void Setup(ref FrameSample frame, int index);

		private double[] _sections, _phases;

		/// <summary>
		/// Writes frames to a ring, each starting the given milliseconds after the one before (so there
		/// is one frame more than intervals), and collects the window between two marks.
		/// </summary>
		private FrameWindowStats Collect(double[] intervalsMs, long from, long to, int capacity = 64, bool countsAllocations = false, Setup setup = null)
		{
			var ring = new FrameSample[capacity];
			var at = 1000 * TicksPerMs;
			for (var i = 0; i <= intervalsMs.Length; i++) {
				var frame = default(FrameSample);
				frame.Frame = i;
				// every phase of the loop takes a millisecond
				for (var phase = 0; phase < FrameSample.Phases; phase++) {
					frame.Phase[phase] = at + phase * TicksPerMs;
				}
				frame.Timing = TimingState.Matched;
				setup?.Invoke(ref frame, i);
				ring[i % capacity] = frame;
				if (i < intervalsMs.Length) {
					at += (long)(intervalsMs[i] * TicksPerMs);
				}
			}
			fixed (FrameSample* frames = ring) {
				return FrameWindow.Collect(frames, capacity, intervalsMs.Length + 1, from, to, countsAllocations, out _sections, out _phases);
			}
		}

		[Test]
		public void AWindowCountsItsOwnFramesOnly()
		{
			// frames 1 to 10 are the window: what is before and after it takes ten times as long
			var intervals = new double[] { 100, 10, 10, 10, 10, 30, 10, 10, 10, 10, 10, 100, 100 };
			var stats = Collect(intervals, 1, 11, setup: (ref FrameSample frame, int index) => frame.Balls = index == 0 || index >= 11 ? 9 : index % 3);

			stats.Frames.Should().Be(10);
			stats.Seconds.Should().BeApproximately(0.12, Precision);
			stats.FrameAvgMs.Should().BeApproximately(12, Precision);
			stats.AverageFps.Should().BeApproximately(1000.0 / 12, Precision);
			stats.FrameP50Ms.Should().BeApproximately(10, Precision);
			stats.FrameP95Ms.Should().BeApproximately(30, Precision);
			stats.FrameMaxMs.Should().BeApproximately(30, Precision);
			// the slowest hundredth of ten frames is the slowest frame
			stats.Low1PercentFps.Should().BeApproximately(1000.0 / 30, Precision);
			stats.FrameStdDevMs.Should().BeApproximately(6, Precision);
			stats.MaxBalls.Should().Be(2);
		}

		[Test]
		public void TheRingWraps()
		{
			// 20 frames in a ring of 8: frames 12 to 19 are left, frame i takes i + 1 ms
			var intervals = Enumerable.Range(1, 19).Select(ms => (double)ms).ToArray();

			var stats = Collect(intervals, 13, 19, capacity: 8);
			stats.Frames.Should().Be(6);
			stats.FrameAvgMs.Should().BeApproximately(16.5, Precision);
			stats.FrameMaxMs.Should().BeApproximately(19, Precision);

			// an end mark past the newest frame is that frame: its interval isn't known yet
			Collect(intervals, 13, 25, capacity: 8).Frames.Should().Be(6);
			// a window that began before the oldest frame left is gone
			Collect(intervals, 11, 19, capacity: 8).Should().BeNull();
		}

		[Test]
		public void PausedAndOwnFramesAreLeftOutWithTheIntervalThatLeadsToThem()
		{
			// frame 5 is paused and frame 8 is the instrumentation's own: they and the frame before
			// each of them took long
			var intervals = new double[] { 10, 10, 10, 10, 500, 700, 10, 300, 400, 10, 10 };
			var stats = Collect(intervals, 0, 11, setup: (ref FrameSample frame, int index) => {
				frame.Paused = (byte)(index == 5 ? 1 : 0);
				frame.SelfInduced = (byte)(index == 8 ? 1 : 0);
			});

			stats.Frames.Should().Be(7);
			stats.FrameMaxMs.Should().BeApproximately(10, Precision);
			stats.FrameAvgMs.Should().BeApproximately(10, Precision);
		}

		[Test]
		public void TimingsThatHaveNotArrivedAreNotCounted()
		{
			var intervals = Enumerable.Repeat(10.0, 12).ToArray();
			var stats = Collect(intervals, 0, 12, setup: (ref FrameSample frame, int index) => {
				if (index >= 8) {
					// Unity's timings come a few frames late
					frame.Timing = TimingState.Pending;
					frame.CpuMainMs = 99;
					return;
				}
				frame.CpuMainMs = 2;
				frame.CpuRenderMs = 3;
				frame.GpuMs = 4;
				frame.PresentWaitMs = 1;
				frame.FrameCompleteTicks = frame.StartTicks + 6 * TicksPerMs;
				frame.PresentCalledTicks = frame.StartTicks + 5 * TicksPerMs;
			});

			stats.Frames.Should().Be(12);
			stats.TimedFrames.Should().Be(8);
			stats.CpuMainMs.Should().BeApproximately(2, Precision);
			stats.CpuRenderMs.Should().BeApproximately(3, Precision);
			stats.GpuMs.Should().BeApproximately(4, Precision);
			stats.PresentWaitMs.Should().BeApproximately(1, Precision);
			stats.RenderLatencyAvgMs.Should().BeApproximately(6, Precision);
			// presents 10 ms apart, every one of them
			stats.PresentStdDevMs.Should().BeApproximately(0, Precision);
		}

		[Test]
		public void AllocationsSectionsAndPhasesAreAveragesPerFrame()
		{
			var intervals = Enumerable.Repeat(10.0, 12).ToArray();
			var stats = Collect(intervals, 2, 12, countsAllocations: true, setup: (ref FrameSample frame, int index) => {
				// the newest two frames don't have their allocations yet
				frame.GcAllocatedBytes = index >= 11 ? 0 : 2048;
				frame.GcCount = index / 4;
				frame.SectionUsec[3] = 500;
				frame.SnapshotPublishUsec = 1;
				frame.SnapshotAgeUsec = 1500;
			});

			stats.Frames.Should().Be(10);
			// frames 2 to 10 have their count
			stats.GcAllocatedPerFrameKb.Should().BeApproximately(2, Precision);
			stats.GcCollections.Should().Be(2);
			stats.SnapshotAgeAvgMs.Should().BeApproximately(1.5, Precision);
			_sections[3].Should().BeApproximately(0.5, Precision);
			_sections[4].Should().Be(0);
			_phases.Should().HaveCount(FrameSample.Phases - 1);
			_phases.Should().OnlyContain(ms => System.Math.Abs(ms - 1) < Precision);
		}

		[Test]
		public void TooFewFramesGiveNoStatistics()
		{
			var intervals = Enumerable.Repeat(10.0, 12).ToArray();
			Collect(intervals, 4, 5).Should().BeNull();
			Collect(intervals, -1, 5).Should().BeNull();
			// all but one of the frames are paused
			Collect(intervals, 0, 12, setup: (ref FrameSample frame, int index) => frame.Paused = (byte)(index == 3 ? 0 : 1)).Should().BeNull();
		}
	}
}
