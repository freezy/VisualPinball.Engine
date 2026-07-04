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
	/// <summary>
	/// Covers cabinet spring, keyboard nudge, and plumb-bob physics primitives.
	/// </summary>
	public class CabinetPhysicsTests
	{
		[Test]
		public void OscillatorTracksAnalyticStepResponse()
		{
			const float mass = 113f;
			const float frequencyHz = 9.3f;
			const float dampingRatio = 0.052f;
			const float targetAcceleration = 12f;
			var oscillator = new DampedHarmonicOscillator(mass, frequencyHz, dampingRatio);
			var force = mass * targetAcceleration;

			for (var i = 0; i < 50; i++) {
				oscillator.StepOneMillisecond(force);
			}

			var expected = AnalyticStepResponse(mass, frequencyHz, dampingRatio, force, 0.050f);
			Assert.That(oscillator.Displacement, Is.EqualTo(expected.Displacement).Within(0.0008f));
			Assert.That(oscillator.Velocity, Is.EqualTo(expected.Velocity).Within(0.03f));
			Assert.That(oscillator.Acceleration, Is.EqualTo(expected.Acceleration).Within(0.7f));
		}

		[Test]
		public void CabinetPhysicsUsesCalibratedAxisModels()
		{
			var cabinet = CabinetPhysicsState.Default;
			cabinet.StepOneMillisecond(new float2(cabinet.Mass * 12f, cabinet.Mass * -10f));

			Assert.That(cabinet.CabinetAcceleration.x, Is.EqualTo(12f).Within(1e-5f));
			Assert.That(cabinet.CabinetAcceleration.y, Is.EqualTo(-10f).Within(1e-5f));
			Assert.That(cabinet.CabinetOffset.x, Is.GreaterThan(0f));
			Assert.That(cabinet.CabinetOffset.y, Is.LessThan(0f));
		}

		[Test]
		public void KeyboardCabinetModelLimitsVisibleRebounds()
		{
			var nudge = new KeyboardNudgeState(KeyboardNudgeMode.CabModel, 1f, 5f);
			nudge.Nudge(0f, 2f);

			var offsets = new float[1000];
			var peak = 0f;
			for (var i = 0; i < offsets.Length; i++) {
				nudge.StepOneMillisecond();
				offsets[i] = nudge.CabinetOffset.y;
				peak = math.max(peak, math.abs(offsets[i]));
			}

			Assert.That(CountVisibleExtrema(offsets, peak * 0.1f), Is.LessThanOrEqualTo(3));
		}

		[Test]
		public void PlumbTiltLatchesAndQueuesTiltEvent()
		{
			var plumb = new PlumbState(true, 1f, 2f);

			for (var i = 0; i < 1000 && !plumb.TiltHigh; i++) {
				plumb.StepOneMillisecond(new float2(80f, 0f));
			}

			Assert.That(plumb.TiltHigh, Is.True);
			Assert.That(plumb.TiltIndex, Is.EqualTo(1));
			Assert.That(plumb.PendingTiltStates.Length, Is.EqualTo(1));
			Assert.That(plumb.PendingTiltStates[0], Is.EqualTo(1));

			var status = plumb.ReadAndResetTiltStatus();
			Assert.That(math.abs(status.x), Is.GreaterThan(0f));
			Assert.That(status.z, Is.GreaterThan(100f));

			var resetStatus = plumb.ReadAndResetTiltStatus();
			Assert.That(resetStatus.x, Is.EqualTo(0f));
			Assert.That(resetStatus.y, Is.EqualTo(0f));
			Assert.That(resetStatus.z, Is.EqualTo(0f));

			plumb.ClearPendingTiltEvents();
			Assert.That(plumb.PendingTiltStates.Length, Is.EqualTo(0));
		}

		/// <summary>
		/// Computes the closed-form underdamped oscillator response used to verify
		/// the one-millisecond integrator.
		/// </summary>
		private static (float Displacement, float Velocity, float Acceleration) AnalyticStepResponse(float mass, float frequencyHz, float dampingRatio, float force, float time)
		{
			var omega0 = 2f * math.PI * frequencyHz;
			var omegaD = omega0 * math.sqrt(1f - dampingRatio * dampingRatio);
			var alpha = dampingRatio * omega0;
			var steadyState = force / (mass * omega0 * omega0);
			var decay = math.exp(-alpha * time);
			var displacement = steadyState * (1f - decay * (math.cos(omegaD * time) + alpha / omegaD * math.sin(omegaD * time)));
			var velocity = steadyState * decay * (omega0 * omega0 / omegaD) * math.sin(omegaD * time);
			var acceleration = (force - 2f * dampingRatio * mass * omega0 * velocity - mass * omega0 * omega0 * displacement) / mass;

			return (displacement, velocity, acceleration);
		}

		/// <summary>
		/// Counts visible rebound peaks after ignoring small tail oscillations.
		/// </summary>
		private static int CountVisibleExtrema(float[] values, float threshold)
		{
			var count = 0;
			for (var i = 2; i < values.Length; i++) {
				var previousSlope = values[i - 1] - values[i - 2];
				var slope = values[i] - values[i - 1];
				if (math.abs(values[i - 1]) <= threshold || previousSlope == 0f || slope == 0f) {
					continue;
				}
				if (math.sign(previousSlope) != math.sign(slope)) {
					count++;
				}
			}
			return count;
		}
	}
}
