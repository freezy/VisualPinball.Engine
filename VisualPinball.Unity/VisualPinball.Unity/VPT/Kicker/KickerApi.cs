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

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Kicker;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity
{
	public class KickerApi : CollidableApi<KickerComponent, KickerColliderComponent, KickerData>,
		IApi, IApiHittable, IApiSwitch, IApiSwitchDevice, IApiCoilDevice, IApiWireDeviceDest
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

		internal float3 Position => MainComponent.Position;

		public KickerDeviceCoil KickerCoil => _coils.Values.FirstOrDefault();

		private readonly Dictionary<string, KickerDeviceCoil> _coils = new Dictionary<string, KickerDeviceCoil>();

		public KickerApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
			foreach (var coil in MainComponent.Coils) {
				_coils[coil.Id] = new KickerDeviceCoil(player, coil, this);
			}
		}

		void IApi.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}

		public void CreateBall(GameObject ballPrefab = null, float radius = 25f, float mass = 1f)
		{
			var ballId = BallManager.CreateBall(MainComponent, radius, mass, ballPrefab);

			ref var ball = ref PhysicsEngine.BallState(ballId);
			ref var kickerState = ref PhysicsEngine.KickerState(ItemId);
			var events = PhysicsEngine.EventQueue;
			ball.CollisionEvent.HitFlag = true; // HACK: avoid capture leaving kicker

			KickerCollider.Collide(new float3(kickerState.Static.Center, kickerState.Static.ZLow), ref ball, ref events, ref PhysicsEngine.InsideOfs, ref kickerState.Collision,
				in kickerState.Static, in kickerState.CollisionMesh, in ball.CollisionEvent, ItemId, true);
		}

		public void CreateSizedBallWithMass(float radius, float mass)
		{
			BallManager.CreateBall(MainComponent, radius, mass);
		}

		public void Kick(float angle, float speed, float inclination = 0)
		{
			KickXYZ(angle, speed, inclination, 0, 0, 0);
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
			ref var kickerState = ref PhysicsEngine.KickerState(ItemId);
			if (kickerState.Collision.HasBall) {
				BallManager.DestroyBall(kickerState.Collision.BallId);
				OnBallDestroyed();
			}
		}

		/// <summary>
		/// Checks whether the kicker contains a ball.
		/// </summary>
		/// <returns>True if there is a ball in the kicker, false otherwise.</returns>
		public bool HasBall()
		{
			ref var kickerState = ref PhysicsEngine.KickerState(ItemId);
			return kickerState.Collision.HasBall;
		}

		internal ref BallState GetBallData()
		{
			ref var kickerState = ref PhysicsEngine.KickerState(ItemId);
			return ref PhysicsEngine.BallState(kickerState.Collision.BallId);
		}

		internal int BallId {
			get {
				ref var kickerState = ref PhysicsEngine.KickerState(ItemId);
				return kickerState.Collision.BallId;
			}
		}

		#region Wiring

		public bool IsSwitchEnabled => SwitchHandler.IsEnabled;

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;
		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);

		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => Coil(deviceItem);

		public IApiCoil Coil() => _coils.Values.FirstOrDefault();

		private IApiCoil Coil(string deviceItem)
		{
			if (_coils.ContainsKey(deviceItem)) {
				return _coils[deviceItem];
			}

			throw new ArgumentException($"Unknown coil \"{deviceItem}\". Valid names are [ {string.Join(", ", _coils.Select(item => $"\"{item.Key}\""))} ].");
		}

		private void OnBallDestroyed()
		{
			ref var kickerState = ref PhysicsEngine.KickerState(ItemId);
			if (kickerState.Collision.HasBall) {
				kickerState.Collision.BallId = 0;
			}
		}

		#endregion

		private void KickXYZ(float angle, float speed, float inclination, float x, float y, float z)
		{
			ref var kickerState = ref PhysicsEngine.KickerState(ItemId);
			var ballId = kickerState.Collision.BallId;
			if (ballId != 0) {
				var angleRad = math.radians(angle); // yaw angle, zero is along -Y axis

				if (math.abs(inclination) > (float) (System.Math.PI / 2.0)) {
					// radians or degrees?  if greater PI/2 assume degrees
					inclination *= (float) (System.Math.PI / 180.0); // convert to radians
				}

				// if < 0 use global value
				var scatterAngle = kickerState.Static.Scatter < 0.0f ? 0.0f : math.radians(kickerState.Static.Scatter);
				scatterAngle *= TableComponent.GlobalDifficulty; // apply difficulty weighting

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
				ref var ballData = ref PhysicsEngine.BallState(ballId);
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

				// update collision event
				ref var collEvent = ref ballData.CollisionEvent;
				collEvent.HitDistance = 0.0f;
				collEvent.HitTime = -1.0f;
				collEvent.HitNormal = float3.zero;
				collEvent.HitVelocity = float2.zero;
				collEvent.HitFlag = false;
				collEvent.IsContact = false;

				// update kicker status
				kickerState.Collision.BallId = 0;
			}
		}

		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus) => AddSwitchDest(switchConfig.WithPulse(false), switchStatus);
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig.WithPulse(false));
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);

		#region Collider Generation

		protected override void CreateColliders(ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			// reduce the hit circle radius because only the inner circle of the kicker should start a hit event
			var radius = MainComponent.Radius * (ColliderComponent.LegacyMode ? ColliderComponent.FallThrough ? 0.75f : 0.6f : 1f);

			colliders.Add(new CircleCollider(float2.zero, radius, 0,
				ColliderComponent.HitHeight, GetColliderInfo(), ColliderType.KickerCircle), translateWithinPlayfieldMatrix);
		}

		#endregion

		#region Events

		void IApiHittable.OnHit(int ballId, bool isUnHit)
		{
			if (isUnHit) {
				UnHit?.Invoke(this, new HitEventArgs(ballId));
				Switch?.Invoke(this, new SwitchEventArgs(false, ballId));
				OnSwitch(false);

			} else {
				Hit?.Invoke(this, new HitEventArgs(ballId));
				Switch?.Invoke(this, new SwitchEventArgs(true, ballId));
				OnSwitch(true);
			}
		}

		#endregion
	}

	public class KickerDeviceCoil : DeviceCoil
	{
		public readonly KickerCoil Coil;
		private readonly KickerApi _kickerApi;

		public KickerDeviceCoil(Player player, KickerCoil coil, KickerApi api) : base(player)
		{
			Coil = coil;
			_kickerApi = api;
			OnEnable = Kick;
		}

		private void Kick() => _kickerApi.Kick(Coil.Angle, Coil.Speed, Coil.Inclination);

	}
}
