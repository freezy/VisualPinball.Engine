﻿// Visual Pinball Engine
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

namespace VisualPinball.Unity
{
	/// <summary>
	/// The scripting API of the flipper.
	/// </summary>
	[Api]
	public class FlipperApi : CollidableApi<FlipperComponent, FlipperColliderComponent, FlipperData>,
		IApi, IApiHittable, IApiRotatable, IApiCollidable, IApiSwitchDevice, IApiSwitch, IApiCoilDevice, IApiWireDeviceDest
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

		void IApi.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);

			_mainCoil = new DeviceCoil(OnMainCoilEnabled, OnMainCoilDisabled);
			_holdCoil = new DeviceCoil(OnHoldCoilEnabled, OnHoldCoilDisabled);
		}

		void IApi.OnDestroy()
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

		internal float StartAngle
		{
			set {
				var staticData = EntityManager.GetComponentData<FlipperStaticData>(Entity);
				staticData.AngleStart = value;
				EntityManager.SetComponentData(Entity, staticData);
			}
		}

		#region Coil Handling

		private DeviceCoil _mainCoil;
		private DeviceCoil _holdCoil;

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => Coil(deviceItem);

		private IApiCoil Coil(string deviceItem) {
			return deviceItem switch {
				FlipperComponent.MainCoilItem => _mainCoil,
				FlipperComponent.HoldCoilItem => _holdCoil,
				_ => throw new ArgumentException($"Unknown flipper coil \"{deviceItem}\". Valid names are: [ \"{FlipperComponent.MainCoilItem}\", \"{FlipperComponent.HoldCoilItem}\" ].")
			};
		}

		private void OnMainCoilEnabled() => OnCoil(true, false);
		private void OnMainCoilDisabled() => OnCoil(false, false);
		private void OnHoldCoilEnabled() => OnCoil(true, true);
		private void OnHoldCoilDisabled() => OnCoil(false, true);

		private void OnCoil(bool enabled, bool isHoldCoil)
		{
			if (MainComponent.IsDualWound) {
				OnDualWoundCoil(enabled, isHoldCoil);
			} else {
				OnSingleWoundCoil(enabled);
			}
		}

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

		#endregion

		#region Wiring

		public bool IsSwitchEnabled => SwitchHandler.IsEnabled;
		IApiSwitchStatus IApiSwitch.AddSwitchDest(SwitchConfig switchConfig) => AddSwitchDest(switchConfig);
		void IApiSwitch.AddWireDest(WireDestConfig wireConfig) => AddWireDest(wireConfig);
		void IApiSwitch.RemoveWireDest(string destId) => RemoveWireDest(destId);

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => this;

		#endregion

		#region Events

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

		protected override void CreateColliders(List<ICollider> colliders, float margin)
		{
			var height = MainComponent.PositionZ;
			var baseRadius = math.max(MainComponent.BaseRadius, 0.01f);
			var hitCircleBase = new CircleCollider(
				MainComponent.Position,
				baseRadius,
				height,
				height + MainComponent.Height,
				GetColliderInfo()
			);

			colliders.Add(new FlipperCollider(hitCircleBase, MainComponent.FlipperRadiusMax, MainComponent.BaseRadius, MainComponent.EndRadius, MainComponent.StartAngle, MainComponent.EndAngle, GetColliderInfo()));
		}

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
