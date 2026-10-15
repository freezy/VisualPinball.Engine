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
using FluentAssertions;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class ReportDedupeTests
	{
		[Test]
		public void ShouldSendTheFirstOfEachKindOnly()
		{
			var dedupe = new ReportDedupe();
			dedupe.ShouldSend("a", false, out var repeats).Should().BeTrue();
			repeats.Should().Be(0);
			dedupe.ShouldSend("a", false, out repeats).Should().BeFalse();
			repeats.Should().Be(1);
			dedupe.ShouldSend("a", false, out repeats).Should().BeFalse();
			repeats.Should().Be(2);
			dedupe.ShouldSend("b", false, out _).Should().BeTrue();
		}

		[Test]
		public void ShouldStopAtTheCap()
		{
			var dedupe = new ReportDedupe(3);
			for (var i = 0; i < 3; i++) {
				dedupe.ShouldSend("key" + i, false, out _).Should().BeTrue();
			}
			dedupe.ShouldSend("key3", false, out _).Should().BeFalse();
		}

		[Test]
		public void ShouldAlwaysSendTheFirstFatalOne()
		{
			var dedupe = new ReportDedupe(1);
			dedupe.ShouldSend("a", false, out _).Should().BeTrue();
			dedupe.ShouldSend("b", false, out _).Should().BeFalse();
			dedupe.ShouldSend("crash", true, out _).Should().BeTrue();
			dedupe.ShouldSend("crash", true, out _).Should().BeFalse();
		}

		[Test]
		public void ShouldSendAFatalOneEvenIfTheSameKindWasAnUnsentError()
		{
			var dedupe = new ReportDedupe(1);
			dedupe.ShouldSend("a", false, out _).Should().BeTrue();
			dedupe.ShouldSend("crash", false, out _).Should().BeFalse("the cap is reached");
			dedupe.ShouldSend("crash", true, out _).Should().BeTrue("the first fatal one always goes");
			dedupe.ShouldSend("crash", true, out _).Should().BeFalse();
			dedupe.ShouldSend("crash", false, out _).Should().BeFalse();
		}

		[Test]
		public void ShouldStartOverAfterReset()
		{
			var dedupe = new ReportDedupe(1);
			dedupe.ShouldSend("a", false, out _).Should().BeTrue();
			dedupe.Reset();
			dedupe.ShouldSend("a", false, out _).Should().BeTrue();
		}

		[Test]
		public void ShouldKeyExceptionsByTypeAndPlaceNotMessage()
		{
			var first = Throw(1);
			var second = Throw(2);
			first.Message.Should().NotBe(second.Message);
			ReportDedupe.Key(first, "L", "t").Should().Be(ReportDedupe.Key(second, "Other", "other"));
			ReportDedupe.Key(first, "L", "t").Should().StartWith("ex|System.InvalidOperationException|");
		}

		[Test]
		public void ShouldKeyWrappedExceptionsByTheInnerOne()
		{
			var inner = Throw(1);
			var wrapped = new AggregateException(inner);
			ReportDedupe.Key(wrapped, "L", "t").Should().Be(ReportDedupe.Key(inner, "L", "t"));
		}

		[Test]
		public void ShouldKeyLogLinesByLoggerAndTemplate()
		{
			ReportDedupe.Key(null, "A", "Cannot load {Path}").Should().Be(ReportDedupe.Key(null, "A", "Cannot load {Path}"));
			ReportDedupe.Key(null, "A", "Cannot load {Path}").Should().NotBe(ReportDedupe.Key(null, "B", "Cannot load {Path}"));
		}

		private static Exception Throw(int value)
		{
			try {
				throw new InvalidOperationException("value " + value);
			} catch (Exception e) {
				return e;
			}
		}
	}
}
