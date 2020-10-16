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
	public abstract class ItemApi<T, TData> where T : Item<TData> where TData : ItemData
	{
		protected readonly T Item;
		internal readonly Entity Entity;

		protected TData Data => Item.Data;
		protected Table Table => _player.Table;

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

		private List<string> _switchIds;
		private readonly IGamelogicEngineWithSwitches _gamelogicEngineWithSwitches;

		protected void AddSwitchId(string switchId)
		{
			if (_switchIds == null) {
				_switchIds = new List<string>();
			}
			_switchIds.Add(switchId);
		}

		protected void OnSwitch(bool normallyClosed)
		{
			if (_gamelogicEngineWithSwitches != null && _switchIds != null) {
				foreach (var switchId in _switchIds) {
					_gamelogicEngineWithSwitches.Switch(switchId, normallyClosed);
				}
			}
		}

		#endregion
	}
}
