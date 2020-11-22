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

		protected EntityManager EntityManager;

		internal VisualPinballSimulationSystemGroup SimulationSystemGroup => World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();

		private readonly Player _player;
		private readonly SwitchHandler _switchHandler;
		protected BallManager BallManager;

		protected ItemApi(T item, Player player)
		{
			Item = item;
			Entity = Entity.Null;
			_player = player;
			_gamelogicEngineWithSwitches = (IGamelogicEngineWithSwitches)player.GameEngine;
		}

		protected ItemApi(T item, Entity entity, Player player)
		{
			EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			Item = item;
			Entity = entity;
			_player = player;
			_switchHandler = new SwitchHandler(Name, player, (IGamelogicEngineWithSwitches)player.GameEngine);
			_gamelogicEngineWithSwitches = (IGamelogicEngineWithSwitches)player.GameEngine;
		}

		protected void OnInit(BallManager ballManager)
		{
			BallManager = ballManager;
		}

		protected void DestroyBall(Entity ballEntity)
		{
			BallManager.DestroyEntity(ballEntity);
		}

		void IApi.OnDestroy()
		{
		}

		#region IApiSwitchable

		private readonly IGamelogicEngineWithSwitches _gamelogicEngineWithSwitches;

		protected DeviceSwitch CreateSwitch(string name, bool isPulseSwitch) => new DeviceSwitch(name, isPulseSwitch, _gamelogicEngineWithSwitches, _player);

		protected void AddSwitchId(SwitchConfig switchConfig) => _switchHandler.AddSwitchId(switchConfig);

		internal void AddWireDest(WireDestConfig wireConfig) => _switchHandler.AddWireDest(wireConfig);

		protected void OnSwitch(bool closed) => _switchHandler.OnSwitch(closed);

		#endregion
	}
}
