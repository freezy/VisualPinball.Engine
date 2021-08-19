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

namespace VisualPinball.Unity
{
	public abstract class ItemCollidableApi<TComponent, TCollidableComponent, TItem, TData> : ItemApi<TComponent, TItem, TData>
		where TComponent : ItemMainAuthoring<TItem, TData>
		where TCollidableComponent : ItemColliderAuthoring<TItem, TData, TComponent>
		where TItem : Item<TData>
		where TData : ItemData
	{
		protected readonly TCollidableComponent ColliderComponent;

		private protected EntityManager EntityManager;
		protected readonly Entity Entity;
		private readonly Entity _parentEntity;

		protected ItemCollidableApi(GameObject go, Entity entity, Entity parentEntity, Player player) : base(go, player)
		{
			EntityManager = World.DefaultGameObjectInjectionWorld != null ? World.DefaultGameObjectInjectionWorld.EntityManager : default;
			Entity = entity;
			_parentEntity = parentEntity;

			ColliderComponent = go.GetComponent<TCollidableComponent>();
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
		internal ColliderInfo GetColliderInfo(PhysicsMaterial physicsMaterial = null) => GetColliderInfo(MainComponent.ItemType, physicsMaterial);

		internal ColliderInfo GetColliderInfo(ItemType itemType, PhysicsMaterial physicsMaterial = null)
		{
			return new ColliderInfo {
				Id = -1,
				ItemType = itemType,
				Entity = Entity,
				ParentEntity = _parentEntity,
				FireEvents = FireHitEvents,
				IsEnabled = IsColliderEnabled,
				Material = GetPhysicsMaterial(physicsMaterial),
				HitThreshold = HitThreshold,
			};
		}

		#endregion
	}
}
