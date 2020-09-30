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
using VisualPinball.Engine.Game.Engine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public abstract class ItemApi<T, TData> where T : Item<TData> where TData : ItemData
	{
		protected readonly T Item;
		protected readonly Player Player;
		internal readonly Entity Entity;

		protected IGamelogicEngineWithSwitches GamelogicEngineWithSwitches;

		protected TData Data => Item.Data;
		protected Table Table => Player.Table;

		protected readonly EntityManager EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

		internal VisualPinballSimulationSystemGroup SimulationSystemGroup => World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();

		protected ItemApi(T item, Entity entity, Player player)
		{
			Item = item;
			Entity = entity;
			Player = player;
		}
	}
}
