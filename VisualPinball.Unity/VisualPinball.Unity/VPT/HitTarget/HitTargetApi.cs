// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using VisualPinball.Engine.Game.Engine;

namespace VisualPinball.Unity
{
	public class HitTargetApi : ItemApi<Engine.VPT.HitTarget.HitTarget, Engine.VPT.HitTarget.HitTargetData>,
		IApiInitializable, IApiHittable, IApiSwitchable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the hit target.
		/// </summary>
		public event EventHandler Hit;

		/// <summary>
		/// Sets the status of a drop target.
		/// </summary>
		///
		/// <remarks>
		/// Setting this will animate the drop target to the desired position.
		/// </remarks>
		///
		/// <exception cref="InvalidOperationException">Thrown if target is not a drop target (but a hit target, which can't be dropped)</exception>
		public bool IsDropped {
			get => EntityManager.GetComponentData<HitTargetAnimationData>(Entity).IsDropped;
			set => SetIsDropped(value);
		}

		internal HitTargetApi(Engine.VPT.HitTarget.HitTarget item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="isDropped"></param>
		/// <exception cref="InvalidOperationException"></exception>
		private void SetIsDropped(bool isDropped)
		{
			if (!Item.Data.IsDropTarget) {
				throw new InvalidOperationException($"You tried to drop hit target {Item.Name}, but only drop targets are droppable!");
			}

			var data = EntityManager.GetComponentData<HitTargetAnimationData>(Entity);
			if (data.IsDropped != isDropped) {
				data.MoveAnimation = true;
				if (isDropped) {
					data.MoveDown = true;

				} else {
					data.MoveDown = false;
					data.TimeStamp = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>().TimeMsec;
				}
			} else {
				data.IsDropped = isDropped;
			}
			EntityManager.SetComponentData(Entity, data);
		}

		#region Events

		void IApiInitializable.OnInit()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(bool _)
		{
			Hit?.Invoke(this, EventArgs.Empty);
			GamelogicEngineWithSwitches?.Switch(Item.Name, true);
		}

		void IApiSwitchable.SetGamelogicEngine(IGamelogicEngineWithSwitches gle) => GamelogicEngineWithSwitches = gle;

		#endregion
	}
}
