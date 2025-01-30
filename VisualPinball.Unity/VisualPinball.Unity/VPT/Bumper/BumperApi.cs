﻿// Visual Pinball Engine
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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	public class BumperApi : CollidableApi<BumperComponent, BumperColliderComponent, BumperData>,
		IApi, IApiHittable, IApiSwitchDevice, IApiSwitch, IApiCoil, IApiCoilDevice, IApiWireDeviceDest
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball enters the bumper area.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		/// <summary>
		/// Event emitted when the ball leaves the bumper area.
		/// </summary>
		public event EventHandler<HitEventArgs> UnHit;

		/// <summary>
		/// Event emitted when the trigger is switched on or off.
		/// </summary>
		public event EventHandler<SwitchEventArgs> Switch;

		/// <summary>
		/// Event emitted when the bumper coil is turned on or off.
		/// </summary>
		public event EventHandler<NoIdCoilEventArgs> CoilStatusChanged;

		private int _switchColliderId;

		public BumperApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
		}

		#region Wiring

		public bool IsSwitchEnabled => SwitchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus) => AddSwitchDest(switchConfig, switchStatus);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => this;
		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => this;

		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig);
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);
		void IApiCoil.OnCoil(bool enabled)
		{
			if (enabled) {
				ref var bumperState = ref PhysicsEngine.BumperState(ItemId);
				bumperState.RingAnimation.IsHit = true;

				ref var insideOfs = ref PhysicsEngine.InsideOfs;
				var idsOfBallsInColl = insideOfs.GetIdsOfBallsInsideItem(ItemId);
				foreach (var ballId in idsOfBallsInColl) {
					if (!PhysicsEngine.Balls.ContainsKey(ballId)) {
						continue;
					}
					ref var ballState = ref PhysicsEngine.BallState(ballId);
					float3 bumperPos = MainComponent.Position;
					float3 ballPos = ballState.Position;
					var bumpDirection = ballPos - bumperPos;
					bumpDirection.z = 0f;
					bumpDirection = math.normalize(bumpDirection);
					var collEvent = new CollisionEventData {
						HitTime = 0f,
						HitNormal = bumpDirection,
						HitVelocity = new float2(bumpDirection.x, bumpDirection.y) * ColliderComponent.Force,
						HitDistance = 0f,
						HitFlag = false,
						HitOrgNormalVelocity = math.dot(bumpDirection, math.normalize(ballState.Velocity)),
						IsContact = true,
						ColliderId = _switchColliderId,
						IsKinematic = false,
						BallId = ballId
					};
					var physicsMaterialData = ColliderComponent.PhysicsMaterialData;
					var random = PhysicsEngine.Random;
					BallCollider.Collide3DWall(ref ballState, in physicsMaterialData, in collEvent, in bumpDirection, ref random);
					ballState.Velocity += bumpDirection * ColliderComponent.Force;
				}
			}

			CoilStatusChanged?.Invoke(this, new NoIdCoilEventArgs(enabled));
		}

		void IApiWireDest.OnChange(bool enabled) => (this as IApiCoil).OnCoil(enabled);

		#endregion

		#region Collider Generation

		protected override bool FireHitEvents => ColliderComponent.HitEvent;
		protected override float HitThreshold => ColliderComponent.Threshold;

		protected override void CreateColliders(ref ColliderReference colliders,
			float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			var height = MainComponent.Position.z;
			var switchCollider = new CircleCollider(new float2(0), MainComponent.Radius, height, height + 100f, GetColliderInfo(), ColliderType.Bumper);
			var rigidCollider = new CircleCollider(new float2(0), MainComponent.Radius * 0.5f, height, height + 100f, GetColliderInfo(), ColliderType.Circle);
			_switchColliderId = colliders.Add(switchCollider, translateWithinPlayfieldMatrix);
			colliders.Add(rigidCollider, translateWithinPlayfieldMatrix);
		}

		#endregion

		#region Events

		void IApi.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}

		void IApiHittable.OnHit(int ballId, bool isUnHit)
		{
			ref var insideOfs = ref PhysicsEngine.InsideOfs;
			if (isUnHit) {
				UnHit?.Invoke(this, new HitEventArgs(ballId));
				if (insideOfs.IsEmpty(ItemId)) { // Last ball just left
					Switch?.Invoke(this, new SwitchEventArgs(false, ballId));
					OnSwitch(false);
				}
			} else {
				Hit?.Invoke(this, new HitEventArgs(ballId));
				if (insideOfs.GetInsideCount(ItemId) == 1) { // Must've been empty before
					ref var bumperState = ref PhysicsEngine.BumperState(ItemId);
					bumperState.SkirtAnimation.HitEvent = true;
					bumperState.RingAnimation.IsHit = true;
					ref var ballState = ref PhysicsEngine.BallState(ballId);
					bumperState.SkirtAnimation.BallPosition = ballState.Position;
					Switch?.Invoke(this, new SwitchEventArgs(true, ballId));
					OnSwitch(true);
				}
			}
		}

		#endregion
	}
}
