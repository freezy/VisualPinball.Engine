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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Drop Target")]
	public class DropTargetComponent : TargetComponent
	{
		public override bool IsLegacy {
			get {
				var colliderComponent = GetComponent<DropTargetColliderComponent>();
				return colliderComponent && colliderComponent.IsLegacy;
			}
		}

		protected override float ZOffset {
			get {
				var animationComponent = GetComponentInChildren<DropTargetAnimationComponent>();
				return animationComponent && animationComponent.IsDropped ? -DropTargetAnimationState.DropTargetLimit : 0f;
			}
		}

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(HitTargetData data)
		{
			var updatedComponents = base.SetData(data).ToList();

			// drop target data
			var colliderComponent = GetComponent<DropTargetColliderComponent>();
			if (colliderComponent) {
				colliderComponent.enabled = data.IsCollidable;
				colliderComponent.UseHitEvent = data.UseHitEvent;
				colliderComponent.Threshold = data.Threshold;
				colliderComponent.IsLegacy = data.IsLegacy;

				colliderComponent.OverwritePhysics = data.OverwritePhysics;
				colliderComponent.Elasticity = data.Elasticity;
				colliderComponent.ElasticityFalloff = data.ElasticityFalloff;
				colliderComponent.Friction = data.Friction;
				colliderComponent.Scatter = data.Scatter;

				updatedComponents.Add(colliderComponent);
			}

			// animation data
			var animationComponent = GetComponent<DropTargetAnimationComponent>();
			if (animationComponent) {
				animationComponent.enabled = data.IsDropTarget;
				animationComponent.Speed = data.DropSpeed;
				animationComponent.RaiseDelay = data.RaiseDelay;
				animationComponent.IsDropped = data.IsDropped;

				updatedComponents.Add(animationComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(HitTargetData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			var colliderComponent = GetComponent<DropTargetColliderComponent>();
			if (colliderComponent) {
				colliderComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}

			// visibility
			SetEnabled<Renderer>(data.IsVisible);

			return Array.Empty<MonoBehaviour>();
		}

		public override HitTargetData CopyDataTo(HitTargetData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			base.CopyDataTo(data, materialNames, textureNames, forExport);

			// collision data
			var colliderComponent = GetComponent<DropTargetColliderComponent>();
			if (colliderComponent) {
				data.IsCollidable = colliderComponent.enabled;
				data.Threshold = colliderComponent.Threshold;
				data.UseHitEvent = colliderComponent.UseHitEvent;
				data.PhysicsMaterial = colliderComponent.PhysicsMaterial == null ? string.Empty : colliderComponent.PhysicsMaterial.name;
				data.IsLegacy = colliderComponent.IsLegacy;

				data.OverwritePhysics = colliderComponent.OverwritePhysics;
				data.Elasticity = colliderComponent.Elasticity;
				data.ElasticityFalloff = colliderComponent.ElasticityFalloff;
				data.Friction = colliderComponent.Friction;
				data.Scatter = colliderComponent.Scatter;

			} else {
				data.IsCollidable = false;
			}

			// animation data
			var dropTargetAnimationComponent = GetComponent<DropTargetAnimationComponent>();
			if (dropTargetAnimationComponent) {
				data.DropSpeed = dropTargetAnimationComponent.Speed;
				data.RaiseDelay = dropTargetAnimationComponent.RaiseDelay;
				data.IsDropped = dropTargetAnimationComponent.IsDropped;
			}

			return data;
		}

		#endregion

		#region Runtime

		public DropTargetApi DropTargetApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			DropTargetApi = new DropTargetApi(gameObject, player, physicsEngine);

			player.Register(DropTargetApi, this);
			if (GetComponent<DropTargetColliderComponent>() && GetComponentInChildren<DropTargetAnimationComponent>()) {
				RegisterPhysics(physicsEngine);
			}
		}

		#endregion

		#region State

		internal DropTargetState CreateState()
		{
			var colliderComponent = GetComponent<DropTargetColliderComponent>();
			var animationComponent = GetComponentInChildren<DropTargetAnimationComponent>();

			var staticData = colliderComponent && animationComponent
				? new DropTargetStaticState {
					Speed = animationComponent.Speed,
					RaiseDelay = animationComponent.RaiseDelay,
					UseHitEvent = colliderComponent.UseHitEvent,
					InitialPosition = transform.position
				} : default;

			var animationData = colliderComponent && animationComponent
				? new DropTargetAnimationState {
					IsDropped = animationComponent.IsDropped,
					MoveDown = !animationComponent.IsDropped,
					ZOffset = animationComponent.IsDropped ? -DropTargetAnimationState.DropTargetLimit : 0f
				} : default;

			return new DropTargetState(
				animationComponent ? animationComponent.gameObject.GetInstanceID() : 0,
				staticData,
				animationData
			);
		}

		#endregion
	}
}
