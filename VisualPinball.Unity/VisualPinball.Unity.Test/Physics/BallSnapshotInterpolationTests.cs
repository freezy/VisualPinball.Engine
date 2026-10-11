// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using NUnit.Framework;
using Unity.Mathematics;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	public class BallSnapshotInterpolationTests
	{
		private const long Step = 1000;

		[Test]
		public void RenderTimeTrailsTheSimulationClock()
		{
			Assert.That(BallSnapshotInterpolation.RenderTimeUsec(50_000, 0, 1f), Is.EqualTo(48_500));
			Assert.That(BallSnapshotInterpolation.RenderTimeUsec(50_000, 400, 1f), Is.EqualTo(48_900));
			Assert.That(BallSnapshotInterpolation.RenderTimeUsec(50_000, 400, 0.5f), Is.EqualTo(48_700));
			Assert.That(BallSnapshotInterpolation.RenderTimeUsec(50_000, -50, 1f), Is.EqualTo(48_500));
		}

		[Test]
		public void SegmentPicksTheBracketingPoses()
		{
			var t = BallSnapshotInterpolation.Segment(9_250, 10_000, 9_000, 8_000, 7_000, out var segment);
			Assert.That(segment, Is.EqualTo(0));
			Assert.That(t, Is.EqualTo(0.25f).Within(1e-6));

			t = BallSnapshotInterpolation.Segment(8_750, 10_000, 9_000, 8_000, 7_000, out segment);
			Assert.That(segment, Is.EqualTo(1));
			Assert.That(t, Is.EqualTo(0.75f).Within(1e-6));

			t = BallSnapshotInterpolation.Segment(7_100, 10_000, 9_000, 8_000, 7_000, out segment);
			Assert.That(segment, Is.EqualTo(2));
			Assert.That(t, Is.EqualTo(0.1f).Within(1e-6));
		}

		[Test]
		public void SegmentClampsOutsideTheHistory()
		{
			var t = BallSnapshotInterpolation.Segment(10_500, 10_000, 9_000, 8_000, 7_000, out var segment);
			Assert.That(segment, Is.EqualTo(0));
			Assert.That(t, Is.EqualTo(1f));

			t = BallSnapshotInterpolation.Segment(6_000, 10_000, 9_000, 8_000, 7_000, out segment);
			Assert.That(segment, Is.EqualTo(2));
			Assert.That(t, Is.EqualTo(0f));

			// only one step of history yet: clamp to the oldest pose there is
			t = BallSnapshotInterpolation.Segment(8_500, 10_000, 9_000, 9_000, 9_000, out segment);
			Assert.That(segment, Is.EqualTo(0));
			Assert.That(t, Is.EqualTo(0f));
		}

		[Test]
		public void SegmentWithoutHistoryRendersTheCurrentPose()
		{
			// first snapshot: all times equal
			var t = BallSnapshotInterpolation.Segment(9_000, 10_000, 10_000, 10_000, 10_000, out var segment);
			Assert.That(segment, Is.EqualTo(0));
			Assert.That(t, Is.EqualTo(1f));
		}

		[Test]
		public void TeleportIsFurtherThanOneBallDiameter()
		{
			Assert.That(BallSnapshotInterpolation.IsTeleport(new float3(0, 0, 25), new float3(10, 0, 25), 25), Is.False);
			Assert.That(BallSnapshotInterpolation.IsTeleport(new float3(0, 0, 25), new float3(49, 0, 25), 25), Is.False);
			Assert.That(BallSnapshotInterpolation.IsTeleport(new float3(0, 0, 25), new float3(400, 300, 25), 25), Is.True);
		}

		/// <summary>
		/// A ball at constant speed, 1 kHz ticks, steady 60 fps frames. Without
		/// interpolation the per-frame displacement varies with the tick phase;
		/// with it, it must be constant.
		/// </summary>
		[TestCase(0L, 0L)]
		[TestCase(300L, 150L)]
		[TestCase(999L, 420L)]
		[TestCase(1L, 499L)]
		[TestCase(500L, 30L)]
		public void ConstantSpeedRendersAtConstantSpeed(long simOffsetUsec, long publishDelayUsec)
		{
			const double speed = 0.01; // units per microsecond
			const long frameUsec = 16_667;
			const int frames = 120;

			var interpolated = new double[frames];
			var latest = new double[frames];
			for (var f = 0; f < frames; f++) {
				var wallUsec = 1_000_000 + f * frameUsec;

				// latest tick whose snapshot is published by now
				var tick = (wallUsec - publishDelayUsec) / Step;
				var tickScheduledUsec = tick * Step;
				var simTimeUsec = tickScheduledUsec + simOffsetUsec;

				// physics steps on its own 1 ms grid until it reaches the sim time
				var time0 = (simTimeUsec + Step - 1) / Step * Step;
				var time1 = time0 - Step;
				var time2 = time0 - 2 * Step;
				var time3 = time0 - 3 * Step;

				var renderTimeUsec = BallSnapshotInterpolation.RenderTimeUsec(simTimeUsec, wallUsec - tickScheduledUsec, 1f);
				var t = BallSnapshotInterpolation.Segment(renderTimeUsec, time0, time1, time2, time3, out var segment);
				var to = time0 - segment * Step;
				var from = to - Step;

				interpolated[f] = math.lerp(from * speed, to * speed, t);
				latest[f] = time0 * speed;
			}

			var expected = frameUsec * speed;
			var latestSpread = 0d;
			for (var f = 1; f < frames; f++) {
				Assert.That(interpolated[f] - interpolated[f - 1], Is.EqualTo(expected).Within(expected * 1e-3), $"frame {f}");
				latestSpread = Math.Max(latestSpread, Math.Abs(latest[f] - latest[f - 1] - expected));
			}
			// sanity: the uninterpolated path does show the phase judder this fixes
			Assert.That(latestSpread, Is.GreaterThan(expected * 0.01));
		}
	}
}
