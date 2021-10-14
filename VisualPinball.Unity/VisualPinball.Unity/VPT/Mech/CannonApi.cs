using System;
using Logger = NLog.Logger;
using NLog;
using UnityEngine;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public class CannonApi : IApi, IApiSwitchDevice, IApiCoilDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly CannonComponent _cannonComponent;
		private Player _player;

		private DeviceCoil GunMotorCoil;

		public DeviceSwitch GunHomeSwitch;
		public DeviceSwitch GunMarkSwitch;

		public event EventHandler Init;

		public int position = 0;
		public const int Length = 240;

		internal CannonApi(GameObject go, Player player)
		{
			_cannonComponent = go.GetComponentInChildren<CannonComponent>();
			_cannonComponent.SetAPI(this);
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
			GunMarkSwitch = new DeviceSwitch(CannonComponent.GunMarkSwitchItem, false, SwitchDefault.NormallyOpen, _player);

			UpdatePosition();

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

		public void UpdatePosition(float y = 0)
		{
			position = (int)(math.abs(y / 0.75) * Length);

			Logger.Info($"UpdatePosition: y={y}, position={position}");

			GunHomeSwitch.SetSwitch(position >= 0 && position <= 5);
			GunMarkSwitch.SetSwitch(position >= 98 && position <= 105);
		}

		void IApi.OnDestroy()
		{
			Logger.Info("Destroying cannon!");
		}

		public void OnChange(bool enabled)
		{
			throw new NotImplementedException();
		}
	}
}

