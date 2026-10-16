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
using System.IO;
using System.Threading;
using FluentAssertions;
using NLog;
using NLog.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	public class LoggingTests
	{
		[Test]
		public void ShouldPassTheFirstOccurrencesThenThrottle()
		{
			var throttle = new LogThrottle(3, TimeSpan.FromHours(1));

			var passed = 0;
			for (var i = 0; i < 20; i++) {
				if (throttle.ShouldLog()) {
					passed++;
				}
			}

			passed.Should().Be(3);
		}

		[Test]
		public void ShouldReportSuppressedOccurrencesWhenTheIntervalElapsed()
		{
			var throttle = new LogThrottle(1, TimeSpan.FromMilliseconds(50));
			throttle.ShouldLog(out _).Should().BeTrue();
			for (var i = 0; i < 7; i++) {
				throttle.ShouldLog(out _).Should().BeFalse();
			}

			Thread.Sleep(80);

			throttle.ShouldLog(out var suppressed).Should().BeTrue();
			suppressed.Should().Be(7);
		}

		[Test]
		public void ShouldStartOverAfterReset()
		{
			var throttle = new LogThrottle(1, TimeSpan.FromHours(1));
			throttle.ShouldLog().Should().BeTrue();
			throttle.ShouldLog().Should().BeFalse();

			throttle.Reset();

			throttle.ShouldLog().Should().BeTrue();
		}

		[Test]
		public void ShouldNotLoseCountsAcrossThreads()
		{
			var throttle = new LogThrottle(100, TimeSpan.FromHours(1));
			var passed = 0;
			var threads = new List<Thread>();
			for (var t = 0; t < 4; t++) {
				var thread = new Thread(() => {
					for (var i = 0; i < 1000; i++) {
						if (throttle.ShouldLog()) {
							Interlocked.Increment(ref passed);
						}
					}
				});
				threads.Add(thread);
				thread.Start();
			}
			threads.ForEach(t => t.Join());

			passed.Should().Be(100);
		}

		[Test]
		public void ShouldLetExactlyOneThroughWhenThreadsRaceForASingleSlot()
		{
			const int threadCount = 4;
			for (var round = 0; round < 200; round++) {
				var throttle = new LogThrottle(1, TimeSpan.FromHours(1));
				var passed = 0;
				using var barrier = new Barrier(threadCount);
				var threads = new List<Thread>();
				for (var t = 0; t < threadCount; t++) {
					var thread = new Thread(() => {
						barrier.SignalAndWait();
						if (throttle.ShouldLog()) {
							Interlocked.Increment(ref passed);
						}
					});
					threads.Add(thread);
					thread.Start();
				}
				threads.ForEach(t => t.Join());

				passed.Should().Be(1, $"round {round}");
			}
		}

		[Test]
		public void ShouldLogOncePerKey()
		{
			var once = new LogOncePerKey<string>();

			once.ShouldLog("coil1").Should().BeTrue();
			once.ShouldLog("coil1").Should().BeFalse();
			once.ShouldLog("coil2").Should().BeTrue();
		}

		[Test]
		public void ShouldLogKeysAgainInANewSession()
		{
			var once = new LogOncePerKey<int>();
			once.ShouldLog(1).Should().BeTrue();

			LogOncePerKeyVersion.Next();

			once.ShouldLog(1).Should().BeTrue();
		}

		[Test]
		public void ShouldThrottleOncePerKeyBeyondTheKeyLimit()
		{
			var once = new LogOncePerKey<int>();
			for (var i = 0; i < LogOncePerKey<int>.MaxKeys; i++) {
				once.ShouldLog(i).Should().BeTrue();
			}

			// the overflow throttle lets the first one through, then holds back
			once.ShouldLog(-1).Should().BeTrue();
			once.ShouldLog(-2).Should().BeFalse();
		}

		[TestCase(new[] { "-vpe-log-level=debug" }, "Debug")]
		[TestCase(new[] { "--vpe-log-level=debug" }, "Debug")]
		[TestCase(new[] { "player.exe", "--vpe-log-level", "warn" }, "Warn")]
		[TestCase(new[] { "player.exe", "-vpe-log-level", "trace" }, "Trace")]
		[TestCase(new[] { "-VPE-LOG-LEVEL=Warning" }, "Warn")]
		[TestCase(new[] { "-vpe-log-level" }, null)]
		[TestCase(new[] { "-vpe-log-level=loud" }, null)]
		[TestCase(new[] { "-vpe-log-levelx=debug" }, null)]
		[TestCase(new string[0], null)]
		public void ShouldParseTheLevelFromTheCommandLine(string[] args, string expected)
		{
			(VpeLog.ParseCommandLineLevel(args)?.Name).Should().Be(expected);
		}

		[TestCase(new[] { "player.exe", "--simulation-trace" }, true)]
		[TestCase(new[] { "player.exe", "-simulation-trace" }, true)]
		[TestCase(new[] { "--no-simulation-trace" }, false)]
		[TestCase(new[] { "-NO-SIMULATION-TRACE" }, false)]
		[TestCase(new[] { "--simulation-trace", "--no-simulation-trace" }, false)]
		[TestCase(new[] { "-no-simulation-trace", "-simulation-trace" }, true)]
		[TestCase(new[] { "player.exe", "--simulation-tracex", "---simulation-trace" }, null)]
		[TestCase(new string[0], null)]
		public void ShouldParseTheSimulationTraceSwitch(string[] args, bool? expected)
		{
			SimulationTrace.ParseCommandLineOverride(args).Should().Be(expected);
		}

		[Test]
		public void ShouldKeepTheNewestLinesWithinTheBudget()
		{
			LogTail.Clear();
			try {
				for (var i = 0; i < LogTail.Capacity + 5; i++) {
					LogTail.Append("line " + i);
				}

				var all = LogTail.Snapshot(int.MaxValue).Split('\n', StringSplitOptions.RemoveEmptyEntries);
				all.Length.Should().Be(LogTail.Capacity);
				all[0].Should().Be("line 5");
				all[^1].Should().Be("line " + (LogTail.Capacity + 4));

				var lastLine = "line " + (LogTail.Capacity + 4) + "\n";
				LogTail.Snapshot(lastLine.Length).Should().Be(lastLine);
				LogTail.Snapshot(3).Should().BeEmpty();

			} finally {
				LogTail.Clear();
			}
		}

		[Test]
		public void ShouldWriteExceptionsAsTextNotAsUnityExceptions()
		{
			// Debug.LogException would turn every logged exception, warnings included,
			// into an "unhandled exception" for error reporting.
			var factory = new LogFactory();
			var config = new LoggingConfiguration(factory);
			config.AddRuleForAllLevels(new UnityTarget { Layout = "${message}|${exception}" });
			factory.Configuration = config;
			var received = new List<LogType>();
			void OnLog(string message, string stackTrace, LogType type) => received.Add(type);
			Application.logMessageReceived += OnLog;
			try {
				LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"recovered\|.*boom"));

				factory.GetLogger("Test").Warn(new InvalidOperationException("boom"), "recovered");

			} finally {
				Application.logMessageReceived -= OnLog;
				factory.Dispose();
			}

			received.Should().Equal(LogType.Warning);
		}

		[Test]
		public void ShouldNotCarryLinesIntoTheNextSession()
		{
			var logger = LogManager.GetLogger("VisualPinball.Unity.Test.Session");
			VpeLog.BeginSession();
			logger.Info("from session one");

			VpeLog.BeginSession();
			VpeLog.Flush(TimeSpan.FromSeconds(2));

			VpeLog.GetTail().Should().NotContain("from session one");
		}

		[Test]
		public void ShouldStartTheFileWithWhatWasLoggedBeforeTheFolderWasKnown()
		{
			var logger = LogManager.GetLogger("VisualPinball.Unity.Test.Session");
			var dir = Path.Combine(Path.GetTempPath(), "vpe-log-test-" + Guid.NewGuid().ToString("N"));
			try {
				VpeLog.Configure("");
				VpeLog.BeginSession();
				logger.Info("before the folder");

				VpeLog.Configure(dir);
				logger.Info("after the folder");
				VpeLog.Flush(TimeSpan.FromSeconds(2));

				var text = ReadShared(VpeLog.LogFile);
				text.Should().Contain("before the folder").And.Contain("after the folder");
				text.Should().Contain(VpeLog.SessionId);

			} finally {
				VpeLog.Configure("");
				Directory.Delete(dir, true);
			}
		}

		[Test]
		public void ShouldOpenTheFileWhenTheSessionStartsAfterTheFolderWasSet()
		{
			var logger = LogManager.GetLogger("VisualPinball.Unity.Test.Session");
			var dir = Path.Combine(Path.GetTempPath(), "vpe-log-test-" + Guid.NewGuid().ToString("N"));
			try {
				VpeLog.Configure(dir);
				VpeLog.BeginSession();
				logger.Warn("in the new session");
				VpeLog.Flush(TimeSpan.FromSeconds(2));

				VpeLog.LogFile.Should().Contain(VpeLog.SessionId);
				var text = ReadShared(VpeLog.LogFile);
				text.Should().Contain("Session " + VpeLog.SessionId + " started");
				text.Should().Contain("|WARN |main|Session|in the new session");

			} finally {
				VpeLog.Configure("");
				Directory.Delete(dir, true);
			}
		}

		[Test]
		public void ShouldNotRenderEventsOnTheLoggingThread()
		{
			// NLog itself formats events with object arguments when it creates them, to capture
			// their state; numbers and strings are left for later. The queue must not format.
			var memory = new NLog.Targets.MemoryTarget { Layout = "${message}" };
			var queue = new LogQueueTarget("queue", memory, 100, startWriter: false);
			using var factory = Factory(queue);
			var logEvent = new LogEventInfo(LogLevel.Warn, "Test", null, "Coil {Coil} changed to {Enabled}", new object[] { 42, true });

			factory.GetLogger("Test").Log(logEvent);

			FormattedMessageField.GetValue(logEvent).Should().BeNull();
			memory.Logs.Should().BeEmpty();

			queue.Drain();

			memory.Logs.Should().Equal("Coil 42 changed to true");
			queue.Count.Should().Be(0);
		}

		private static readonly System.Reflection.FieldInfo FormattedMessageField = typeof(LogEventInfo)
			.GetField("_formattedMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		[Test]
		public void ShouldKeepItsCapacityWhenATargetDrainsFromInsideAWrite()
		{
			var reentrant = new ReentrantTarget();
			var queue = new LogQueueTarget("queue", reentrant, 1, startWriter: false);
			reentrant.Queue = queue;
			using var factory = Factory(queue);
			var logger = factory.GetLogger("Test");

			logger.Info("one");
			queue.Drain();
			logger.Info("two");
			queue.Drain();

			reentrant.Writes.Should().Be(2);
			queue.Count.Should().Be(0);
		}

		[Test]
		public void ShouldCompleteAFlushThroughTheWriter()
		{
			var sink = new FlushCountingTarget { Layout = "${message}" };
			var queue = new LogQueueTarget("queue", sink, 100);
			using var factory = Factory(queue);
			Exception flushError = new TimeoutException("never completed");
			using var flushed = new ManualResetEventSlim();

			factory.GetLogger("Test").Info("flushed");
			factory.Flush(e => { flushError = e; flushed.Set(); }, TimeSpan.FromSeconds(5));

			flushed.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
			flushError.Should().BeNull();
			sink.Flushes.Should().BeGreaterThan(0);
			sink.Logs.Should().Equal("flushed");
		}

		[Test]
		public void ShouldHaveWrittenEveryEarlierEventWhenAFlushCompletes()
		{
			// without a writer, flushes are drained by pool threads that may overlap
			var sink = new FlushCountingTarget { Layout = "${message}" };
			var queue = new LogQueueTarget("queue", sink, 10000, startWriter: false);
			using var factory = Factory(queue);
			var logger = factory.GetLogger("Test");
			var failures = 0;
			using var done = new CountdownEvent(4 * 50);
			var threads = new List<Thread>();
			for (var t = 0; t < 4; t++) {
				var id = t;
				threads.Add(new Thread(() => {
					for (var i = 0; i < 50; i++) {
						var message = $"t{id}-{i}";
						logger.Fatal(message);
						factory.Flush(e => {
							bool written;
							lock (sink.Logs) {
								written = sink.Logs.Contains(message);
							}
							if (e != null || !written) {
								Interlocked.Increment(ref failures);
							}
							done.Signal();
						}, TimeSpan.FromSeconds(10));
					}
				}));
			}
			threads.ForEach(t => t.Start());
			threads.ForEach(t => t.Join());

			done.Wait(TimeSpan.FromSeconds(20)).Should().BeTrue();
			failures.Should().Be(0);
		}

		[Test]
		public void ShouldStopTheWriterAndWriteWhatsLeft()
		{
			var memory = new NLog.Targets.MemoryTarget { Layout = "${message}" };
			var queue = new LogQueueTarget("queue", memory, 100);
			using var factory = Factory(queue);

			factory.GetLogger("Test").Info("before stop");
			queue.Stop(TimeSpan.FromSeconds(2)).Should().BeTrue();

			memory.Logs.Should().Equal("before stop");
			queue.Count.Should().Be(0);
		}

		private static LogFactory Factory(NLog.Targets.Target target)
		{
			var factory = new LogFactory();
			var config = new LoggingConfiguration(factory);
			config.AddRuleForAllLevels(target);
			factory.Configuration = config;
			return factory;
		}

		private sealed class FlushCountingTarget : NLog.Targets.TargetWithLayout
		{
			public readonly List<string> Logs = new();
			public int Flushes;

			protected override void Write(LogEventInfo logEvent)
			{
				lock (Logs) {
					Logs.Add(RenderLogEvent(Layout, logEvent));
				}
			}

			protected override void FlushAsync(NLog.Common.AsyncContinuation asyncContinuation)
			{
				Interlocked.Increment(ref Flushes);
				asyncContinuation(null);
			}
		}

		private sealed class ReentrantTarget : NLog.Targets.Target
		{
			public LogQueueTarget Queue;
			public int Writes;

			protected override void Write(LogEventInfo logEvent)
			{
				Writes++;
				// a target that logs or flushes while being written to ends up here
				Queue.Drain();
			}
		}

		private static string ReadShared(string path)
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}
	}
}
