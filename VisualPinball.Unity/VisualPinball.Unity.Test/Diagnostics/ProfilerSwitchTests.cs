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

using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Unity.Profiling;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The performance instrumentation is off unless something turns it on: normal play records
	/// nothing and holds no memory for it.
	/// </summary>
	public class ProfilerSwitchTests
	{
		[TearDown]
		public void TearDown()
		{
			VpeProfiler.Stop(ProfilerPurpose.Benchmark);
			VpeProfiler.Stop(ProfilerPurpose.Session);
		}

		[Test]
		public void ItIsOffByDefault()
		{
			VpeProfiler.IsEnabled.Should().BeFalse();
			VpeProfiler.Purpose.Should().Be(ProfilerPurpose.Off);
			VpeProfiler.ReportsIncidents.Should().BeFalse();
			FrameWindow.Mark().Should().Be(-1);
		}

		[Test]
		public unsafe void CallsDoNothingWhileOff()
		{
			VpeProfiler.Begin(ProfileSection.PlayerOnUpdate);
			VpeProfiler.End(ProfileSection.PlayerOnUpdate);
			VpeProfiler.Mark(ProfileEvent.UserMarker);
			VpeProfiler.Count(ProfileCounter.LampChanges);

			(FrameRecorder.Frames == null).Should().BeTrue("the rings are only allocated while recording");
			SectionRecorder.FrameTicks[(int)ProfileSection.PlayerOnUpdate].Should().Be(0);
		}

		[Test]
		public unsafe void ABenchmarkTurnsItOnAndOff()
		{
			VpeProfiler.Start(ProfilerPurpose.Benchmark).Should().BeTrue();
			VpeProfiler.IsEnabled.Should().BeTrue();
			VpeProfiler.Purpose.Should().Be(ProfilerPurpose.Benchmark);
			VpeProfiler.ReportsIncidents.Should().BeFalse("a benchmark reports its own numbers");
			(FrameRecorder.Frames != null).Should().BeTrue();
			FrameWindow.Mark().Should().BeGreaterOrEqualTo(0);

			VpeProfiler.Stop(ProfilerPurpose.Benchmark);
			VpeProfiler.IsEnabled.Should().BeFalse();
			(FrameRecorder.Frames == null).Should().BeTrue();
			FrameWindow.Mark().Should().Be(-1);
		}

		[Test]
		public void ASessionKeepsRunningWhenABenchmarkEnds()
		{
			VpeProfiler.Start(ProfilerPurpose.Session).Should().BeTrue();
			VpeProfiler.ReportsIncidents.Should().BeTrue();
			VpeProfiler.Start(ProfilerPurpose.Benchmark).Should().BeFalse("it already runs for the session");

			VpeProfiler.Stop(ProfilerPurpose.Benchmark);
			VpeProfiler.IsEnabled.Should().BeTrue();
			VpeProfiler.Purpose.Should().Be(ProfilerPurpose.Session);
		}

		[Test]
		public void ASectionBegunBeforeItWasTurnedOnDoesNotCount()
		{
			VpeProfiler.Begin(ProfileSection.GleUpdate); // off: ignored
			VpeProfiler.Start(ProfilerPurpose.Benchmark);
			VpeProfiler.End(ProfileSection.GleUpdate);
			SectionRecorder.FrameTicks[(int)ProfileSection.GleUpdate].Should().Be(0);

			VpeProfiler.Begin(ProfileSection.GleUpdate);
			VpeProfiler.End(ProfileSection.GleUpdate);
			SectionRecorder.FrameTicks[(int)ProfileSection.GleUpdate].Should().BeGreaterOrEqualTo(0);
		}

		[TestCase(0.5, 3.0)]
		[TestCase(0.95, 5.0)]
		[TestCase(0.99, 5.0)]
		[TestCase(0.2, 1.0)]
		public void PercentilesUseTheNearestRank(double share, double expected)
		{
			var sorted = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
			FrameWindow.Percentile(sorted, sorted.Length, share).Should().Be(expected);
		}

		[Test]
		public void TooFewFramesGiveNoStatistics()
		{
			VpeProfiler.Start(ProfilerPurpose.Benchmark);
			var mark = FrameWindow.Mark();
			FrameWindow.Collect(mark, mark).Should().BeNull();
			FrameWindow.Collect(-1, mark).Should().BeNull();
		}
	}
}
