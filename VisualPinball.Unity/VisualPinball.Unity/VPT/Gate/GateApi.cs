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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class GateApi : ItemApi<Engine.VPT.Gate.Gate, Engine.VPT.Gate.GateData>, IApiInitializable,
		IApiHittable, IApiRotatable, IApiSwitch, IApiColliderGenerator
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

		public GateApi(Engine.VPT.Gate.Gate item, Entity entity, Entity parentEntity, Player player) : base(item, entity, parentEntity, player)
		{
		}

		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig) => AddSwitchDest(switchConfig.WithPulse(Item.IsPulseSwitch));
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig.WithPulse(Item.IsPulseSwitch));
		void IApiSwitch.DestroyBall(Entity ballEntity) => DestroyBall(ballEntity);

		#region Collider Generation

		void IApiColliderGenerator.CreateColliders(Table table, List<ICollider> colliders)
		{
			var colliderGenerator = new GateColliderGenerator(this);
			colliderGenerator.GenerateColliders(table, colliders);
		}

		ColliderInfo IApiColliderGenerator.GetColliderInfo(Table table) => GetColliderInfo(table);

		#endregion

		#region Events

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(Entity ballEntity, bool _)
		{
			Hit?.Invoke(this, new HitEventArgs(ballEntity));
			Switch?.Invoke(this, new SwitchEventArgs(true, ballEntity));
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
