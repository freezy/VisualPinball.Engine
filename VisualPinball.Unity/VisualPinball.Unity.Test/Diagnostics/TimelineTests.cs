// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Plugins;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The start-up and table load timelines, and when a load counts as settled.
	/// </summary>
	public class TimelineTests
	{
		[Test]
		public void ShouldMeasureBetweenMilestones()
		{
			var timeline = new Timeline("test", "begin");
			Thread.Sleep(30);
			timeline.Mark("first");
			timeline.Mark("second");

			timeline.MsTo("first").Should().BeGreaterOrEqualTo(25);
			timeline.MsTo("second").Should().BeGreaterOrEqualTo(timeline.MsTo("first"));
			timeline.MsTo("missing").Should().Be(-1);
			timeline.TotalMs.Should().Be(timeline.MsTo("second"));
		}

		[Test]
		public void ShouldFoldShortIntervalsIntoTheNextOne()
		{
			var timeline = new Timeline("test", "begin");
			timeline.Mark("quick");
			Thread.Sleep(30);
			timeline.Mark("slow");
			timeline.Mark("last");

			var line = timeline.Format(minMs: 20);
			line.Should().StartWith("slow ");
			line.Should().NotContain("quick");
			line.Should().Contain("last "); // the last interval is always shown
		}

		[Test]
		public void ShouldStartInThePast()
		{
			var start = Stopwatch.GetTimestamp() - Stopwatch.Frequency; // a second ago
			var timeline = new Timeline("test", "process-start", start, 0);
			timeline.Mark("managed");
			timeline.MsTo("managed").Should().BeInRange(990, 5000);
		}

		[Test]
		public void ShouldReplaceFacts()
		{
			var timeline = new Timeline("test", "begin");
			timeline.Fact("texture-cache", "miss");
			timeline.Fact("texture-cache", "cooked");
			timeline.GetFact("texture-cache").Should().Be("cooked");
			timeline.Facts.Should().HaveCount(1);
		}

		[Test]
		public void ShouldDescribeEveryMilestone()
		{
			var timeline = new Timeline("test", "begin");
			timeline.Mark("read");
			timeline.Fact("load", "reload");
			var w = new DiagnosticsWriter();
			timeline.Describe(w, "timeline");
			var keys = new System.Collections.Generic.List<string>();
			foreach (var entry in w.Entries) {
				keys.Add(entry.Key);
			}
			keys.Should().Contain(new[] { "timeline.total-ms", "timeline.read", "timeline.load" });
		}

		[Test]
		public void SteadyFramesAreStable()
		{
			var frames = new float[180];
			for (var i = 0; i < frames.Length; i++) {
				frames[i] = 16.6f + (i % 3) * 0.2f;
			}
			LoadAttempt.IsStable(frames, frames.Length, 16.6f).Should().BeTrue();
		}

		[Test]
		public void OneLongFrameIsNotStable()
		{
			var frames = new float[180];
			for (var i = 0; i < frames.Length; i++) {
				frames[i] = 16.6f;
			}
			frames[90] = 40f;
			LoadAttempt.IsStable(frames, frames.Length, 40f).Should().BeFalse();
		}

		[Test]
		public void AHitchOutsideTheBufferIsNotStable()
		{
			// a fast frame rate fills the buffer with steady frames; the hitch only shows in the maximum
			var frames = new float[2048];
			for (var i = 0; i < frames.Length; i++) {
				frames[i] = 1f;
			}
			LoadAttempt.IsStable(frames, frames.Length, 250f).Should().BeFalse();
		}
	}
}
