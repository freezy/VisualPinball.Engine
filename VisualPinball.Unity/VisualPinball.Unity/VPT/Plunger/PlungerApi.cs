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
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public class PlungerApi : CollidableApi<PlungerComponent, PlungerColliderComponent, PlungerData>,
		IApi, IApiRotatable, IApiCoilDevice, IApiWireDeviceDest
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the plunger moved back to the park position.
		/// </summary>
		public event EventHandler<StrokeEventArgs> LimitBos;

		/// <summary>
		/// Event emitted when the plunger was pulled back and reached its end position.
		/// </summary>
		public event EventHandler<StrokeEventArgs> LimitEos;

		/// <summary>
		/// This starts moving the plunger back, until the coil is turned off, which
		/// will then fire the coil.
		/// </summary>
		///
		/// <remarks>
		/// It's only technically a coil, in the real world it's the player's hand. ;)
		/// </remarks>
		public DeviceCoil PullCoil;

		/// <summary>
		/// Auto-fires the plunger.
		/// </summary>
		public DeviceCoil FireCoil;

		// todo
		public event EventHandler Timer;

		public bool DoRetract { get; set; } = true;

		internal PlungerApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
		}

		internal void OnAnalogPlunge(InputAction.CallbackContext ctx)
		{
			var pos = ctx.ReadValue<float>(); // 0 = resting pos, 1 = pulled back
			ref var plungerState = ref PhysicsEngine.PlungerState(ItemId);
			plungerState.Movement.AnalogPosition = pos;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);

			PullCoil = new DeviceCoil(Player, PullBack, Fire);
			FireCoil = new DeviceCoil(Player, Fire);
		}

		void IApi.OnDestroy()
		{
		}

		public void PullBack()
		{
			var collComponent = GameObject.GetComponent<PlungerColliderComponent>();
			if (!collComponent) {
				return;
			}

			ref var plungerState = ref PhysicsEngine.PlungerState(ItemId);
			if (DoRetract) {
				PlungerCommands.PullBackAndRetract(collComponent.SpeedPull, ref plungerState.Velocity, ref plungerState.Movement);

			} else {
				PlungerCommands.PullBack(collComponent.SpeedPull, ref plungerState.Velocity, ref plungerState.Movement);
			}
		}

		public void Fire()
		{
			var collComponent = GameObject.GetComponent<PlungerColliderComponent>();
			if (!collComponent) {
				return;
			}
			ref var plungerState = ref PhysicsEngine.PlungerState(ItemId);

			// check for an auto plunger
			if (collComponent.IsAutoPlunger) {
				// Auto Plunger - this models a "Launch Ball" button or a
				// ROM-controlled launcher, rather than a player-operated
				// spring plunger.  In a physical machine, this would be
				// implemented as a solenoid kicker, so the amount of force
				// is constant (modulo some mechanical randomness).  Simulate
				// this by triggering a release from the maximum retracted
				// position.
				PlungerCommands.Fire(1f, ref plungerState.Velocity, ref plungerState.Movement, in plungerState.Static);

			} else {
				// Regular plunger - trigger a release from the current
				// position, using the keyboard firing strength.

				var pos = (plungerState.Movement.Position - plungerState.Static.FrameEnd) / (plungerState.Static.FrameStart - plungerState.Static.FrameEnd);
				PlungerCommands.Fire(pos, ref plungerState.Velocity, ref plungerState.Movement, in plungerState.Static);
			}
		}

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => Coil(deviceItem);

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch
			{
				PlungerComponent.FireCoilId => FireCoil,
				PlungerComponent.PullCoilId => PullCoil,
				_ => throw new ArgumentException($"Unknown plunger coil \"{deviceItem}\". Valid names are: [ \"{PlungerComponent.FireCoilId}\", \"{PlungerComponent.PullCoilId}\" ].")
			};
		}

		#region Collider Generation

		protected override void CreateColliders(ref ColliderReference colliders,
			ref ColliderReference kinematicColliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			if (ColliderComponent._isKinematic) {
				kinematicColliders.Add(new PlungerCollider(MainComponent, ColliderComponent, GetColliderInfo()), translateWithinPlayfieldMatrix);
			} else {
				colliders.Add(new PlungerCollider(MainComponent, ColliderComponent, GetColliderInfo()), translateWithinPlayfieldMatrix);
			}
		}

		#endregion

		#region Events

		void IApiRotatable.OnRotate(float speed, bool direction)
		{
			if (direction) {
				LimitEos?.Invoke(this, new StrokeEventArgs { Speed = speed });
			} else {
				LimitBos?.Invoke(this, new StrokeEventArgs { Speed = speed });
			}
		}

		#endregion
	}

	public struct StrokeEventArgs
	{
		public float Speed;
	}
}
