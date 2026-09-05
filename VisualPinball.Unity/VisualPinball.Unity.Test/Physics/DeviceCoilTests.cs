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
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class DeviceCoilTests
	{
		[Test]
		public void ShouldPublishStatusForASimulationThreadDispatchedTransition()
		{
			var mainThreadEnables = 0;
			var simulationThreadEnables = 0;
			var statusEvents = new List<bool>();
			var coil = new DeviceCoil(null,
				onEnable: () => mainThreadEnables++,
				onEnableSimulationThread: () => simulationThreadEnables++);
			coil.CoilStatusChanged += (_, args) => statusEvents.Add(args.IsEnergized);

			// native poll on the simulation thread, then the managed mirror of the same press
			coil.OnCoilSimulationThread(1f);
			coil.PublishSimulationThreadDispatchedState(true);

			Assert.That(simulationThreadEnables, Is.EqualTo(1));
			Assert.That(mainThreadEnables, Is.Zero, "the physics effect belongs to the simulation thread");
			Assert.That(coil.IsEnabled, Is.True);
			Assert.That(statusEvents, Is.EqualTo(new[] { true }),
				"listeners such as coil sounds and animations still see the transition");

			// the managed mirror may also arrive first; it must not fire physics twice
			coil.PublishSimulationThreadDispatchedState(false);
			coil.OnCoilSimulationThread(0f);
			coil.OnCoil(true);

			Assert.That(mainThreadEnables, Is.Zero, "a later main-thread call keeps its physics suppressed");
			Assert.That(statusEvents, Is.EqualTo(new[] { true, false, true }));
		}
	}
}
