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

// ReSharper disable EventNeverSubscribedTo.Global
#pragma warning disable 67

using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The scripting API of the flipper.
	/// </summary>
	[Api]
	public class FlipperApi : ItemCollidableApi<FlipperAuthoring, FlipperColliderAuthoring, Flipper, FlipperData>, IApiInitializable, IApiHittable,
		IApiRotatable, IApiCollidable, IApiSwitch, IApiCoil, IApiColliderGenerator
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the flipper was touched by the ball, but did
		/// not collide.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		/// <summary>
		/// Event emitted when the flipper collided with the ball.
		/// </summary>
		public event EventHandler<CollideEventArgs> Collide;

		/// <summary>
		/// Event emitted when the flipper comes to rest, i.e. moves back to
		/// the resting position.
		/// </summary>
		public event EventHandler<RotationEventArgs> LimitBos;

		/// <summary>
		/// Event emitted when the flipper reaches its end position.
		/// </summary>
		public event EventHandler<RotationEventArgs> LimitEos;

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		public event EventHandler<SwitchEventArgs> Switch;

		// todo
		public event EventHandler Timer;

		private bool _isEos;

		internal FlipperApi(GameObject go, Entity entity, Entity parentEntity, Player player)
			: base(go, entity, parentEntity, player)
		{
		}

		/// <summary>
		/// Enables the flipper's solenoid, making the flipper to start moving
		/// to its end position.
		/// </summary>
		public void RotateToEnd()
		{
			EngineProvider<IPhysicsEngine>.Get().FlipperRotateToEnd(Entity);
		}

		/// <summary>
		/// Disables the flipper's solenoid, making the flipper rotate back to
		/// its resting position.
		/// </summary>
		public void RotateToStart()
		{
			EngineProvider<IPhysicsEngine>.Get().FlipperRotateToStart(Entity);
		}

		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig) => AddSwitchDest(switchConfig.WithPulse(MainComponent.IsPulseSwitch));
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig.WithPulse(MainComponent.IsPulseSwitch));
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);
		void IApiSwitch.DestroyBall(Entity ballEntity) => DestroyBall(ballEntity);

		void IApiCoil.OnCoil(bool enabled, bool isHoldCoil)
		{
			if (Data.IsDualWound) {
				OnDualWoundCoil(enabled, isHoldCoil);
			} else {
				OnSingleWoundCoil(enabled);
			}
		}
		void IApiWireDest.OnChange(bool enabled) => (this as IApiCoil).OnCoil(enabled, false);

		private void OnSingleWoundCoil(bool enabled)
		{
			if (enabled) {
				RotateToEnd();
			} else {
				RotateToStart();
			}
		}

		private void OnDualWoundCoil(bool enabled, bool isHoldCoil)
		{
			if (enabled) {
				if (!isHoldCoil) {
					RotateToEnd();
				}

			} else {
				if (_isEos && isHoldCoil) {
					_isEos = false;
					Switch?.Invoke(this, new SwitchEventArgs(false, Entity.Null));
					OnSwitch(false);
					RotateToStart();
				}

				if (!_isEos && !isHoldCoil) {
					RotateToStart();
				}
			}
		}

		#region Events

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(Entity ballEntity, bool _)
		{
			Hit?.Invoke(this, new HitEventArgs(ballEntity));
		}

		void IApiRotatable.OnRotate(float speed, bool direction)
		{
			if (direction) {
				_isEos = true;
				LimitEos?.Invoke(this, new RotationEventArgs { AngleSpeed = speed });
				Switch?.Invoke(this, new SwitchEventArgs(true, Entity.Null));
				OnSwitch(true);

			} else {
				LimitBos?.Invoke(this, new RotationEventArgs { AngleSpeed = speed });
			}
		}

		void IApiCollidable.OnCollide(Entity ballEntity, float hit)
		{
			Collide?.Invoke(this, new CollideEventArgs { FlipperHit = hit });
		}

		#endregion

		#region Collider Generation

		Entity IApiColliderGenerator.ColliderEntity => Entity;

		void IApiColliderGenerator.CreateColliders(Table table, List<ICollider> colliders)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			var baseRadius = math.max(Data.BaseRadius, 0.01f);
			var hitCircleBase = new CircleCollider(
				Data.Center.ToUnityFloat2(),
				baseRadius,
				height,
				height + Data.Height,
				GetColliderInfo()
			);

			colliders.Add(new FlipperCollider(hitCircleBase, Data.FlipperRadius, Data.EndRadius, GetColliderInfo()));
		}

		ColliderInfo IApiColliderGenerator.GetColliderInfo() => GetColliderInfo();

		protected override PhysicsMaterialData GetPhysicsMaterial(PhysicsMaterial physicsMaterial) => new PhysicsMaterialData {
			ElasticityFalloff = Data.OverridePhysics != 0
				? Data.OverrideElasticityFalloff
				: Data.ElasticityFalloff,
			Elasticity = Data.OverridePhysics != 0
				? Data.OverrideElasticity
				: Data.Elasticity,
			Friction = Data.OverridePhysics != 0
				? Data.OverrideFriction
				: Data.Friction,
			ScatterAngleRad = math.radians(Data.OverridePhysics != 0
				? Data.OverrideScatterAngle
				: Data.Scatter
			)
		};

		#endregion

	}


	/// <summary>
	/// Event data when the ball collides with the flipper.
	/// </summary>
	public struct CollideEventArgs
	{
		/// <summary>
		/// The relative normal velocity with which the flipper was hit.
		/// </summary>
		public float FlipperHit;
	}
}
