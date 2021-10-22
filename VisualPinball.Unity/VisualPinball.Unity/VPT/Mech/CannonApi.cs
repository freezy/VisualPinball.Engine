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
using Logger = NLog.Logger;
using NLog;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class CannonApi : IApi, IApiSwitchDevice, IApiCoilDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public const int Length = 240;

		private enum Direction
		{
			Forward = 0,
			Reverse = 1
		}

		private readonly CannonComponent _cannonComponent;
		private Player _player;

		private DeviceCoil GunMotorCoil;

		public DeviceSwitch GunHomeSwitch;
		public DeviceSwitch GunMarkSwitch;

		public event EventHandler Init;

		private bool _enabled;
		private float _position;
		private Direction _direction;

		internal CannonApi(GameObject go, Player player)
		{
			_cannonComponent = go.GetComponentInChildren<CannonComponent>();
			_player = player;
		}

		IApiCoil IApiCoilDevice.Coil(string deviceItem)
		{
			return deviceItem == CannonComponent.GunMotorCoilItem ? GunMotorCoil : null;
		}

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem)
		{
			if (deviceItem == CannonComponent.GunHomeSwitchItem)
			{
				return GunHomeSwitch;
			}
			else if (deviceItem == CannonComponent.GunMarkSwitchItem)
			{
				return GunMarkSwitch;
			}

			return null;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			_enabled = false;
			_position = 0;
			_direction = Direction.Forward;

			GunMotorCoil = new DeviceCoil(OnGunMotorCoilEnabled, OnGunMotorCoilDisabled);

			GunHomeSwitch = new DeviceSwitch(CannonComponent.GunHomeSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			GunHomeSwitch.SetSwitch(true);

			GunMarkSwitch = new DeviceSwitch(CannonComponent.GunMarkSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			GunMarkSwitch.SetSwitch(false);

			_player.OnUpdate += OnUpdate;

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void OnGunMotorCoilEnabled()
		{
			_enabled = true;

			Logger.Info("OnGunMotorCoilEnabled");
		}

		private void OnGunMotorCoilDisabled()
		{
			_enabled = false;

			Logger.Info("OnGunMotorCoilDisabled");
		}

		private void OnUpdate(object sender, EventArgs eventArgs)
		{
			if (!_enabled)
			{
				return;
			}

			float speed = (Length * 2 / 6.5f) * Time.deltaTime;

			if (_direction == Direction.Forward)
			{
				_position += speed;

				if (_position >= Length)
				{
					_position = Length - (_position - Length);

					_direction = Direction.Reverse;
				}
			}
			else
			{
				_position -= speed;

				if (_position <= 0)
				{
					_position = -_position;

					_direction = Direction.Forward;
				}
			}

			if (_position >= 0 && _position <= 5)
			{
				if (!GunHomeSwitch.IsEnabled)
				{
					GunHomeSwitch.SetSwitch(true);
				}
			}
			else if (GunHomeSwitch.IsEnabled)
			{
				GunHomeSwitch.SetSwitch(false);
			}

			if (_position >= 98 && _position <= 105)
			{
				if (!GunMarkSwitch.IsEnabled)
				{
					GunMarkSwitch.SetSwitch(true);
				}
			}
			else if (GunMarkSwitch.IsEnabled)
			{
				GunMarkSwitch.SetSwitch(false);
			}

			_cannonComponent.UpdateRotation(_position / Length);

			Logger.Debug($"Cannon position={_position}");
		}

		void IApi.OnDestroy()
		{
			_player.OnUpdate -= OnUpdate;

			Logger.Info("Destroying cannon!");
		}
	}
}

