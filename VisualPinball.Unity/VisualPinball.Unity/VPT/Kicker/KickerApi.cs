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

using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Table;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity
{
	public class KickerApi : ItemApi<Kicker, KickerData>, IApiInitializable, IApiHittable,
		IApiSwitch, IApiCoil, IApiColliderGenerator
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball moves into the kicker.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		/// <summary>
		/// Event emitted when the ball leaves the kicker.
		/// </summary>
		public event EventHandler<HitEventArgs> UnHit;

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		public event EventHandler<SwitchEventArgs> Switch;

		public KickerApi(Kicker item, Entity entity, Entity parentEntity, Player player) : base(item, entity, parentEntity, player)
		{
		}

		public void CreateBall()
		{
			BallManager.CreateBall(Item, 25f, 1f, Entity);
		}

		public void CreateSizedBallWithMass(float radius, float mass)
		{
			BallManager.CreateBall(Item, radius, mass, Entity);
		}

		public void CreateSizedBall(float radius)
		{
			BallManager.CreateBall(Item, radius, 1f, Entity);
		}

		public void Kick()
		{
			SimulationSystemGroup.QueueAfterBallCreation(() => KickXYZ(Table, Entity, Data.Angle, Data.Speed, 0, 0, 0, 0));
		}


		public void Kick(float angle, float speed, float inclination = 0)
		{
			SimulationSystemGroup.QueueAfterBallCreation(() => KickXYZ(Table, Entity, angle, speed, inclination, 0, 0, 0));
		}

		/// <summary>
		/// Queues the ball to be destroyed at the next cycle.
		/// </summary>
		///
		/// <remarks>
		/// If there is not ball in the kicker, this does nothing.
		/// </remarks>
		public void DestroyBall()
		{
			var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			var kickerCollisionData = entityManager.GetComponentData<KickerCollisionData>(Entity);
			var ballEntity = kickerCollisionData.BallEntity;
			(this as IApiSwitch).DestroyBall(ballEntity);
		}


		void IApiSwitch.DestroyBall(Entity ballEntity)
		{
			if (ballEntity != Entity.Null) {
				BallManager.DestroyEntity(ballEntity);
				SimulationSystemGroup.QueueAfterBallCreation(OnBallDestroyed);
			}
		}

		void IApiCoil.OnCoil(bool enabled, bool _)
		{
			if (enabled) {
				Kick();
			}
		}
		void IApiWireDest.OnChange(bool enabled) => (this as IApiCoil).OnCoil(enabled, false);

		private void OnBallDestroyed()
		{
			var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			var kickerCollisionData = entityManager.GetComponentData<KickerCollisionData>(Entity);
			var ballEntity = kickerCollisionData.BallEntity;
			if (ballEntity != Entity.Null) {

				// update kicker status
				kickerCollisionData.BallEntity = Entity.Null;
				entityManager.SetComponentData(Entity, kickerCollisionData);
			}
		}

		private static void KickXYZ(Table table, Entity kickerEntity, float angle, float speed, float inclination, float x, float y, float z)
		{
			var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			var kickerCollisionData = entityManager.GetComponentData<KickerCollisionData>(kickerEntity);
			var kickerStaticData = entityManager.GetComponentData<KickerStaticData>(kickerEntity);
			var ballEntity = kickerCollisionData.BallEntity;
			if (ballEntity != Entity.Null) {
				var angleRad = math.radians(angle); // yaw angle, zero is along -Y axis

				if (math.abs(inclination) > (float) (System.Math.PI / 2.0)) {
					// radians or degrees?  if greater PI/2 assume degrees
					inclination *= (float) (System.Math.PI / 180.0); // convert to radians
				}

				// if < 0 use global value
				var scatterAngle = kickerStaticData.Scatter < 0.0f ? 0.0f : math.radians(kickerStaticData.Scatter);
				scatterAngle *= table.Data.GlobalDifficulty; // apply difficulty weighting

				if (scatterAngle > 1.0e-5f) { // ignore near zero angles
					var scatter = new Random().NextFloat(-1f, 1f); // -1.0f..1.0f
					scatter *= (1.0f - scatter * scatter) * 2.59808f * scatterAngle; // shape quadratic distribution and scale
					angleRad += scatter;
				}

				var speedZ = math.sin(inclination) * speed;
				if (speedZ > 0.0f) {
					speed *= math.cos(inclination);
				}

				// update ball data
				var ballData = entityManager.GetComponentData<BallData>(ballEntity);
				ballData.Position = new float3(
					ballData.Position.x + x,
					ballData.Position.y + y,
					ballData.Position.z + z
				);
				ballData.Velocity = new float3(
					math.sin(angleRad) * speed,
					-math.cos(angleRad) * speed,
					speedZ
				);
				ballData.IsFrozen = false;
				ballData.AngularMomentum = float3.zero;
				entityManager.SetComponentData(ballEntity, ballData);

				// update collision event
				var collEvent = entityManager.GetComponentData<CollisionEventData>(ballEntity);
				collEvent.HitDistance = 0.0f;
				collEvent.HitTime = -1.0f;
				collEvent.HitNormal = float3.zero;
				collEvent.HitVelocity = float2.zero;
				collEvent.HitFlag = false;
				collEvent.IsContact = false;
				entityManager.SetComponentData(ballEntity, collEvent);

				// update kicker status
				kickerCollisionData.BallEntity = Entity.Null;
				entityManager.SetComponentData(kickerEntity, kickerCollisionData);
			}
		}

		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig) => AddSwitchDest(switchConfig.WithPulse(Item.IsPulseSwitch));
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig.WithPulse(Item.IsPulseSwitch));
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);

		#region Collider Generation

		public override bool IsColliderEnabled => Data.IsEnabled;
		Entity IApiColliderGenerator.ColliderEntity => Entity;

		void IApiColliderGenerator.CreateColliders(Table table, List<ICollider> colliders)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y) * table.GetScaleZ();

			// reduce the hit circle radius because only the inner circle of the kicker should start a hit event
			var radius = Data.Radius * (Data.LegacyMode ? Data.FallThrough ? 0.75f : 0.6f : 1f);

			colliders.Add(new CircleCollider(Data.Center.ToUnityFloat2(), radius, height,
				height + Data.HitHeight, GetColliderInfo(), ColliderType.KickerCircle));
		}

		ColliderInfo IApiColliderGenerator.GetColliderInfo() => GetColliderInfo();

		#endregion

		#region Events

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApiHittable.OnHit(Entity ballEntity, bool isUnHit)
		{
			if (isUnHit) {
				UnHit?.Invoke(this, new HitEventArgs(ballEntity));
				Switch?.Invoke(this, new SwitchEventArgs(false, ballEntity));
				OnSwitch(false);

			} else {
				Hit?.Invoke(this, new HitEventArgs(ballEntity));
				Switch?.Invoke(this, new SwitchEventArgs(true, ballEntity));
				OnSwitch(true);
			}
		}

		#endregion
	}
}
