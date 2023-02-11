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
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class CollisionSwitchApi : IApi, IApiSwitch, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly CollisionSwitchComponent _collisionSwitchComponent;
		private readonly Player _player;
		private IApiHittable _hittable;

		private readonly SwitchHandler _switchHandler;

		public event EventHandler Init;
		public event EventHandler<SwitchEventArgs> Switch;

		public bool IsSwitchEnabled => _switchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus) => _switchHandler.AddSwitchDest(switchConfig.WithPulse(true), switchStatus);
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => _switchHandler.AddWireDest(wireConfig.WithPulse(true));
		void IApiSwitch.RemoveWireDest(string destId) => _switchHandler.RemoveWireDest(destId);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;
		public void OnSwitch(bool closed) => _switchHandler.OnSwitch(closed);

		public bool IsHittable => _hittable != null;

		internal CollisionSwitchApi(GameObject go, Player player)
		{
			_collisionSwitchComponent = go.GetComponentInChildren<CollisionSwitchComponent>();
			_player = player;

			_switchHandler = new SwitchHandler(go.name, player);
		}

		void IApi.OnInit(BallManager ballManager)
		{
			_hittable = _player.TableApi.Hittable(_collisionSwitchComponent.GetComponentInParent<MonoBehaviour>());

			if (_hittable != null) {
				_hittable.Hit += OnHit;
			}
			else {
				Logger.Error($"{_collisionSwitchComponent.name} not connected to a hittable component");
			}

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void OnHit(object sender, HitEventArgs e)
		{
			Switch?.Invoke(this, new SwitchEventArgs(true, e.BallId));
			OnSwitch(true);
		}

		void IApi.OnDestroy()
		{
			Logger.Info($"Destroying {_collisionSwitchComponent.name}");

			if (_hittable != null) {
				_hittable.Hit -= OnHit;
			}
		}
	}
}

