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

using System;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Runtime switch device for the table tilt-bob component.
	/// </summary>
	internal sealed class TiltBobApi : IApi, IApiSwitch, IApiSwitchDevice
	{
		private readonly SwitchHandler _switchHandler;

		public event EventHandler Init;
		public event EventHandler<SwitchEventArgs> Switch;

		public bool IsSwitchEnabled => _switchHandler.IsEnabled;

		public TiltBobApi(GameObject gameObject, Player player, PhysicsEngine physicsEngine)
		{
			_switchHandler = new SwitchHandler(gameObject.name, player, physicsEngine);
		}

		public void SetSwitch(bool enabled)
		{
			_switchHandler.OnSwitch(enabled);
			Switch?.Invoke(this, new SwitchEventArgs(enabled));
		}

		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus) =>
			_switchHandler.AddSwitchDest(switchConfig.WithPulse(false).WithDefault(SwitchDefault.NormallyOpen), switchStatus);

		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => _switchHandler.AddWireDest(wireConfig.WithPulse(false));

		void IApiSwitch.RemoveWireDest(string destId) => _switchHandler.RemoveWireDest(destId);

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) =>
			deviceItem == TiltBobComponent.SwitchItem ? this : null;

		void IApi.OnInit(BallManager ballManager)
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}
	}
}
