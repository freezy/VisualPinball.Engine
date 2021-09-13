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
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	public class TriggerApi : CollidableApi<TriggerComponent, TriggerColliderComponent, TriggerData>,
		IApi, IApiHittable, IApiSwitch, IApiSwitchDevice
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball glides on the trigger.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		/// <summary>
		/// Event emitted when the ball leaves the trigger.
		/// </summary>
		public event EventHandler<HitEventArgs> UnHit;

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		public event EventHandler<SwitchEventArgs> Switch;

		internal TriggerApi(GameObject go, Entity entity, Entity parentEntity, Player player)
			: base(go, entity, parentEntity, player)
		{
		}

		#region Wiring

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;

		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig) => AddSwitchDest(switchConfig);
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig);
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);
		void IApiSwitch.DestroyBall(Entity ballEntity) => DestroyBall(ballEntity);

		#endregion

		#region Collider Generation

		protected override bool FireHitEvents => true;

		protected override void CreateColliders(List<ICollider> colliders, float margin)
		{
			var meshComponent = GameObject.GetComponent<TriggerMeshComponent>();
			if (meshComponent) {
				var colliderGenerator = new TriggerColliderGenerator(this, MainComponent, ColliderComponent, meshComponent);
				colliderGenerator.GenerateColliders(colliders);
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

		void IApiHittable.OnHit(Entity ballEntity, bool isUnHit)
		{
			if (isUnHit) {
				UnHit?.Invoke(this, new HitEventArgs(ballEntity));
				Switch?.Invoke(this, new SwitchEventArgs(false, ballEntity));
				OnSwitch(false);

			} else {
				Hit?.Invoke(this, new HitEventArgs(ballEntity));
				Switch?.Invoke(this, new SwitchEventArgs(true, ballEntity));
				OnSwitch(true);
			}
		}

		#endregion

	}
}
