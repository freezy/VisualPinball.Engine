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
using System.Collections.Generic;
using Unity.Mathematics;
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

		internal TriggerApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
		}

		#region Wiring

		public bool IsSwitchEnabled => SwitchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus) => AddSwitchDest(switchConfig, switchStatus);
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig);
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;

		#endregion

		#region Collider Generation

		protected override bool FireHitEvents => true;

		protected override void CreateColliders(ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			var meshComponent = GameObject.GetComponent<TriggerMeshComponent>();
			var colliderGenerator = new TriggerColliderGenerator(this, MainComponent, ColliderComponent, meshComponent, translateWithinPlayfieldMatrix);
			colliderGenerator.GenerateColliders(ref colliders);
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
			if (isUnHit) {
				UnHit?.Invoke(this, new HitEventArgs(ballId));
				Switch?.Invoke(this, new SwitchEventArgs(false, ballId));
				OnSwitch(false);

			} else {
				Hit?.Invoke(this, new HitEventArgs(ballId));
				Switch?.Invoke(this, new SwitchEventArgs(true, ballId));
				OnSwitch(true);
			}
		}

		#endregion

	}
}
