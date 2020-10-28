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

using System.Collections.Generic;
using Unity.Entities;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public abstract class ItemApi<T, TData> : IApi where T : Item<TData> where TData : ItemData
	{
		public string Name => Item.Name;

		protected readonly T Item;
		internal readonly Entity Entity;

		protected TData Data => Item.Data;
		protected Table Table => _player.Table;
		protected TableApi TableApi => _player.TableApi;

		protected readonly EntityManager EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

		internal VisualPinballSimulationSystemGroup SimulationSystemGroup => World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();

		private readonly Player _player;

		protected ItemApi(T item, Entity entity, Player player)
		{
			Item = item;
			Entity = entity;
			_player = player;
			_gamelogicEngineWithSwitches = (IGamelogicEngineWithSwitches)player.GameEngine;
		}

		#region IApiSwitchable

		private List<SwitchConfig> _switchIds;
		private readonly IGamelogicEngineWithSwitches _gamelogicEngineWithSwitches;

		protected void AddSwitchId(string switchId, bool isPulseSwitch, int pulseDelay)
		{
			if (_switchIds == null) {
				_switchIds = new List<SwitchConfig>();
			}
			_switchIds.Add(new SwitchConfig(switchId, isPulseSwitch, pulseDelay));
		}

		protected void OnSwitch(bool normallyClosed)
		{
			if (_gamelogicEngineWithSwitches != null && _switchIds != null) {
				foreach (var switchConfig in _switchIds) {
					_gamelogicEngineWithSwitches.Switch(switchConfig.SwitchId, normallyClosed);

					// time switch opening if closed and pulse
					if (normallyClosed && switchConfig.IsPulseSwitch) {
						SimulationSystemGroup.ScheduleSwitch(switchConfig.PulseDelay,
							() => _gamelogicEngineWithSwitches.Switch(switchConfig.SwitchId, false));
					}
				}
			}
		}

		#endregion

		private readonly struct SwitchConfig
		{
			public readonly string SwitchId;
			public readonly int PulseDelay;
			public readonly bool IsPulseSwitch;

			public SwitchConfig(string switchId, bool isPulseSwitch, int pulseDelay)
			{
				SwitchId = switchId;
				PulseDelay = pulseDelay;
				IsPulseSwitch = isPulseSwitch;
			}
		}
	}
}
