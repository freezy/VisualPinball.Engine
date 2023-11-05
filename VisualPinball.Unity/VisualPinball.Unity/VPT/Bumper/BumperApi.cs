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
using UnityEngine;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	public class BumperApi : CollidableApi<BumperComponent, BumperColliderComponent, BumperData>,
		IApi, IApiHittable, IApiSwitchDevice, IApiSwitch, IApiCoil, IApiCoilDevice, IApiWireDeviceDest
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the bumper.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		public event EventHandler<SwitchEventArgs> Switch;

		public BumperApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
		}

		#region Wiring

		public bool IsSwitchEnabled => SwitchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus) => AddSwitchDest(switchConfig.WithPulse(true), switchStatus);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => this;
		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => this;

		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig.WithPulse(true));
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);
		void IApiCoil.OnCoil(bool enabled)
		{
			if (!enabled) {
				return;
			}
			ref var bumperState = ref PhysicsEngine.BumperState(ItemId);
			bumperState.RingAnimation.IsHit = true;
		}

		void IApiWireDest.OnChange(bool enabled) => (this as IApiCoil).OnCoil(enabled);

		#endregion

		#region Collider Generation

		protected override bool FireHitEvents => ColliderComponent.HitEvent;
		protected override float HitThreshold => ColliderComponent.Threshold;

		protected override void CreateColliders(ref ColliderReference colliders,
			ref ColliderReference kinematicColliders, float margin)
		{
			var height = MainComponent.PositionZ;
			if (ColliderComponent.IsKinematic) {
				kinematicColliders.Add(new CircleCollider(MainComponent.Position, MainComponent.Radius, height,
					height + MainComponent.HeightScale, GetColliderInfo(), ColliderType.Bumper));
			} else {
				colliders.Add(new CircleCollider(MainComponent.Position, MainComponent.Radius, height,
					height + MainComponent.HeightScale, GetColliderInfo(), ColliderType.Bumper));
			}
		}

		#endregion

		#region Events

		void IApi.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}

		void IApiHittable.OnHit(int ballId, bool isUnHit)
		{
			Hit?.Invoke(this, new HitEventArgs(ballId));
			Switch?.Invoke(this, new SwitchEventArgs(!isUnHit, ballId));
			OnSwitch(true);
		}

		#endregion
	}
}
