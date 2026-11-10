// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class PlungerProbeTests
	{
		private long _host = 50000000;

		/// <summary>
		/// Feeds one sample every 2 ms and steps the probe every 10 ms.
		/// </summary>
		private void Run(PlungerProbe probe, int durationMs, Func<int, float> position)
		{
			for (var ms = 0; ms < durationMs; ms += 10) {
				for (var sub = 0; sub < 10; sub += 2) {
					_host += 2000;
					probe.Enqueue(PlungerSensorChannel.Position, position(ms + sub), _host);
				}
				probe.Step(10000);
			}
		}

		[Test]
		public void RestDoesNotLaunch()
		{
			var probe = new PlungerProbe();
			Run(probe, 500, _ => 0f);
			var snapshot = probe.Snapshot();
			Assert.That(snapshot.IsActive, Is.False);
			Assert.That(snapshot.LaunchCount, Is.EqualTo(0));
			Assert.That(snapshot.PlungerPosition, Is.EqualTo(PlungerProbe.RestPosition).Within(1e-3f));
		}

		[Test]
		public void PullFollowsTheSensor()
		{
			var probe = new PlungerProbe();
			Run(probe, 200, _ => 0f);
			Run(probe, 600, ms => System.Math.Min(1f, ms / 400f));
			var snapshot = probe.Snapshot();
			Assert.That(snapshot.IsActive, Is.True);
			Assert.That(snapshot.FilteredPosition, Is.EqualTo(1f).Within(0.05f));
			Assert.That(snapshot.PlungerPosition, Is.GreaterThan(0.9f));
			Assert.That(snapshot.BallPosition, Is.LessThan(0f), "the ball follows the tip back");
		}

		[Test]
		public void ReleaseLaunchesTheBall()
		{
			var probe = new PlungerProbe();
			Run(probe, 200, _ => 0f);
			Run(probe, 600, ms => System.Math.Min(1f, ms / 400f));
			Run(probe, 300, _ => 0f);
			var snapshot = probe.Snapshot();
			Assert.That(snapshot.LaunchCount, Is.EqualTo(1));
			Assert.That(snapshot.LastLaunchFraction, Is.GreaterThan(0.6f));
		}

		[Test]
		public void SlowReturnDoesNotLaunch()
		{
			var probe = new PlungerProbe();
			Run(probe, 200, _ => 0f);
			Run(probe, 600, ms => System.Math.Min(1f, ms / 400f));
			Run(probe, 3000, ms => System.Math.Max(0f, 1f - ms / 2500f));
			Assert.That(probe.Snapshot().LaunchCount, Is.EqualTo(0));
		}

		[Test]
		public void LongGapsRestartInsteadOfCatchingUp()
		{
			var probe = new PlungerProbe();
			Run(probe, 200, _ => 0f);
			var before = probe.Snapshot().TimeUsec;
			probe.Step(5000000);
			Assert.That(probe.Snapshot().TimeUsec - before, Is.LessThanOrEqualTo(100000UL));
		}
	}
}
