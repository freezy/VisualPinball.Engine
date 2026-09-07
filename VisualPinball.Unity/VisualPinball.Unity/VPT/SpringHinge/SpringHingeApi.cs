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
	public class SpringHingeApi : IApi, IApiColliderGenerator, IApiHittable, IApiSwitchDevice
	{
		private readonly SpringHingeComponent _component;
		private readonly PhysicsEngine _physicsEngine;
		private readonly int _itemId;
		private readonly SpringHingeColliderComponent _colliderComponent;
		private readonly DeviceSwitch _angleSwitch;
		private bool _angleSwitchClosed;

		public event EventHandler Init;
		public event EventHandler<HitEventArgs> Hit;

		internal SpringHingeApi(SpringHingeComponent component, PhysicsEngine physicsEngine)
		{
			_component = component;
			_physicsEngine = physicsEngine;
			_itemId = component.ItemId;
			_colliderComponent = component.GetComponent<SpringHingeColliderComponent>();
			var player = component.GetComponentInParent<Player>();
			_angleSwitch = new DeviceSwitch(SpringHingeComponent.AngleSwitchItem,
				false, SwitchDefault.NormallyOpen, player, physicsEngine);
		}

		internal float Angle => _component.PublishedAngle;

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem)
			=> deviceItem == SpringHingeComponent.AngleSwitchItem
				? _angleSwitch
				: throw new ArgumentException($"Unknown spring-hinge switch \"{deviceItem}\". Valid name is \"{SpringHingeComponent.AngleSwitchItem}\".");

		internal void OnAngleChanged(float angle)
		{
			if (!_component.EnableAngleSwitch) {
				return;
			}
			var angleDegrees = math.degrees(angle);
			if (!_angleSwitchClosed && angleDegrees >= _component.SwitchCloseAngle) {
				_angleSwitchClosed = true;
				_angleSwitch.SetSwitch(true);
			} else if (_angleSwitchClosed && angleDegrees <= _component.SwitchOpenAngle) {
				_angleSwitchClosed = false;
				_angleSwitch.SetSwitch(false);
			}
		}

		public void Reset(float angle)
		{
			if (!_physicsEngine) {
				return;
			}
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.SpringHingeStates.ContainsKey(_itemId)) {
					return;
				}
				MagnetPhysics.ReleaseOwnedAttachmentsForHinge(_itemId, ref state);
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

		void IApiHittable.OnHit(int ballId, bool isUnHit)
		{
			if (!isUnHit) {
				Hit?.Invoke(this, new HitEventArgs(ballId));
			}
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
				FireEvents = _colliderComponent.HitEvent,
				HitThreshold = _colliderComponent.HitThreshold,
				Material = material
			};
		}
	}
}
