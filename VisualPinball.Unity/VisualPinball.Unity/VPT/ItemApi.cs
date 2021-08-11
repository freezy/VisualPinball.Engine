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
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Base class for all item APIs.
	/// </summary>
	/// <typeparam name="TItem">Item type</typeparam>
	/// <typeparam name="TData">Item data type</typeparam>
	[Api]
	public abstract class ItemApi<TItem, TData> : IApi where TItem : Item<TData> where TData : ItemData
	{
		/// <summary>
		/// Item name
		/// </summary>
		public string Name => Item.Name;

		internal TItem Item;
		internal GameObject GameObject;
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

		protected ItemApi(TItem item, GameObject go, Player player)
		{
			Item = item;
			GameObject = go;
			Entity = Entity.Null;
			ParentEntity = Entity.Null;
			_player = player;
		}

		protected ItemApi(TItem item, GameObject go, Entity entity, Entity parentEntity, Player player)
		{
			EntityManager = World.DefaultGameObjectInjectionWorld != null ? World.DefaultGameObjectInjectionWorld.EntityManager : default;
			Item = item;
			GameObject = go;
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

		public virtual bool IsColliderEnabled  => !(Data is IPhysicsMaterialData physicalData) || physicalData.GetIsCollidable();
		protected virtual bool FireHitEvents { get; } = false;
		protected virtual float HitThreshold { get; } = 0;
		protected virtual PhysicsMaterialData GetPhysicsMaterial(PhysicsMaterial mat)
		{
			if (Data is IPhysicsMaterialData physicalData) {
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
		/// <param name="physicsMaterial">physics material read from the collider component</param>
		internal ColliderInfo GetColliderInfo(PhysicsMaterial physicsMaterial = null) => GetColliderInfo(Item.ItemType, physicsMaterial);

		internal ColliderInfo GetColliderInfo(ItemType itemType, PhysicsMaterial physicsMaterial = null)
		{
			return new ColliderInfo {
				Id = -1,
				ItemType = itemType,
				Entity = Entity,
				ParentEntity = ParentEntity,
				FireEvents = FireHitEvents,
				IsEnabled = IsColliderEnabled,
				Material = GetPhysicsMaterial(physicsMaterial),
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
		internal void RemoveWireDest(string destId) => _switchHandler.RemoveWireDest(destId);

		private protected void OnSwitch(bool closed) => _switchHandler.OnSwitch(closed);

		#endregion
	}
}
