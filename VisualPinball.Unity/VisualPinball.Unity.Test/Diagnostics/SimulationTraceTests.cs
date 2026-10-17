// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The trace's modes, and the seqlock that lets a summary read the rings while they're
	/// written.
	/// </summary>
	public class SimulationTraceTests
	{
		private SimulationTraceMode _mode;
		private string _summaryPath;

		[SetUp]
		public void SetUp()
		{
			_mode = SimulationTrace.Mode;
			_summaryPath = SimulationTrace.SummaryPath;
			SimulationTrace.SummaryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vpe-simtrace-test.txt");
		}

		[TearDown]
		public void TearDown()
		{
			SimulationTrace.BeforePublishForTests = null;
			SimulationTrace.End();
			SimulationTrace.PendingEnd?.Wait(TimeSpan.FromSeconds(10));
			SimulationTrace.Mode = _mode;
			SimulationTrace.SummaryPath = _summaryPath;
		}

		[TestCase(true, false, false, SimulationTraceMode.Full)]
		[TestCase(false, true, true, SimulationTraceMode.Off)]
		[TestCase(null, true, true, SimulationTraceMode.Full)]
		[TestCase(null, true, false, SimulationTraceMode.Off)]
		[TestCase(null, false, true, SimulationTraceMode.Summary)]
		[TestCase(null, false, false, SimulationTraceMode.Summary)]
		public void ShouldResolveTheMode(bool? commandLine, bool isEditor, bool toggle, SimulationTraceMode expected)
		{
			SimulationTrace.ResolveMode(commandLine, isEditor, toggle).Should().Be(expected);
		}

		[Test]
		public void OffRecordsNothing()
		{
			SimulationTrace.Mode = SimulationTraceMode.Off;
			SimulationTrace.Begin();
			SimulationTrace.IsRecording.Should().BeFalse();
			SimulationTrace.CommitTick(); // no session: ignored
		}

		[Test]
		public unsafe void ARecordBeingWrittenIsNotRead()
		{
			const int capacity = 4;
			var ring = (long*)UnsafeUtility.Malloc(capacity * sizeof(long), 8, Allocator.Temp);
			var seq = (long*)UnsafeUtility.Malloc(capacity * sizeof(long), 8, Allocator.Temp);
			try {
				UnsafeUtility.MemClear(seq, capacity * sizeof(long));

				// never written
				SimulationTrace.TryRead(ring, seq, capacity, 0, out long _).Should().BeFalse();

				// index 1 committed
				ring[1] = 42;
				seq[1] = 4;
				SimulationTrace.TryRead(ring, seq, capacity, 1, out long value).Should().BeTrue();
				value.Should().Be(42);

				// the writer stopped in the middle of index 5, which overwrites slot 1 (wraparound)
				seq[1] = 11;
				SimulationTrace.TryRead(ring, seq, capacity, 1, out long _).Should().BeFalse();
				SimulationTrace.TryRead(ring, seq, capacity, 5, out long _).Should().BeFalse();

				// index 5 committed: index 1 is lapped, 5 is readable
				seq[1] = 12;
				SimulationTrace.TryRead(ring, seq, capacity, 1, out long _).Should().BeFalse();
				SimulationTrace.TryRead(ring, seq, capacity, 5, out long _).Should().BeTrue();
			} finally {
				UnsafeUtility.Free(ring, Allocator.Temp);
				UnsafeUtility.Free(seq, Allocator.Temp);
			}
		}

		[Test]
		public void ASummaryReadsWhileTheSimulationWrites()
		{
			SimulationTrace.Mode = SimulationTraceMode.Summary;
			SimulationTrace.Begin();
			SimulationTrace.IsRecording.Should().BeTrue();

			// more than the 60 s ring, so the writer laps the reader
			var stop = 0;
			var written = 0L;
			var writer = new Thread(() => {
				var index = 0L;
				while (Volatile.Read(ref stop) == 0) {
					SimulationTrace.Tick = new SimulationTrace.TickRecord {
						Index = index,
						StartUsec = index * 1000,
						TargetUsec = index * 1000,
						SimTimeUsec = index * 1000,
						TotalUsec = 100 + (int)(index % 7),
						WaitUsec = 900,
					};
					SimulationTrace.CommitTick();
					index++;
					if (index % 200 == 0) {
						Thread.Sleep(1);
					}
				}
				Interlocked.Exchange(ref written, index);
			});
			writer.Start();
			try {
				for (var i = 0; i < 5; i++) {
					Thread.Sleep(20);
					var summary = SimulationTrace.SummarizeNow();
					summary.Should().NotBeNull();
					summary.Should().Contain("tick total us");
					// every record a summary takes is a consistent one: total in [100, 106]
					summary.Should().MatchRegex(@"tick total us\s+p50=\s+10[0-6] ");
				}
			} finally {
				Interlocked.Exchange(ref stop, 1);
				writer.Join();
			}
			Interlocked.Read(ref written).Should().BeGreaterThan(0);
		}

		[Test]
		public void EndingPublishesAFinalSummaryAndFreesTheRings()
		{
			SimulationTrace.Mode = SimulationTraceMode.Summary;
			SimulationTrace.Begin();
			for (var i = 0; i < 1000; i++) {
				SimulationTrace.Tick = new SimulationTrace.TickRecord { Index = i, StartUsec = i * 1000, TotalUsec = 200 };
				SimulationTrace.CommitTick();
			}
			SimulationTrace.BeginFrame();
			SimulationTrace.Frame.UnscaledDeltaUsec = 16_666;
			SimulationTrace.CommitFrame();

			var updated = new ManualResetEventSlim();
			string final = null;
			void OnUpdated(string summary)
			{
				if (summary.Contains("(final)") && summary.Contains("ticks recorded: 1000 ")) {
					final = summary;
					updated.Set();
				}
			}
			SimulationTrace.SummaryUpdated += OnUpdated;
			try {
				SimulationTrace.End();
				SimulationTrace.IsRecording.Should().BeFalse();
				updated.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
			} finally {
				SimulationTrace.SummaryUpdated -= OnUpdated;
			}
			final.Should().Contain("ticks recorded: 1000 (summarized 1000, torn 0)");
			final.Should().Contain("frames: 1,");
			SimulationTrace.LatestSummary.Should().Be(final);
			System.IO.File.ReadAllText(SimulationTrace.SummaryPath).Should().Be(final);

			// writes after the end go nowhere
			SimulationTrace.CommitTick();
			SimulationTrace.CommitFrame();
		}

		[Test]
		public void ANewRunClearsTheCachedSummary()
		{
			SimulationTrace.Mode = SimulationTraceMode.Summary;
			SimulationTrace.Begin();
			CommitTicks(10);
			SimulationTrace.SummarizeNow().Should().NotBeNull();
			SimulationTrace.LatestSummary.Should().NotBeNull();
			SimulationTrace.End();
			SimulationTrace.PendingEnd?.Wait(TimeSpan.FromSeconds(10));

			// the next table must not report the last one's timing
			SimulationTrace.Begin();
			SimulationTrace.LatestSummary.Should().BeNull();
		}

		[Test]
		public void ALateSummaryDoesNotReplaceTheFinalOne()
		{
			SimulationTrace.Mode = SimulationTraceMode.Summary;
			SimulationTrace.Begin();
			CommitTicks(1000);
			var session = SimulationTrace.CurrentSession;
			SimulationTrace.End();
			SimulationTrace.PendingEnd?.Wait(TimeSpan.FromSeconds(10));
			var final = SimulationTrace.LatestSummary;
			final.Should().Contain("(final)");

			// a periodic summary that was late to the party
			SimulationTrace.SummarizeLate(session).Should().BeNull();
			SimulationTrace.LatestSummary.Should().Be(final);
		}

		[Test]
		public void ASummaryOfASupersededRunIsNotPublished()
		{
			SimulationTrace.Mode = SimulationTraceMode.Summary;
			SimulationTrace.Begin();
			CommitTicks(100);
			// a new logging session starts while the summary is being computed
			SimulationTrace.BeforePublishForTests = SimulationTrace.OnLogSessionStarted;
			SimulationTrace.SummarizeNow().Should().NotBeNull();
			SimulationTrace.LatestSummary.Should().BeNull();
			System.IO.File.Exists(SimulationTrace.SummaryPath).Should().BeFalse();
		}

		[Test]
		public void ANewRunDeletesTheOldSummaryFile()
		{
			SimulationTrace.Mode = SimulationTraceMode.Summary;
			SimulationTrace.Begin();
			CommitTicks(100);
			SimulationTrace.SummarizeNow();
			System.IO.File.Exists(SimulationTrace.SummaryPath).Should().BeTrue();
			SimulationTrace.End();
			SimulationTrace.PendingEnd?.Wait(TimeSpan.FromSeconds(10));

			// a crash of the next table must not send this one's summary
			SimulationTrace.Begin();
			System.IO.File.Exists(SimulationTrace.SummaryPath).Should().BeFalse();
		}

		private static void CommitTicks(int count)
		{
			for (var i = 0; i < count; i++) {
				SimulationTrace.Tick = new SimulationTrace.TickRecord { Index = i, StartUsec = i * 1000, TotalUsec = 100 };
				SimulationTrace.CommitTick();
			}
		}

		[Test]
		public void EndingDuringASummaryWaitsForIt()
		{
			SimulationTrace.Mode = SimulationTraceMode.Summary;
			SimulationTrace.Begin();
			for (var i = 0; i < 60_000; i++) {
				SimulationTrace.Tick = new SimulationTrace.TickRecord { Index = i, StartUsec = i * 1000, TotalUsec = i % 1000 };
				SimulationTrace.CommitTick();
			}
			var summaries = 0;
			var reader = new Thread(() => {
				for (var i = 0; i < 20; i++) {
					if (SimulationTrace.SummarizeNow() != null) {
						Interlocked.Increment(ref summaries);
					}
				}
			});
			reader.Start();
			Thread.Sleep(5);
			SimulationTrace.End(); // the rings are freed only after the summary that reads them
			reader.Join();
			SimulationTrace.IsRecording.Should().BeFalse();
		}
	}
}
