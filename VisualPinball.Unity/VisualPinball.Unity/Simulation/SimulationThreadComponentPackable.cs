// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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

namespace VisualPinball.Unity.Simulation
{
	public struct SimulationThreadComponentPackable
	{
		public bool EnableSimulationThread;
		public bool EnableNativeInput;
		public int InputPollingIntervalUs;
		public bool ShowStatistics;
		public float StatisticsInterval;

		public static byte[] Pack(SimulationThreadComponent comp)
		{
			return PackageApi.Packer.Pack(new SimulationThreadComponentPackable {
				EnableSimulationThread = comp.EnableSimulationThread,
				EnableNativeInput = comp.EnableNativeInput,
				InputPollingIntervalUs = comp.InputPollingIntervalUs,
				ShowStatistics = comp.ShowStatistics,
				StatisticsInterval = comp.StatisticsInterval,
			});
		}

		public static void Unpack(byte[] bytes, SimulationThreadComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SimulationThreadComponentPackable>(bytes);
			comp.EnableSimulationThread = data.EnableSimulationThread;
			comp.EnableNativeInput = data.EnableNativeInput;
			comp.InputPollingIntervalUs = data.InputPollingIntervalUs;
			comp.ShowStatistics = data.ShowStatistics;
			comp.StatisticsInterval = data.StatisticsInterval;
		}
	}
}
