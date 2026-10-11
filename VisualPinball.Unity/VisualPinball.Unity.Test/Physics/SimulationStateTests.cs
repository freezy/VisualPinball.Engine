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
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	public class SimulationStateTests
	{
		[Test]
		public void AcquireReturnsTheLatestPublishedSnapshot()
		{
			using var state = new SimulationState();
			Publish(state, 1000);
			Publish(state, 2000);

			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(2000));
		}

		[Test]
		public void AcquireWithoutNewPublishKeepsTheSameSnapshot()
		{
			using var state = new SimulationState();
			Publish(state, 1000);
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(1000));
			Publish(state, 2000);
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(2000));

			// nothing published since: must not fall back to the older buffer
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(2000));
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(2000));

			Publish(state, 3000);
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(3000));
		}

		private static void Publish(SimulationState state, long simulationTimeUsec)
		{
			ref var buffer = ref state.GetWriteBuffer();
			buffer.SimulationTimeUsec = simulationTimeUsec;
			state.PublishWriteBuffer();
		}
	}
}
