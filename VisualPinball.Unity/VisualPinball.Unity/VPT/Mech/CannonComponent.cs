using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;
using NLog;
using System;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Cannon")]
	public class CannonComponent : MonoBehaviour, ISwitchDeviceComponent, ICoilDeviceComponent
	{
		public delegate float OnGunMotorUpdatePositionHandler(float delta);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public const string GunMotorCoilItem = "gun_motor_coil";

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(GunMotorCoilItem) {
				Description = "Gun Motor"
			}
		};

		public const string GunMarkSwitchItem = "gun_mark_switch";
		public const string GunHomeSwitchItem = "gun_home_switch";

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(GunMarkSwitchItem) {
				Description = "Gun Mark"
			},
			 new GamelogicEngineSwitch(GunHomeSwitchItem) {
				Description = "Gun Home"
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		public event OnGunMotorUpdatePositionHandler OnGunMotorUpdatePosition;

		public bool IsEnabled = false;

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null)
			{
				Logger.Error($"Cannot find player for cannon {name}.");
				return;
			}

			player.RegisterMech(this);
		}

		private void Update()
		{
			if (!IsEnabled) return;

			float position = OnGunMotorUpdatePosition(Time.deltaTime);

			var rotation = transform.rotation;
			rotation.y = -(position * 0.65f);

			transform.rotation = rotation;
		}
	}
}
