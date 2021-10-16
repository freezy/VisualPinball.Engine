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

		private Direction _direction;
		private float _position;

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
			GunMotorCoil = new DeviceCoil(OnGunMotorCoilEnabled, OnGunMotorCoilDisabled);

			GunHomeSwitch = new DeviceSwitch(CannonComponent.GunHomeSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			GunHomeSwitch.SetSwitch(true);

			GunMarkSwitch = new DeviceSwitch(CannonComponent.GunMarkSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			GunMarkSwitch.SetSwitch(false);

			_direction = Direction.Forward;
			_position = 0;

			_cannonComponent.OnGunMotorUpdatePosition += OnGunMotorUpdatePosition;

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void OnGunMotorCoilEnabled()
		{
			Logger.Info("OnGunMotorCoilEnabled");

			_cannonComponent.IsEnabled = true;
		}

		private void OnGunMotorCoilDisabled()
		{
			_cannonComponent.IsEnabled = false;

			Logger.Info("OnGunMotorCoilDisabled");
		}

		public float OnGunMotorUpdatePosition(float delta)
		{
			float speed = (Length * 2 / 6.5f) * delta;

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

			Logger.Info($"Position={_position}");

			return _position / Length;
		}

		void IApi.OnDestroy()
		{
			Logger.Info("Destroying cannon api!");

			_cannonComponent.OnGunMotorUpdatePosition -= OnGunMotorUpdatePosition;
		}
	}
}

