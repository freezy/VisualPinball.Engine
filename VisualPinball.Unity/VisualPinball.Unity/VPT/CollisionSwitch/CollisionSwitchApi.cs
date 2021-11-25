using System;
using Logger = NLog.Logger;
using NLog;
using UnityEngine;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class CollisionSwitchApi : IApi, IApiSwitch, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly CollisionSwitchComponent _collisionSwitchComponent;
		private Player _player;
		private IApiHittable _hittable;

		private protected readonly SwitchHandler SwitchHandler;

		public event EventHandler Init;
		public event EventHandler<SwitchEventArgs> Switch;

		public bool IsSwitchEnabled => SwitchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig) => SwitchHandler.AddSwitchDest(switchConfig.WithPulse(true));
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => SwitchHandler.AddWireDest(wireConfig.WithPulse(true));
		void IApiSwitch.RemoveWireDest(string destId) => SwitchHandler.RemoveWireDest(destId);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;
		public void OnSwitch(bool closed) => SwitchHandler.OnSwitch(closed);

		public bool IsHittable => _hittable != null;

		internal CollisionSwitchApi(GameObject go, Player player)
		{
			_collisionSwitchComponent = go.GetComponentInChildren<CollisionSwitchComponent>();
			_player = player;

			SwitchHandler = new SwitchHandler(go.name, player);
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
			Switch?.Invoke(this, new SwitchEventArgs(true, e.BallEntity));
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

