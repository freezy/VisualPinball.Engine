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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class SpinnerApi : ItemApi<SpinnerAuthoring, Engine.VPT.Spinner.Spinner, Engine.VPT.Spinner.SpinnerData>,
		IApiInitializable, IApiRotatable, IApiSpinnable, IApiSwitch, IApiColliderGenerator
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the spinner reaches the minimal angle.
		/// </summary>
		///
		/// <remarks>
		/// Only emitted if min and max angle are different, otherwise
		/// subscribe to the <see cref="Spin"/> event.
		/// </remarks>
		public event EventHandler<RotationEventArgs> LimitBos;

		/// <summary>
		/// Event emitted when the spinner reaches the maximal angle.
		/// </summary>
		///
		/// <remarks>
		/// Only emitted if min and max angle are different, otherwise
		/// subscribe to the <see cref="Spin"/> event.
		/// </remarks>
		public event EventHandler<RotationEventArgs> LimitEos;

		/// <summary>
		/// Event emitted when the spinner performs one spin.
		/// </summary>
		///
		/// <remarks>
		/// Only emitted when min and max angles are the same, i.e. the spinner
		/// is able to rotate entirely without rotated back at a given angle.
		/// </remarks>
		public event EventHandler Spin;

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		public event EventHandler<SwitchEventArgs> Switch;

		// todo
		public event EventHandler Timer;

		public SpinnerApi(Engine.VPT.Spinner.Spinner item, GameObject go, Entity entity, Entity parentEntity, Player player)
			: base(item, go, entity, parentEntity, player)
		{
		}

		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig) => AddSwitchDest(switchConfig.WithPulse(Item.IsPulseSwitch));
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig.WithPulse(Item.IsPulseSwitch));
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);
		void IApiSwitch.DestroyBall(Entity ballEntity) => DestroyBall(ballEntity);

		#region Collider Generation

		protected override bool FireHitEvents { get; } = true;
		Entity IApiColliderGenerator.ColliderEntity => Entity;

		void IApiColliderGenerator.CreateColliders(Table table, List<ICollider> colliders)
		{
			var spinnerComp = GameObject.GetComponent<SpinnerAuthoring>();
			if (spinnerComp) {
				var colliderGenerator = new SpinnerColliderGenerator(this, spinnerComp.CreateData());
				colliderGenerator.GenerateColliders(spinnerComp.HeightOnPlayfield, colliders);
			}
		}

		ColliderInfo IApiColliderGenerator.GetColliderInfo() => GetColliderInfo();

		#endregion

		#region Events

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiSpinnable.OnSpin()
		{
			Spin?.Invoke(this, EventArgs.Empty);
			Switch?.Invoke(this, new SwitchEventArgs(true, Entity.Null));
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
