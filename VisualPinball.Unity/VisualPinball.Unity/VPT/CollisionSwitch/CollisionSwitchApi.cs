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
		private IColliderComponent _colliderComponent;
		private Player _player;
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
		
		internal CollisionSwitchApi(GameObject go, Player player)
		{
			_collisionSwitchComponent = go.GetComponentInChildren<CollisionSwitchComponent>();
			_player = player;

			SwitchHandler = new SwitchHandler(go.name, player);
		}

		void IApi.OnInit(BallManager ballManager)
		{
			_ballManager = ballManager;

			_colliderComponent = _collisionSwitchComponent.GetComponentInParent<IColliderComponent>();

			if (_colliderComponent is SurfaceColliderComponent) {
				_player.TableApi.Surface(_collisionSwitchComponent.name).Hit += OnHit;
			}
			else if (_colliderComponent is RubberColliderComponent) {
				_player.TableApi.Rubber(_collisionSwitchComponent.name).Hit += OnHit;
			}
			else if (_colliderComponent is PrimitiveColliderComponent) {
				_player.TableApi.Primitive(_collisionSwitchComponent.name).Hit += OnHit;
			}
			else {
				Logger.Error($"{_collisionSwitchComponent.name} not connected to a surface, rubber, or primitive");
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

			if (_colliderComponent is SurfaceColliderComponent) {
				_player.TableApi.Surface(_collisionSwitchComponent.name).Hit -= OnHit;
			}
			else if (_colliderComponent is RubberColliderComponent) {
				_player.TableApi.Rubber(_collisionSwitchComponent.name).Hit -= OnHit;
			}
			else if (_colliderComponent is PrimitiveColliderComponent) {
				_player.TableApi.Primitive(_collisionSwitchComponent.name).Hit -= OnHit;
			}
		}
	}
}

