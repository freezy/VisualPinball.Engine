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
	/// Devices switches are switches within a device that are not directly linked to any game item.
	/// </summary>
	[Api]
	public class DeviceSwitch : IApiSwitch
	{
		/// <summary>
		/// Event emitted when the switch opens or closes.
		/// </summary>
		public event EventHandler<SwitchEventArgs> Switch;

		/// <summary>
		/// Indicates whether the switch is currently opened or closed.
		/// </summary>
		public bool IsClosed => _switchHandler.IsClosed;

		/// <summary>
		/// Indicates whether the switch is currently enabled.
		/// </summary>
		///
		/// <remarks>
		/// We sometimes need to check the status of a switch and don't care whether it's an opto switch (which returns
		/// the inverted value) or not.
		/// </remarks>
		public bool IsEnabled => _invertValue ? !IsClosed : IsClosed;

		/// <summary>
		/// If true, *setting* the switch will inverse the given value.
		/// </summary>
		///
		/// <remarks>
		/// This is important for opto switches since the work the other way around.
		/// </remarks>
		private readonly bool _invertValue;

		private readonly bool _isPulseSwitch;
		private readonly SwitchHandler _switchHandler;

		public DeviceSwitch(string name, bool isPulseSwitch, bool isOptoSwitch, Player player)
		{
			_isPulseSwitch = isPulseSwitch;
			_invertValue = isOptoSwitch;
			_switchHandler = new SwitchHandler(name, player, isOptoSwitch);
		}

		public void AddSwitchId(SwitchConfig switchConfig) => _switchHandler.AddSwitchId(switchConfig.WithPulse(_isPulseSwitch));
		public void AddWireDest(WireDestConfig wireConfig) => _switchHandler.AddWireDest(wireConfig);
		public void DestroyBall(Entity ballEntity) { } // device switches can't destroy balls

		public void SetSwitch(bool value)
		{
			var closed = _invertValue ? !value : value;
			_switchHandler.OnSwitch(closed);
			Switch?.Invoke(this, new SwitchEventArgs(closed, Entity.Null));
		}

		public void ScheduleSwitch(bool value, int delay)
		{
			if (delay == 0) {
				SetSwitch(value);
			} else {
				var closed = _invertValue ? !value : value;
				_switchHandler.ScheduleSwitch(closed, delay, c => {
					Switch?.Invoke(this, new SwitchEventArgs(c, Entity.Null));
				});
			}
		}
	}
}
