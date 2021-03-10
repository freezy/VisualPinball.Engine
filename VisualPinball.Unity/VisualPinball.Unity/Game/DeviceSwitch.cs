// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
		public bool IsEnabled => _switchHandler.IsEnabled;

		/// <summary>
		/// Guesses whether the switch is closed or not.
		/// </summary>
		///
		/// <remarks>
		/// This is used in the trough inspector to render the switch states. We "guess", because
		/// in case the switch default is configurable, we don't actually know, because then it depends
		/// on each individual mapping.
		/// </remarks>
		public bool IsSwitchClosed => _switchDefault == SwitchDefault.NormallyClosed ? !IsEnabled : IsEnabled;

		private readonly bool _isPulseSwitch;
		private readonly SwitchDefault _switchDefault;
		private readonly SwitchHandler _switchHandler;

		public DeviceSwitch(string name, bool isPulseSwitch, SwitchDefault switchDefault, Player player)
		{
			_isPulseSwitch = isPulseSwitch;
			_switchDefault = switchDefault;
			_switchHandler = new SwitchHandler(name, player);
		}

		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig) =>
			_switchHandler.AddSwitchDest(switchConfig.WithPulse(_isPulseSwitch).WithDefault(_switchDefault));
		public void AddWireDest(WireDestConfig wireConfig) => _switchHandler.AddWireDest(wireConfig);
		void IApiSwitch.RemoveWireDest(string destId) => _switchHandler.RemoveWireDest(destId);
		public void DestroyBall(Entity ballEntity) { } // device switches can't destroy balls

		/// <summary>
		/// Enables or disables the switch.
		/// </summary>
		/// <param name="enabled">If true, closes mechanical switch or opens opto switch. If false, opens mechanical switch or closes opto switch.</param>
		public void SetSwitch(bool enabled)
		{
			_switchHandler.OnSwitch(enabled);
			Switch?.Invoke(this, new SwitchEventArgs(enabled, Entity.Null));
		}

		/// <summary>
		/// Schedules the switch to be enabled or disabled.
		/// </summary>
		/// <param name="enabled">If true, closes mechanical switch or opens opto switch. If false, opens mechanical switch or closes opto switch.</param>
		/// <param name="delay">Delay in milliseconds</param>
		public void ScheduleSwitch(bool enabled, int delay)
		{
			if (delay == 0) {
				SetSwitch(enabled);
			} else {
				_switchHandler.ScheduleSwitch(enabled, delay, isEnabled => {
					Switch?.Invoke(this, new SwitchEventArgs(isEnabled, Entity.Null));
				});
			}
		}
	}

	public enum SwitchDefault
	{
		Configurable, NormallyClosed, NormallyOpen
	}
}
