// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The triple buffer between the simulation thread and the main thread never hands the
	/// main thread a snapshot older than the one it already has.
	/// </summary>
	public class SimulationStateTests
	{
		[Test]
		public void AcquireWithoutNewPublicationKeepsTheCurrentSnapshot()
		{
			using var state = new SimulationState();
			Publish(state, 1000);
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(1000));

			// no new publication: before the fix, this swapped back to the older buffer
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(1000));
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(1000));
		}

		[Test]
		public void AcquireTakesTheNewestPublication()
		{
			using var state = new SimulationState();
			Publish(state, 1000);
			Publish(state, 2000);
			Publish(state, 3000);
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(3000));

			Publish(state, 4000);
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(4000));
			Assert.That(state.AcquireReadBuffer().SimulationTimeUsec, Is.EqualTo(4000));
		}

		[Test]
		public void SequenceGrowsWithEveryPublication()
		{
			using var state = new SimulationState();
			Publish(state, 1000);
			var first = state.AcquireReadBuffer().PublishSequence;
			Publish(state, 2000);
			var second = state.AcquireReadBuffer().PublishSequence;
			Assert.That(first, Is.GreaterThan(0));
			Assert.That(second, Is.GreaterThan(first));
		}

		[Test]
		public void NothingPublishedYieldsAnEmptySnapshot()
		{
			using var state = new SimulationState();
			Assert.That(state.AcquireReadBuffer().PublishSequence, Is.EqualTo(0));
		}

		private static void Publish(SimulationState state, long simulationTimeUsec)
		{
			state.GetWriteBuffer().SimulationTimeUsec = simulationTimeUsec;
			state.PublishWriteBuffer();
		}
	}
}
