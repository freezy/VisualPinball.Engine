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
using UnityEngine;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	public class GateApi : CollidableApi<GateComponent, GateColliderComponent, GateData>,
		IApi, IApiHittable, IApiRotatable, IApiSwitch, IApiSwitchDevice
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the gate.
		/// </summary>
		///
		/// <remarks>
		/// For two-way gates, this is emitted twice, once when entering, and
		/// once when leaving. For one-way gates, it's emitted once when the
		/// ball rolls through it, but not when the gate blocks the ball. <p/>
		///
		/// Also note that the gate must be collidable.
		/// </remarks>
		public event EventHandler<HitEventArgs> Hit;

		/// <summary>
		/// Event emitted when the gate passes its parked position. Only
		/// emitted for one-way gates.
		/// </summary>
		///
		/// <remarks>
		/// Can be emitted multiple times, as the gate bounces a few times
		/// before coming to a rest.<p/>
		///
		/// Note that the gate must be collidable.
		/// </remarks>
		public event EventHandler<RotationEventArgs> LimitBos;

		/// <summary>
		/// Event emitted when the gate rotates to its top position.
		/// </summary>
		///
		/// <remarks>
		/// The gate must be collidable.
		/// </remarks>
		public event EventHandler<RotationEventArgs> LimitEos;

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		public event EventHandler<SwitchEventArgs> Switch;

		// todo
		public event EventHandler Timer;

		public GateApi(GameObject go, Player player) : base(go, player)
		{
		}

		public void Lift(float speed, float angleDeg)
		{
			// fixme job
			// var data = EntityManager.GetComponentData<GateMovementData>(Entity);
			// data.IsLifting = true;
			// data.LiftSpeed = speed;
			// data.LiftAngle = math.radians(angleDeg);
			// EntityManager.SetComponentData(Entity, data);
		}

		#region Wiring

		public bool IsSwitchEnabled => SwitchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus) => AddSwitchDest(switchConfig.WithPulse(true), switchStatus);
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig.WithPulse(true));
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;

		#endregion

		#region Collider Generation

		protected override void CreateColliders(ref ColliderReference colliders, float margin)
		{
			var colliderGenerator = new GateColliderGenerator(this, MainComponent, ColliderComponent);
			colliderGenerator.GenerateColliders(MainComponent.PositionZ, ref colliders);
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
			Switch?.Invoke(this, new SwitchEventArgs(true, ballId));
			OnSwitch(true);
		}

		void IApiRotatable.OnRotate(float speed, bool direction)
		{
			if (direction) {
				LimitEos?.Invoke(this, new RotationEventArgs { AngleSpeed = speed });
			} else {
				LimitBos?.Invoke(this, new RotationEventArgs { AngleSpeed = speed });
			}
		}

		#endregion
	}
}
