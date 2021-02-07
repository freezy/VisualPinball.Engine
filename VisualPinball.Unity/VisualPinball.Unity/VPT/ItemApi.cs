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
using Unity.Mathematics;
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
	public abstract class ItemApi<TItem, TData> : IApi where TItem : Item<TData> where TData : ItemData
	{
		/// <summary>
		/// Item name
		/// </summary>
		public string Name => Item.Name;

		internal TItem Item;
		internal readonly Entity Entity;
		internal readonly Entity ParentEntity;

		public TData Data => Item.Data;
		private protected Table Table => _player.Table;
		private protected TableApi TableApi => _player.TableApi;

		private protected EntityManager EntityManager;

		internal VisualPinballSimulationSystemGroup SimulationSystemGroup => World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();

		private readonly Player _player;
		private readonly SwitchHandler _switchHandler;
		private protected BallManager BallManager;

		protected ItemApi(TItem item, Player player)
		{
			Item = item;
			Entity = Entity.Null;
			ParentEntity = Entity.Null;
			_player = player;
		}

		protected ItemApi(TItem item, Entity entity, Entity parentEntity, Player player)
		{
			EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			Item = item;
			Entity = entity;
			ParentEntity = parentEntity;
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

		#region Collider

		internal virtual bool IsColliderEnabled  => !(Data is IPhysicalData physicalData) || physicalData.GetIsCollidable();
		internal virtual bool FireHitEvents { get; } = false;
		internal virtual float HitThreshold { get; } = 0;
		internal virtual PhysicsMaterialData GetPhysicsMaterial(Table table)
		{
			if (Data is IPhysicalData physicalData) {
				var mat = table.GetMaterial(physicalData.GetPhysicsMaterial());
				var matData = new PhysicsMaterialData();
				if (mat != null && !physicalData.GetOverwritePhysics()) {
					matData.Elasticity = mat.Elasticity;
					matData.ElasticityFalloff = mat.ElasticityFalloff;
					matData.Friction = mat.Friction;
					matData.ScatterAngleRad = math.radians(mat.ScatterAngle);

				} else {
					matData.Elasticity = physicalData.GetElasticity();
					matData.ElasticityFalloff = physicalData.GetElasticityFalloff();
					matData.Friction = physicalData.GetFriction();
					matData.ScatterAngleRad = math.radians(physicalData.GetScatter());
				}
				return matData;
			}
			return default;
		}

		/// <summary>
		/// Returns returns collider info passed when creating the collider.
		///
		/// Use this for colliders that are part of the quad tree.
		/// </summary>
		/// <param name="table"></param>
		/// <param name="nextColliderId">Reference to collider index</param>
		internal ColliderInfo GetNextColliderInfo(Table table, ref int nextColliderId)
		{
			var id = nextColliderId++;
			return GetColliderInfo(table, id);
		}

		/// <summary>
		/// Only use this for colliders that are part of another collider.
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		internal ColliderInfo GetChildColliderInfo(Table table)
		{
			return GetColliderInfo(table, -1);
		}

		private ColliderInfo GetColliderInfo(Table table, int id)
		{
			return new ColliderInfo {
				Id = id,
				ItemType = Item.ItemType,
				Entity = Entity,
				ParentEntity = ParentEntity,
				FireEvents = FireHitEvents,
				IsEnabled = IsColliderEnabled,
				Material = GetPhysicsMaterial(table),
				HitThreshold = HitThreshold,
			};
		}

		#endregion

		void IApi.OnDestroy()
		{
		}

		#region IApiSwitchable

		private protected DeviceSwitch CreateSwitch(string name, bool isPulseSwitch, SwitchDefault switchDefault = SwitchDefault.Configurable) => new DeviceSwitch(name, isPulseSwitch, switchDefault, _player);

		private protected IApiSwitchStatus AddSwitchDest(SwitchConfig switchConfig) => _switchHandler.AddSwitchDest(switchConfig);

		internal void AddWireDest(WireDestConfig wireConfig) => _switchHandler.AddWireDest(wireConfig);

		private protected void OnSwitch(bool closed) => _switchHandler.OnSwitch(closed);

		#endregion
	}
}
