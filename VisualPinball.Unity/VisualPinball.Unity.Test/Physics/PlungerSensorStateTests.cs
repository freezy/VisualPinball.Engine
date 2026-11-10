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
using NUnit.Framework;
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Drives <see cref="PlungerInputState"/> with recorded-style sample traces,
	/// the way the physics owner does: stage once per frame, then step every
	/// millisecond of that frame.
	/// </summary>
	public class PlungerSensorStateTests
	{
		private const long HostStartUsec = 5_000_000;
		private const ulong SimStartUsec = 123_000;
		private const int SampleIntervalUsec = 8000;

		/// <summary>
		/// One host-clock sample of a trace.
		/// </summary>
		private readonly struct Sample
		{
			public readonly long HostUsec;
			public readonly PlungerSensorChannel Channel;
			public readonly float Value;

			public Sample(long hostUsec, float value, PlungerSensorChannel channel = PlungerSensorChannel.Position)
			{
				HostUsec = hostUsec;
				Value = value;
				Channel = channel;
			}
		}

		/// <summary>
		/// Runs frames of <paramref name="frameUsec"/> over a trace and reports every step.
		/// </summary>
		private sealed class Driver
		{
			public PlungerInputState State;
			public ulong SimUsec = SimStartUsec;
			public long HostUsec = HostStartUsec;
			private readonly List<Sample> _samples = new();
			private int _next;

			public Driver(bool velocityMapped = false)
			{
				State.ConfigureSensors(1);
				State.ConfigureSensor(0, true, velocityMapped, true);
			}

			public void Add(long hostUsec, float value, PlungerSensorChannel channel = PlungerSensorChannel.Position)
			{
				_samples.Add(new Sample(hostUsec, value, channel));
			}

			/// <summary>Adds position samples every 8 ms from <paramref name="fromUsec"/> to <paramref name="toUsec"/>, interpolating linearly.</summary>
			public void Ramp(long fromUsec, long toUsec, float from, float to)
			{
				for (var t = fromUsec; t < toUsec; t += SampleIntervalUsec) {
					var f = (float)(t - fromUsec) / (toUsec - fromUsec);
					Add(t, math.lerp(from, to, f));
				}
			}

			public void RunUntil(long hostEndUsec, long frameUsec, Action<Driver> onStep = null)
			{
				while (HostUsec < hostEndUsec) {
					HostUsec += frameUsec;
					var batchEnd = SimUsec + (ulong)frameUsec;
					while (_next < _samples.Count && _samples[_next].HostUsec <= HostUsec) {
						var s = _samples[_next++];
						State.Stage(0, s.Channel, s.Value, s.HostUsec, batchEnd);
					}
					State.EndStaging(batchEnd);
					while (SimUsec < batchEnd) {
						SimUsec += 1000;
						State.StepOneMillisecond(SimUsec);
						onStep?.Invoke(this);
					}
				}
			}
		}

		[Test]
		public void SensorAtRestStaysInactive()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 2_000_000, 0f, 0f);
			driver.RunUntil(HostStartUsec + 2_000_000, 16_667);

			Assert.That(driver.State.HasSensor, Is.False);
			Assert.That(driver.State.GetPosition(0.2f), Is.EqualTo(0.2f).Within(1e-6f));
		}

		[Test]
		public void PullActivatesAndFollowsThePosition()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 500_000, 0f, 1f);
			driver.Ramp(HostStartUsec + 500_000, HostStartUsec + 1_000_000, 1f, 1f);
			driver.RunUntil(HostStartUsec + 1_000_000, 16_667);

			Assert.That(driver.State.HasSensor, Is.True);
			Assert.That(driver.State.ActiveSourceIndex, Is.EqualTo(0));
			Assert.That(driver.State.RawPosition, Is.EqualTo(1f).Within(0.02f));
			Assert.That(driver.State.GetPosition(0.2f), Is.EqualTo(1f).Within(0.02f));
			Assert.That(driver.State.GetHitVelocity(0.2f), Is.EqualTo(0f));
		}

		[Test]
		public void HeldRetractedStaysActive()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 300_000, 0f, 1f);
			driver.Ramp(HostStartUsec + 300_000, HostStartUsec + 15_000_000, 1f, 1f);
			driver.RunUntil(HostStartUsec + 15_000_000, 16_667);

			Assert.That(driver.State.HasSensor, Is.True);
			Assert.That(driver.State.RawPosition, Is.EqualTo(1f).Within(0.02f));
		}

		[Test]
		public void SensorDeactivatesTenSecondsAfterReturningToRest()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 300_000, 0f, 0.5f);
			driver.Ramp(HostStartUsec + 300_000, HostStartUsec + 2_300_000, 0.5f, 0f);
			driver.Ramp(HostStartUsec + 2_300_000, HostStartUsec + 14_000_000, 0f, 0f);

			driver.RunUntil(HostStartUsec + 11_000_000, 16_667);
			Assert.That(driver.State.HasSensor, Is.True);
			driver.RunUntil(HostStartUsec + 14_000_000, 16_667);
			Assert.That(driver.State.HasSensor, Is.False);
		}

		[Test]
		public void ReleaseInsideOneCatchUpFrameProducesHitVelocity()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 400_000, 0f, 1f);
			driver.Ramp(HostStartUsec + 400_000, HostStartUsec + 800_000, 1f, 1f);
			// Release: the device sees the plunger fly forward, bounce and settle within ~40 ms.
			const long release = HostStartUsec + 800_000;
			driver.Add(release, 1f);
			driver.Add(release + 8000, -0.25f);
			driver.Add(release + 16000, 0.15f);
			driver.Add(release + 24000, -0.05f);
			driver.Ramp(release + 32000, release + 600_000, 0f, 0f);

			driver.RunUntil(release - 20_000, 16_667);
			var minHitVelocity = 0f;
			// One 100 ms frame holds the whole release.
			driver.RunUntil(release + 600_000, 100_000, d => {
				minHitVelocity = math.min(minHitVelocity, d.State.GetHitVelocity(0f));
			});

			Assert.That(minHitVelocity, Is.LessThan(-0.8f * 100f / 13f));
		}

		[Test]
		public void SlowManualReturnDoesNotFire()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 400_000, 0f, 1f);
			driver.Ramp(HostStartUsec + 400_000, HostStartUsec + 800_000, 1f, 1f);
			driver.Ramp(HostStartUsec + 800_000, HostStartUsec + 3_800_000, 1f, 0f);
			driver.Ramp(HostStartUsec + 3_800_000, HostStartUsec + 4_500_000, 0f, 0f);

			var minHitVelocity = 0f;
			driver.RunUntil(HostStartUsec + 4_500_000, 16_667, d => {
				minHitVelocity = math.min(minHitVelocity, d.State.GetHitVelocity(0f));
			});

			Assert.That(minHitVelocity, Is.EqualTo(0f));
		}

		[Test]
		public void MappedVelocityIsUsedAsHitVelocity()
		{
			var driver = new Driver(velocityMapped: true);
			driver.Ramp(HostStartUsec, HostStartUsec + 400_000, 0f, 1f);
			driver.Add(HostStartUsec + 400_000, -6f, PlungerSensorChannel.Velocity);
			driver.RunUntil(HostStartUsec + 450_000, 16_667);

			Assert.That(driver.State.HasSensor, Is.True);
			Assert.That(driver.State.GetHitVelocity(0.25f), Is.EqualTo(-6f * 0.75f).Within(1e-5f));
		}

		[Test]
		public void FirstSampleLandsAtTheBatchEnd()
		{
			var sensor = new PlungerSensorState(true, false);
			sensor.Stage(PlungerSensorChannel.Position, 0.5f, 1_000_000, 2_000_000);
			sensor.EndStaging(2_000_000);

			sensor.StepOneMillisecond(1_999_000);
			Assert.That(sensor.HasPositionReading, Is.False);
			sensor.StepOneMillisecond(2_000_000);
			Assert.That(sensor.HasPositionReading, Is.True);
			Assert.That(sensor.RawPosition, Is.EqualTo(0.5f).Within(1e-5f));
		}

		[Test]
		public void SamplesAheadOfTheBatchAreShiftedBack()
		{
			var sensor = new PlungerSensorState(true, false);
			sensor.Stage(PlungerSensorChannel.Position, 0.1f, 1_000_000, 2_000_000);
			sensor.EndStaging(2_000_000);
			sensor.StepOneMillisecond(2_000_000);

			// Host clock ran 30 ms while the simulation only advanced 10 ms.
			sensor.Stage(PlungerSensorChannel.Position, 0.2f, 1_030_000, 2_010_000);
			sensor.EndStaging(2_010_000);
			for (ulong t = 2_001_000; t <= 2_010_000; t += 1000) {
				sensor.StepOneMillisecond(t);
			}
			Assert.That(sensor.PendingCount, Is.EqualTo(0));
			Assert.That(sensor.PositionReading, Is.EqualTo(0.2f));
		}

		[Test]
		public void OverflowResetsTheClock()
		{
			var sensor = new PlungerSensorState(true, false);
			for (var i = 0; i < PlungerSensorState.PendingCapacity; i++) {
				Assert.That(sensor.Stage(PlungerSensorChannel.Position, 0.5f, 1000 + i, 2_000_000), Is.True);
			}
			Assert.That(sensor.Stage(PlungerSensorChannel.Position, 0.5f, 5000, 2_000_000), Is.False);
			Assert.That(sensor.PendingCount, Is.EqualTo(1));
			Assert.That(sensor.OverflowCount, Is.EqualTo(1));
		}

		[Test]
		public void ClockResetWhileRetractedDoesNotFire()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 400_000, 0f, 1f);
			driver.Ramp(HostStartUsec + 400_000, HostStartUsec + 3_000_000, 1f, 1f);
			driver.RunUntil(HostStartUsec + 1_000_000, 16_667);

			// Pause for a second: the simulation stops, the host clock does not.
			driver.State.ResetClocks();
			driver.HostUsec += 1_000_000;

			var minHitVelocity = 0f;
			driver.RunUntil(HostStartUsec + 3_000_000, 16_667, d => {
				minHitVelocity = math.min(minHitVelocity, d.State.GetHitVelocity(0f));
			});

			Assert.That(minHitVelocity, Is.EqualTo(0f));
			Assert.That(driver.State.HasSensor, Is.True);
			Assert.That(driver.State.RawPosition, Is.EqualTo(1f).Within(0.02f));
		}

		[Test]
		public void CatchUpResetKeepsTheLastStagedPosition()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 400_000, 0f, 1f);
			driver.Ramp(HostStartUsec + 400_000, HostStartUsec + 1_000_000, 1f, 1f);
			driver.RunUntil(HostStartUsec + 1_000_000, 16_667);

			// The plunger is back at rest, reported once, and the frame that
			// stages it gets skipped. A change-only joystick sends nothing else.
			var batchEnd = driver.SimUsec + 16_667;
			driver.State.Stage(0, PlungerSensorChannel.Position, 0f, driver.HostUsec + 5000, batchEnd);
			driver.State.EndStaging(batchEnd);
			driver.State.ResetClocks();

			var minHitVelocity = 0f;
			driver.RunUntil(driver.HostUsec + 500_000, 16_667, d => {
				minHitVelocity = math.min(minHitVelocity, d.State.GetHitVelocity(0f));
			});

			Assert.That(minHitVelocity, Is.EqualTo(0f));
			Assert.That(driver.State.RawPosition, Is.EqualTo(0f).Within(0.02f));
		}

		[Test]
		public void ResetClearsAReleaseInProgress()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 400_000, 0f, 1f);
			driver.Ramp(HostStartUsec + 400_000, HostStartUsec + 800_000, 1f, 1f);
			const long release = HostStartUsec + 800_000;
			driver.Add(release, 1f);
			driver.Add(release + 8000, 0f);
			driver.RunUntil(release + 20_000, 1000);
			Assert.That(driver.State.GetHitVelocity(0f), Is.LessThan(0f), "the release is under way");

			driver.State.ResetClocks();
			Assert.That(driver.State.GetHitVelocity(0f), Is.EqualTo(0f));
		}

		[Test]
		public void ExternalOverrideWinsOverSensors()
		{
			var driver = new Driver();
			driver.Ramp(HostStartUsec, HostStartUsec + 400_000, 0f, 1f);
			driver.RunUntil(HostStartUsec + 400_000, 16_667);

			driver.State.SetExternalPlunger(true, -4f, 0.5f);
			Assert.That(driver.State.ActiveSourceIndex, Is.EqualTo(PlungerInputState.ExternalSource));
			Assert.That(driver.State.GetPosition(0.2f), Is.EqualTo(math.lerp(0.2f, 1f, 0.5f)).Within(1e-6f));
			Assert.That(driver.State.GetHitVelocity(0.2f), Is.EqualTo(-4f * 0.8f).Within(1e-6f));

			driver.State.SetExternalPlunger(false, 0f, 0f);
			Assert.That(driver.State.ActiveSourceIndex, Is.EqualTo(0));
		}

		[Test]
		public void ForwardOfRestMapsTowardsFullyExtended()
		{
			var state = new PlungerInputState();
			state.SetExternalPlunger(true, 0f, -0.25f);
			Assert.That(state.GetPosition(0.2f), Is.EqualTo(0f).Within(1e-6f));
			state.SetExternalPlunger(true, 0f, -0.1f);
			Assert.That(state.GetPosition(0.5f), Is.EqualTo(0.45f).Within(1e-6f));
		}
	}
}
