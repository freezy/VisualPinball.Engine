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
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	public class DropTargetApi : CollidableApi<TargetComponent, DropTargetColliderComponent, HitTargetData>,
		IApi, IApiHittable, IApiSwitch, IApiSwitchDevice, IApiDroppable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the hit target.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		public event EventHandler<SwitchEventArgs> Switch;

		/// <summary>
		/// Sets the status of a drop target.
		/// </summary>
		///
		/// <remarks>
		/// Setting this will animate the drop target to the desired position.
		/// </remarks>
		///
		/// <exception cref="InvalidOperationException">Thrown if target is not a drop target (but a hit target, which can't be dropped)</exception>
		public bool IsDropped
		{
			get => PhysicsEngine.DropTargetState(ItemId).Animation.IsDropped;
			set => SetIsDropped(value);
		}

		internal DropTargetApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
		}

		public void OnDropStatusChanged(bool isDropped, int ballId)
		{
			OnSwitch(isDropped);
			Switch?.Invoke(this, new SwitchEventArgs(isDropped, ballId));
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="isDropped"></param>
		/// <exception cref="InvalidOperationException"></exception>
		private void SetIsDropped(bool isDropped)
		{
			ref var state = ref PhysicsEngine.DropTargetState(ItemId);
			if (state.Animation.IsDropped != isDropped) {
				state.Animation.MoveAnimation = true;
				if (isDropped) {
					state.Animation.MoveDown = true;
				}
				else {
					state.Animation.MoveDown = false;
					state.Animation.TimeStamp = PhysicsEngine.TimeMsec;
				}
			} else {
				state.Animation.IsDropped = isDropped;
			}
		}

		#region Wiring

		public bool IsSwitchEnabled => SwitchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus) => AddSwitchDest(switchConfig, switchStatus);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;

		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig);
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);

		#endregion

		#region Collider Generation

		protected override bool FireHitEvents => true;
		protected override float HitThreshold => ColliderComponent.Threshold;

		protected override void CreateColliders(ref ColliderReference colliders,
			ref ColliderReference kinematicColliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			var colliderGenerator = new DropTargetColliderGenerator(this, MainComponent, MainComponent, translateWithinPlayfieldMatrix);
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

		void IApiHittable.OnHit(int ballId, bool _)
		{
			Hit?.Invoke(this, new HitEventArgs(ballId));
		}

		#endregion
	}
}
