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
		private Player _player;
		private SurfaceApi _surfaceApi;
		private BallManager _ballManager;

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

		internal SlingshotApi(GameObject go, Player player)
		{
			_slingshotComponent = go.GetComponentInChildren<SlingshotComponent>();
			_player = player;

			SwitchHandler = new SwitchHandler(go.name, player);
		}

		void IApi.OnInit(BallManager ballManager)
		{
			_ballManager = ballManager;
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

