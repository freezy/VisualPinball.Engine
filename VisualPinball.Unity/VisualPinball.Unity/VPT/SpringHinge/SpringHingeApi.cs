// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Unity.Mathematics;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	public class SpringHingeApi : IApi, IApiColliderGenerator
	{
		private readonly SpringHingeComponent _component;
		private readonly PhysicsEngine _physicsEngine;
		private readonly int _itemId;
		private readonly SpringHingeColliderComponent _colliderComponent;

		public event EventHandler Init;

		internal SpringHingeApi(SpringHingeComponent component, PhysicsEngine physicsEngine)
		{
			_component = component;
			_physicsEngine = physicsEngine;
			_itemId = component.ItemId;
			_colliderComponent = component.GetComponent<SpringHingeColliderComponent>();
		}

		internal float Angle => _component.PublishedAngle;

		public void Reset(float angle)
		{
			if (!_physicsEngine) {
				return;
			}
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.SpringHingeStates.ContainsKey(_itemId)) {
					return;
				}
				ref var hinge = ref state.SpringHingeStates.GetValueByRef(_itemId);
				hinge.Movement.Angle = math.clamp(math.radians(angle),
					hinge.Static.MinimumAngle, hinge.Static.MaximumAngle);
				hinge.Movement.AngularVelocity = 0f;
				hinge.Movement.ActiveStop = 0;
			});
		}

		void IApi.OnInit(BallManager ballManager) => Init?.Invoke(this, EventArgs.Empty);

		void IApi.OnDestroy()
		{
		}

		bool IApiColliderGenerator.IsColliderAvailable => _colliderComponent && _colliderComponent.IsCollidable;

		void IApiColliderGenerator.CreateColliders(ref ColliderReference colliders,
			float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			if (!_colliderComponent || !_colliderComponent.IsCollidable) {
				return;
			}
			colliders.Add(SpringHingeColliderGenerator.Create(_component, _colliderComponent,
				GetColliderInfo(ItemType.Invalid), margin));
		}

		ColliderInfo IApiColliderGenerator.GetColliderInfo() => GetColliderInfo(ItemType.Invalid);
		ColliderInfo IApiColliderGenerator.GetColliderInfo(ItemType itemType) => GetColliderInfo(itemType);

		private ColliderInfo GetColliderInfo(ItemType itemType)
		{
			if (!_colliderComponent) {
				return new ColliderInfo { ItemId = _itemId, ItemType = itemType };
			}
			var material = !_colliderComponent.OverwritePhysics && _colliderComponent.PhysicsMaterial
				? new PhysicsMaterialData {
					Elasticity = _colliderComponent.PhysicsMaterial.Elasticity,
					ElasticityFalloff = _colliderComponent.PhysicsMaterial.ElasticityFalloff,
					Friction = _colliderComponent.PhysicsMaterial.Friction,
					ScatterAngleRad = 0f,
					UseElasticityOverVelocity = _colliderComponent.PhysicsMaterial.UseElasticityOverVelocity,
					UseFrictionOverVelocity = _colliderComponent.PhysicsMaterial.UseFrictionOverVelocity
				}
				: new PhysicsMaterialData {
					Elasticity = _colliderComponent.Elasticity,
					ElasticityFalloff = _colliderComponent.ElasticityFalloff,
					Friction = _colliderComponent.Friction,
					ScatterAngleRad = 0f
				};
			if (_physicsEngine && !_colliderComponent.OverwritePhysics && _colliderComponent.PhysicsMaterial) {
				if (material.UseElasticityOverVelocity
				    && !_physicsEngine.ElasticityOverVelocityLUTs.ContainsKey(_itemId)) {
					_physicsEngine.ElasticityOverVelocityLUTs.Add(_itemId,
						_colliderComponent.PhysicsMaterial.GetElasticityOverVelocityLUT());
				}
				if (material.UseFrictionOverVelocity
				    && !_physicsEngine.FrictionOverVelocityLUTs.ContainsKey(_itemId)) {
					_physicsEngine.FrictionOverVelocityLUTs.Add(_itemId,
						_colliderComponent.PhysicsMaterial.GetFrictionOverVelocityLUT());
				}
			}
			return new ColliderInfo {
				ItemId = _itemId,
				ItemType = itemType,
				Material = material
			};
		}
	}
}
