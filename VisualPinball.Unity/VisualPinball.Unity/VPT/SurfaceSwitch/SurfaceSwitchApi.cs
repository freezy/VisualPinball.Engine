using System;
using Logger = NLog.Logger;
using NLog;
using UnityEngine;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public class SurfaceSwitchApi : IApi, IApiSwitch, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly SurfaceSwitchComponent _surfaceSwitchComponent;
		private Player _player;
		private BallManager _ballManager;

		private SurfaceApi _surfaceApi;

		private protected readonly SwitchHandler SwitchHandler;

		public event EventHandler Init;
		public event EventHandler<SwitchEventArgs> Switch;

		public bool IsSwitchEnabled => SwitchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig) => SwitchHandler.AddSwitchDest(switchConfig.WithPulse(true));
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => SwitchHandler.AddWireDest(wireConfig.WithPulse(true));
		void IApiSwitch.RemoveWireDest(string destId) => SwitchHandler.RemoveWireDest(destId);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;
		public void OnSwitch(bool closed) => SwitchHandler.OnSwitch(closed);
		public void DestroyBall(Entity ballEntity) => _ballManager.DestroyEntity(ballEntity);
		
		internal SurfaceSwitchApi(GameObject go, Player player)
		{
			_surfaceSwitchComponent = go.GetComponentInChildren<SurfaceSwitchComponent>();
			_player = player;

			SwitchHandler = new SwitchHandler(go.name, player);
		}

		void IApi.OnInit(BallManager ballManager)
		{
			_ballManager = ballManager;

			_surfaceApi = _player.TableApi.Surface(_surfaceSwitchComponent.name);

			if (_surfaceApi != null) {
				_surfaceApi.Hit += OnHit;
			}
			else {
				Logger.Error($"{_surfaceSwitchComponent.name} not connected to a surface");
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
			Logger.Info($"Destroying {_surfaceSwitchComponent.name}");

			_surfaceApi.Hit -= OnHit;
		}
	}
}
