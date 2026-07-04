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
	public struct SimulationThreadNudgeSensorPackable
	{
		public NudgeSensorType Type;
		public float Strength;
		public float CabinetMassKg;
		public string X;
		public string Y;
		public string AccelerationX;
		public string AccelerationY;
		public string VelocityX;
		public string VelocityY;

		public static SimulationThreadNudgeSensorPackable Pack(SimulationThreadNudgeSensorConfig sensor)
		{
			sensor ??= new SimulationThreadNudgeSensorConfig();
			sensor.Normalize();
			return new SimulationThreadNudgeSensorPackable {
				Type = sensor.Type,
				Strength = sensor.Strength,
				CabinetMassKg = sensor.CabinetMassKg,
				X = sensor.X,
				Y = sensor.Y,
				AccelerationX = sensor.AccelerationX,
				AccelerationY = sensor.AccelerationY,
				VelocityX = sensor.VelocityX,
				VelocityY = sensor.VelocityY
			};
		}

		public SimulationThreadNudgeSensorConfig Unpack()
		{
			return new SimulationThreadNudgeSensorConfig {
				Type = Type,
				Strength = Strength,
				CabinetMassKg = CabinetMassKg,
				X = X,
				Y = Y,
				AccelerationX = AccelerationX,
				AccelerationY = AccelerationY,
				VelocityX = VelocityX,
				VelocityY = VelocityY
			};
		}
	}

	public struct SimulationThreadComponentPackable
	{
		public bool EnableSimulationThread;
		public bool EnableNativeInput;
		public int InputPollingIntervalUs;
		public bool HasNudgeSensors;
		public SimulationThreadNudgeSensorPackable[] NudgeSensors;
		public bool ShowStatistics;
		public float StatisticsInterval;

		public static byte[] Pack(SimulationThreadComponent comp)
		{
			return PackageApi.Packer.Pack(new SimulationThreadComponentPackable {
				EnableSimulationThread = comp.EnableSimulationThread,
				EnableNativeInput = comp.EnableNativeInput,
				InputPollingIntervalUs = comp.InputPollingIntervalUs,
				HasNudgeSensors = true,
				NudgeSensors = PackNudgeSensors(comp),
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
			if (data.HasNudgeSensors) {
				comp.NudgeSensors ??= new System.Collections.Generic.List<SimulationThreadNudgeSensorConfig>();
				comp.NudgeSensors.Clear();
				if (data.NudgeSensors != null) {
					for (var i = 0; i < data.NudgeSensors.Length && i < NudgeState.MaxSensors; i++) {
						comp.NudgeSensors.Add(data.NudgeSensors[i].Unpack());
					}
				}
			}
			comp.ShowStatistics = data.ShowStatistics;
			comp.StatisticsInterval = data.StatisticsInterval;
		}

		private static SimulationThreadNudgeSensorPackable[] PackNudgeSensors(SimulationThreadComponent comp)
		{
			if (comp.NudgeSensors == null || comp.NudgeSensors.Count == 0) {
				return null;
			}
			var count = comp.NudgeSensors.Count > NudgeState.MaxSensors ? NudgeState.MaxSensors : comp.NudgeSensors.Count;
			var result = new SimulationThreadNudgeSensorPackable[count];
			for (var i = 0; i < count; i++) {
				result[i] = SimulationThreadNudgeSensorPackable.Pack(comp.NudgeSensors[i]);
			}
			return result;
		}
	}
}
