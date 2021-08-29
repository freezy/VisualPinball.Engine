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

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public abstract class ItemCollidableApi<TComponent, TCollidableComponent, TData> : ItemApi<TComponent, TData>,
		IApiColliderGenerator
		where TComponent : ItemMainAuthoring<TData>
		where TCollidableComponent : ItemColliderAuthoring<TData, TComponent>
		where TData : ItemData
	{
		protected readonly Entity Entity;
		protected readonly TCollidableComponent ColliderComponent;

		private protected EntityManager EntityManager;
		private readonly Entity _parentEntity;

		protected ItemCollidableApi(GameObject go, Entity entity, Entity parentEntity, Player player) : base(go, player)
		{
			EntityManager = World.DefaultGameObjectInjectionWorld != null ? World.DefaultGameObjectInjectionWorld.EntityManager : default;
			Entity = entity;
			_parentEntity = parentEntity;

			ColliderComponent = go.GetComponent<TCollidableComponent>();
		}

		#region Collider

		bool IApiColliderGenerator.IsColliderAvailable => ColliderComponent;
		bool IApiColliderGenerator.IsColliderEnabled => ColliderComponent && ColliderComponent.isActiveAndEnabled;
		Entity IApiColliderGenerator.ColliderEntity => Entity;

		protected virtual bool FireHitEvents => false;
		protected virtual float HitThreshold => 0;

		protected abstract void CreateColliders(List<ICollider> colliders);

		void IApiColliderGenerator.CreateColliders(List<ICollider> colliders)
		{
			CreateColliders(colliders);
		}

		ColliderInfo IApiColliderGenerator.GetColliderInfo() => GetColliderInfo();

		public ColliderInfo GetColliderInfo() => GetColliderInfo(MainComponent.ItemType);

		public ColliderInfo GetColliderInfo(ItemType itemType)
		{
			return new ColliderInfo {
				Id = -1,
				ItemType = itemType,
				Entity = Entity,
				ParentEntity = _parentEntity,
				FireEvents = FireHitEvents,
				IsEnabled = ColliderComponent && ColliderComponent.isActiveAndEnabled,
				Material = ColliderComponent.PhysicsMaterialData,
				HitThreshold = HitThreshold,
			};
		}

		#endregion
	}
}
