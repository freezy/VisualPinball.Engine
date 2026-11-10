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
	/// Same cases as VP's <c>tests/input/test-plunger-kalman.cpp</c>.
	/// </summary>
	public class PlungerKalmanFilterTests
	{
		[Test]
		public void ResetSetsTheState()
		{
			var filter = PlungerKalmanFilter.Default;
			filter.Reset(0.25f, 0f, 1000);
			Assert.That(filter.Position, Is.EqualTo(0.25f).Within(1e-6f));
			Assert.That(filter.Velocity, Is.EqualTo(0f).Within(1e-6f));
			Assert.That(filter.TimeNs, Is.EqualTo(1000ul));
		}

		[Test]
		public void PredictionAdvancesPositionWithVelocity()
		{
			var filter = PlungerKalmanFilter.Default;
			filter.Reset(0.5f, 2f, 1000000ul);
			filter.PredictTo(2000000ul);
			Assert.That(filter.Position, Is.EqualTo(0.502f).Within(1e-5f));
			Assert.That(filter.TimeNs, Is.EqualTo(2000000ul));

			filter.PredictTo(500000ul);
			Assert.That(filter.Position, Is.EqualTo(0.502f).Within(1e-5f));
			Assert.That(filter.TimeNs, Is.EqualTo(2000000ul));
		}

		[Test]
		public void PositionMeasurementsConvergeToTheMeasuredValue()
		{
			var filter = PlungerKalmanFilter.Default;
			filter.Reset(0f, 0f, 1000000ul);
			for (ulong i = 1; i <= 200; i++) {
				filter.UpdatePosition(1000000ul + i * 1000000ul, 0.5f);
			}
			Assert.That(filter.Position, Is.EqualTo(0.5f).Within(0.025f));
			Assert.That(math.abs(filter.Velocity), Is.LessThan(0.5f));
		}

		[Test]
		public void FirstPositionUpdateAnchorsTheTimeBase()
		{
			var filter = PlungerKalmanFilter.Default;
			filter.UpdatePosition(5000000ul, 0.3f);
			Assert.That(filter.TimeNs, Is.EqualTo(5000000ul));
			Assert.That(filter.Position, Is.GreaterThan(0.1f));
		}

		[Test]
		public void PositionLimitsClampTheEstimate()
		{
			var config = PlungerKalmanFilter.Config.Default;
			config.EnablePositionLimits = true;
			config.MinPosition = 0f;
			config.MaxPosition = 1f;
			var filter = PlungerKalmanFilter.Default;
			filter.SetConfig(config);

			filter.Reset(1.5f, 0f, 0);
			Assert.That(filter.Position, Is.EqualTo(1f).Within(1e-6f));
			filter.Reset(-0.5f, 0f, 0);
			Assert.That(filter.Position, Is.EqualTo(0f).Within(1e-6f));
		}

		[Test]
		public void LateMeasurementsRewindAndReplayTheFilter()
		{
			var filter = PlungerKalmanFilter.Default;
			filter.Reset(0f, 0f, 1000000ul);
			filter.PredictTo(3000000ul);
			filter.UpdatePosition(2000000ul, 1f);
			Assert.That(filter.TimeNs, Is.EqualTo(3000000ul));
			Assert.That(filter.Position, Is.GreaterThan(0.5f));
			Assert.That(filter.Position, Is.LessThanOrEqualTo(1.05f));
		}

		[Test]
		public void LateMeasurementMatchesChronologicalOrder()
		{
			var chronological = PlungerKalmanFilter.Default;
			chronological.Reset(0f, 0f, 1000000ul);
			chronological.UpdatePosition(2000000ul, 0.4f);
			chronological.UpdatePosition(3000000ul, 0.6f);
			chronological.PredictTo(6000000ul);

			var delayed = PlungerKalmanFilter.Default;
			delayed.Reset(0f, 0f, 1000000ul);
			delayed.UpdatePosition(2000000ul, 0.4f);
			delayed.PredictTo(6000000ul);
			delayed.UpdatePosition(3000000ul, 0.6f);

			Assert.That(delayed.TimeNs, Is.EqualTo(chronological.TimeNs));
			Assert.That(delayed.Position, Is.EqualTo(chronological.Position).Within(1e-4f));
			Assert.That(delayed.Velocity, Is.EqualTo(chronological.Velocity).Within(1e-3f));
		}

		[Test]
		public void VelocityZeroPseudoMeasurementDampsTheVelocity()
		{
			var filter = PlungerKalmanFilter.Default;
			filter.Reset(0.5f, 2f, 1000000ul);
			filter.UpdateVelocityZero(0.01f);
			Assert.That(math.abs(filter.Velocity), Is.LessThan(2f));
		}

		[Test]
		public void NonFiniteMeasurementsAreIgnored()
		{
			var filter = PlungerKalmanFilter.Default;
			filter.Reset(0.5f, 0f, 1000000ul);
			filter.UpdatePosition(2000000ul, float.NaN);
			Assert.That(filter.Position, Is.EqualTo(0.5f).Within(1e-6f));
			filter.UpdatePosition(2000000ul, float.PositiveInfinity);
			Assert.That(filter.Position, Is.EqualTo(0.5f).Within(1e-6f));
		}
	}
}
