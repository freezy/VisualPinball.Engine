// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
	public abstract class CollidableApi<TComponent, TCollidableComponent, TData> : ItemApi<TComponent, TData>,
		IApiColliderGenerator
		where TComponent : MainComponent<TData>
		where TCollidableComponent : ColliderComponent<TData, TComponent>
		where TData : ItemData
	{
		public bool IsCollidable {
			get => _simulateCycleSystemGroup != null && _simulateCycleSystemGroup.ItemsColliding[Entity];
			set {
				if (_simulateCycleSystemGroup != null) {
					_simulateCycleSystemGroup.ItemsColliding[Entity] = value;
				}
			}
		}

		protected readonly Entity Entity;
		protected readonly TCollidableComponent ColliderComponent;

		private protected EntityManager EntityManager;
		private readonly SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected CollidableApi(GameObject go, Entity entity, Player player) : base(go, player)
		{
			if (World.DefaultGameObjectInjectionWorld != null) {
				_simulateCycleSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<SimulateCycleSystemGroup>();
			}
			EntityManager = World.DefaultGameObjectInjectionWorld != null ? World.DefaultGameObjectInjectionWorld.EntityManager : default;
			Entity = entity;

			ColliderComponent = go.GetComponent<TCollidableComponent>();
		}

		#region Collider

		bool IApiColliderGenerator.IsColliderAvailable => ColliderComponent;
		bool IApiColliderGenerator.IsColliderEnabled => ColliderComponent && ColliderComponent.isActiveAndEnabled;
		Entity IApiColliderGenerator.ColliderEntity => Entity;

		protected virtual bool FireHitEvents => false;
		protected virtual float HitThreshold => 0;

		protected abstract void CreateColliders(List<ICollider> colliders, float margin);

		void IApiColliderGenerator.CreateColliders(List<ICollider> colliders, float margin)
		{
			if (!ColliderComponent) {
				return;
			}
			CreateColliders(colliders, margin);
		}

		ColliderInfo IApiColliderGenerator.GetColliderInfo() => GetColliderInfo(MainComponent.ItemType);

		public ColliderInfo GetColliderInfo() => GetColliderInfo(MainComponent.ItemType);

		public ColliderInfo GetColliderInfo(ItemType itemType)
		{
			return new ColliderInfo {
				Id = -1,
				ItemType = itemType,
				Entity = Entity,
				FireEvents = FireHitEvents,
				IsEnabled = ColliderComponent && ColliderComponent.isActiveAndEnabled,
				Material = ColliderComponent.PhysicsMaterialData,
				HitThreshold = HitThreshold,
			};
		}

		#endregion
	}
}
