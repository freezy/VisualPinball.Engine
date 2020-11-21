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

using System;
using Unity.Entities;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Devices switches are switches withing a device that are not directly
	/// linked to any game item.
	/// </summary>
	public class DeviceSwitch : IApiSwitch
	{
		private readonly bool _isPulseSwitch;
		private readonly SwitchHandler _switchHandler;
		public event EventHandler<SwitchEventArgs> Switch;

		public DeviceSwitch(bool isPulseSwitch, IGamelogicEngineWithSwitches engine, Player player)
		{
			_isPulseSwitch = isPulseSwitch;
			_switchHandler = new SwitchHandler(player, engine);
		}

		public void AddSwitchId(SwitchConfig switchConfig) => _switchHandler.AddSwitchId(switchConfig.WithPulse(_isPulseSwitch));

		public void AddWireDest(WireDestConfig wireConfig) => _switchHandler.AddWireDest(wireConfig);

		public void SetSwitch(bool closed)
		{
			_switchHandler.OnSwitch(closed);
			Switch?.Invoke(this, new SwitchEventArgs(closed, Entity.Null));
		}

		public void ScheduleSwitch(bool closed, int delay) => _switchHandler.ScheduleSwitch(closed, delay);
	}
}
