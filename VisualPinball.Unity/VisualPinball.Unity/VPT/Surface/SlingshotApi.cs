// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using Logger = NLog.Logger;
using NLog;
using UnityEngine;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class SlingshotApi : IApi, IApiSwitch, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly SlingshotComponent _slingshotComponent;
		private readonly Player _player;
		private SurfaceApi _surfaceApi;

		private readonly SwitchHandler _switchHandler;

		public event EventHandler Init;
		public event EventHandler<SwitchEventArgs> Switch;

		public bool IsSwitchEnabled => _switchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus) => _switchHandler.AddSwitchDest(switchConfig.WithPulse(true), switchStatus);
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => _switchHandler.AddWireDest(wireConfig.WithPulse(true));
		void IApiSwitch.RemoveWireDest(string destId) => _switchHandler.RemoveWireDest(destId);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;

		private void OnSwitch(bool closed) => _switchHandler.OnSwitch(closed);

		internal SlingshotApi(GameObject go, Player player)
		{
			_slingshotComponent = go.GetComponentInChildren<SlingshotComponent>();
			_player = player;

			_switchHandler = new SwitchHandler(go.name, player);
		}

		void IApi.OnInit(BallManager ballManager)
		{
			_surfaceApi = _player.TableApi.Surface(_slingshotComponent.SlingshotSurface.MainComponent);

			if (_surfaceApi != null) {
				_surfaceApi.Slingshot += OnSlingshot;
			}

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void OnSlingshot(object sender, EventArgs e)
		{
			Switch?.Invoke(this, new SwitchEventArgs(true, Entity.Null));
			OnSwitch(true);
		}

		void IApi.OnDestroy()
		{
			Logger.Info($"Destroying {_slingshotComponent.name}");

			if (_surfaceApi != null) {
				_surfaceApi.Slingshot -= OnSlingshot;
			}
		}
	}
}

