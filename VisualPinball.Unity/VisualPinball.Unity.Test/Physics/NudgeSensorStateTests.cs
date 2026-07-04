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

using NUnit.Framework;
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	public class NudgeSensorStateTests
	{
		[Test]
		public void KalmanRestConstraintsDrainConstantBias()
		{
			var axis = MotionKalmanAxis.Default;
			for (ulong t = 1000; t <= 500000; t += 1000) {
				axis.UpdateAcceleration(t, 0.3f);
				axis.UpdateRestConstraints(t);
			}

			Assert.That(math.abs(axis.Acceleration), Is.LessThan(0.02f));
			Assert.That(math.abs(axis.Velocity), Is.LessThan(0.01f));
		}

		[Test]
		public void GainCalibratorConvergesAcrossMotionSegments()
		{
			var calibrator = MotionGainCalibratorAxis.Default;
			const float expectedGain = 2.0f;
			ulong baseTime = 0;

			for (var segment = 0; segment < 30; segment++) {
				calibrator.StartSegment(baseTime);
				var velocity = 0f;
				var previousAcceleration = 0f;
				for (var i = 0; i <= 100; i++) {
					var t = i / 100f;
					var acceleration = math.sin(2f * math.PI * t);
					if (i > 0) {
						velocity += 0.5f * (previousAcceleration + acceleration) * 0.001f;
					}
					calibrator.AddSample(baseTime + (ulong)(i * 1000), expectedGain * velocity, acceleration);
					previousAcceleration = acceleration;
				}
				calibrator.EndSegment();
				baseTime += 150000;
			}

			Assert.That(calibrator.Gain, Is.EqualTo(expectedGain).Within(0.05f));
			Assert.That(calibrator.GlobalConfidence, Is.GreaterThanOrEqualTo(0.5f));
		}

		[Test]
		public void IntentHandlerFiresSingleImpulseForPeak()
		{
			var intent = new NudgeIntentState(false);

			intent.StepOneMillisecond(new float2(0f, 0f));
			intent.StepOneMillisecond(new float2(0f, -0.5f));
			intent.StepOneMillisecond(new float2(0f, -1.2f));

			Assert.That(intent.IsImpulseInProgress, Is.True);
			intent.StepOneMillisecond(new float2(0f, -1.4f));
			Assert.That(intent.ImpulseAcceleration.y, Is.LessThan(0f));

			// Ride out the rest of the peak and its decay: no second impulse may
			// fire for the same peak (25ms impulse length, then the signal decays).
			var impulseTicks = 0;
			for (var i = 0; i < 200; i++) {
				var signal = -1.4f * math.exp(-i / 30f);
				intent.StepOneMillisecond(new float2(0f, signal));
				if (intent.IsImpulseInProgress) {
					impulseTicks++;
				}
			}
			Assert.That(impulseTicks, Is.LessThanOrEqualTo(25));
		}

		[Test]
		public void GainCalibratorSurvivesLongSegmentsByCompacting()
		{
			var calibrator = MotionGainCalibratorAxis.Default;
			const float expectedGain = 2.0f;
			ulong baseTime = 0;

			// 1.5s segments at 1kHz — way past the sample buffer capacity, forcing
			// resolution compaction. The segment must stay whole (rest-to-rest), so
			// the gain still converges and no segment splitting inflates confidence.
			for (var segment = 0; segment < 10; segment++) {
				calibrator.StartSegment(baseTime);
				var velocity = 0f;
				var previousAcceleration = 0f;
				for (var i = 0; i <= 1500; i++) {
					var t = i / 1500f;
					var acceleration = math.sin(2f * math.PI * 9f * t) * math.sin(math.PI * t);
					if (i > 0) {
						velocity += 0.5f * (previousAcceleration + acceleration) * 0.001f;
					}
					calibrator.AddSample(baseTime + (ulong)(i * 1000), expectedGain * velocity, acceleration);
					previousAcceleration = acceleration;
				}
				calibrator.EndSegment();
				baseTime += 2000000;
			}

			Assert.That(calibrator.AcceptedSegmentCount, Is.LessThanOrEqualTo(10));
			Assert.That(calibrator.Gain, Is.EqualTo(expectedGain).Within(0.15f));
		}

		[Test]
		public void DirectCabinetSensorCanDriveNudgeState()
		{
			var nudge = new NudgeState(KeyboardNudgeMode.CabModel, 1f, 5f);
			nudge.ConfigureSensor(0, new NudgeSensorRuntimeConfig {
				Type = NudgeSensorType.CabinetDirect,
				Strength = 1f,
				CabinetMassKg = 113f,
				AccelerationYMapped = 1
			});

			nudge.ApplySensorSample(0, NudgeSensorChannel.AccelerationY, 0f, 1000);
			nudge.StepOneMillisecond();
			nudge.ApplySensorSample(0, NudgeSensorChannel.AccelerationY, -12f, 2000);
			for (var i = 0; i < 10; i++) {
				nudge.StepOneMillisecond();
			}

			Assert.That(nudge.ActiveSourceIndex, Is.EqualTo(0));
			Assert.That(nudge.CabinetAcceleration.y, Is.LessThan(0f));
			Assert.That(math.abs(nudge.CabinetOffset.y), Is.GreaterThan(0f));
		}

		[Test]
		public void DirectCabinetSensorStrengthScalesLinearly()
		{
			// The reference applies the strength scale twice (upstream bug); VPE
			// deliberately applies it once, so halving strength halves the output.
			float RunWithStrength(float strength)
			{
				var nudge = new NudgeState(KeyboardNudgeMode.CabModel, 1f, 5f);
				nudge.ConfigureSensor(0, new NudgeSensorRuntimeConfig {
					Type = NudgeSensorType.CabinetDirect,
					Strength = strength,
					CabinetMassKg = 113f,
					AccelerationYMapped = 1
				});
				nudge.ApplySensorSample(0, NudgeSensorChannel.AccelerationY, 0f, 1000);
				nudge.StepOneMillisecond();
				nudge.ApplySensorSample(0, NudgeSensorChannel.AccelerationY, -12f, 2000);
				var peak = 0f;
				for (var i = 0; i < 20; i++) {
					nudge.StepOneMillisecond();
					peak = math.min(peak, nudge.CabinetAcceleration.y);
				}
				return peak;
			}

			var fullStrength = RunWithStrength(1f);
			var halfStrength = RunWithStrength(0.5f);

			Assert.That(fullStrength, Is.LessThan(0f));
			Assert.That(halfStrength, Is.EqualTo(fullStrength * 0.5f).Within(math.abs(fullStrength) * 0.02f));
		}
	}
}
