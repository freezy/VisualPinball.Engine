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

using Unity.Entities;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Base class for all item APIs.
	/// </summary>
	/// <typeparam name="T">Item type</typeparam>
	/// <typeparam name="TData">Item data type</typeparam>
	[Api]
	public abstract class ItemApi<T, TData> : IApi where T : Item<TData> where TData : ItemData
	{
		/// <summary>
		/// Item name
		/// </summary>
		public string Name => Item.Name;

		private protected readonly T Item;
		internal readonly Entity Entity;

		private protected TData Data => Item.Data;
		private protected Table Table => _player.Table;
		private protected TableApi TableApi => _player.TableApi;

		private protected EntityManager EntityManager;

		internal VisualPinballSimulationSystemGroup SimulationSystemGroup => World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();

		private readonly Player _player;
		private readonly SwitchHandler _switchHandler;
		private protected BallManager BallManager;

		private protected ItemApi(T item, Player player)
		{
			Item = item;
			Entity = Entity.Null;
			_player = player;
		}

		private protected ItemApi(T item, Entity entity, Player player)
		{
			EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			Item = item;
			Entity = entity;
			_player = player;
			_switchHandler = new SwitchHandler(Name, player);
		}

		private protected void OnInit(BallManager ballManager)
		{
			BallManager = ballManager;
		}

		private protected void DestroyBall(Entity ballEntity)
		{
			BallManager.DestroyEntity(ballEntity);
		}

		void IApi.OnDestroy()
		{
		}

		#region IApiSwitchable

		private protected DeviceSwitch CreateSwitch(string name, bool isPulseSwitch, bool isOptoSwitch) => new DeviceSwitch(name, isPulseSwitch, isOptoSwitch, _player);

		private protected void AddSwitchId(SwitchConfig switchConfig) => _switchHandler.AddSwitchId(switchConfig);

		internal void AddWireDest(WireDestConfig wireConfig) => _switchHandler.AddWireDest(wireConfig);

		private protected void OnSwitch(bool closed) => _switchHandler.OnSwitch(closed);

		#endregion
	}
}
