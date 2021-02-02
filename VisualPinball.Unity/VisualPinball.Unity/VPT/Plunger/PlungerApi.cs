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
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public class PlungerApi : ItemApi<Plunger, PlungerData>, IApiInitializable, IApiRotatable, IApiCoilDevice, IApiWireDeviceDest
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

		internal PlungerApi(Plunger item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		internal void OnAnalogPlunge(InputAction.CallbackContext ctx)
		{
			var pos = ctx.ReadValue<float>(); // 0 = resting pos, 1 = pulled back
			var movementData = EntityManager.GetComponentData<PlungerMovementData>(Entity);
			movementData.AnalogPosition = pos;
			EntityManager.SetComponentData(Entity, movementData);
		}

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);

			PullCoil = new DeviceCoil(PullBack, Fire);
			FireCoil = new DeviceCoil(Fire);
		}

		public void PullBack()
		{
			var movementData = EntityManager.GetComponentData<PlungerMovementData>(Entity);
			var velocityData = EntityManager.GetComponentData<PlungerVelocityData>(Entity);

			if (DoRetract) {
				PlungerCommands.PullBackAndRetract(Item.Data.SpeedPull, ref velocityData, ref movementData);

			} else {
				PlungerCommands.PullBack(Item.Data.SpeedPull, ref velocityData, ref movementData);
			}

			EntityManager.SetComponentData(Entity, movementData);
			EntityManager.SetComponentData(Entity, velocityData);
		}

		public void Fire()
		{
			var movementData = EntityManager.GetComponentData<PlungerMovementData>(Entity);
			var velocityData = EntityManager.GetComponentData<PlungerVelocityData>(Entity);
			var staticData = EntityManager.GetComponentData<PlungerStaticData>(Entity);

			// check for an auto plunger
			if (Item.Data.AutoPlunger) {
				// Auto Plunger - this models a "Launch Ball" button or a
				// ROM-controlled launcher, rather than a player-operated
				// spring plunger.  In a physical machine, this would be
				// implemented as a solenoid kicker, so the amount of force
				// is constant (modulo some mechanical randomness).  Simulate
				// this by triggering a release from the maximum retracted
				// position.
				PlungerCommands.Fire(1f, ref velocityData, ref movementData, in staticData);

			} else {
				// Regular plunger - trigger a release from the current
				// position, using the keyboard firing strength.

				var pos = (movementData.Position - staticData.FrameEnd) / (staticData.FrameStart - staticData.FrameEnd);
				PlungerCommands.Fire(pos, ref velocityData, ref movementData, in staticData);
			}

			EntityManager.SetComponentData(Entity, movementData);
			EntityManager.SetComponentData(Entity, velocityData);
		}

		IApiCoil IApiCoilDevice.Coil(string coilId)
		{
			switch (coilId) {
				case Plunger.FireCoil:
					return FireCoil;

				case Plunger.PullCoil:
					return PullCoil;

				default:
					return null;
			}
		}

		IApiWireDest IApiWireDeviceDest.Wire(string coilId) => (this as IApiCoilDevice).Coil(coilId);

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
