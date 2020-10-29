// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using System.Collections.Generic;
using NLog;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class DeviceSwitch : IApiSwitch
	{
		private readonly bool _isPulseSwitch;
		private List<SwitchConfig> _engineSwitchIds;
		private readonly IGamelogicEngineWithSwitches _engine;
		private static VisualPinballSimulationSystemGroup SimulationSystemGroup => World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public DeviceSwitch(bool isPulseSwitch, IGamelogicEngineWithSwitches engine)
		{
			_isPulseSwitch = isPulseSwitch;
			_engine = engine;
		}

		public void AddSwitchId(string switchId, int pulseDelay)
		{
			if (_engineSwitchIds == null) {
				_engineSwitchIds = new List<SwitchConfig>();
			}

			_engineSwitchIds.Add(new SwitchConfig(switchId, _isPulseSwitch, pulseDelay));
		}

		public void OnSwitch(bool normallyClosed)
		{
			if (_engine != null && _engineSwitchIds != null) {
				foreach (var switchConfig in _engineSwitchIds) {
					_engine.Switch(switchConfig.SwitchId, normallyClosed);

					// time switch opening if closed and pulse
					if (normallyClosed && switchConfig.IsPulseSwitch) {
						SimulationSystemGroup.ScheduleSwitch(switchConfig.PulseDelay,
							() => _engine.Switch(switchConfig.SwitchId, false));
					}
				}
			} else {
				Logger.Warn("Cannot trigger device switch.");
			}
		}
	}
}
