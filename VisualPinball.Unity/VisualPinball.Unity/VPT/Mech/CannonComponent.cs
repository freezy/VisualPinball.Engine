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
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private enum Direction
        {
            CounterClockwise = 0,
            Clockwise = 1
        }

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
       
        public event EventHandler Init;
        public bool IsEnabled = false;
        private CannonApi _api;
        private Direction _direction = Direction.CounterClockwise;

        public void SetAPI(CannonApi api)
		{
            _api = api;
		}

        void Update()
        {
            if (!IsEnabled)
			{
                return;
			}

            var rotation = transform.rotation;

            if (_direction == Direction.CounterClockwise)
            {
                rotation.y -= 0.02f;

                if (rotation.y <= -.75)
                {
                    _direction = Direction.Clockwise;
                }
            }
            else if (_direction == Direction.Clockwise)
            {
                rotation.y += 0.02f;

                if (rotation.y >= 0)
                {
                    _direction = Direction.CounterClockwise;
                }
            }

            _api.UpdatePosition(rotation.y);

            transform.rotation = rotation;
        }

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
	}
}
