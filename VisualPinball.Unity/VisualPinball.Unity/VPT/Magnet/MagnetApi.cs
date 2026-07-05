// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	public class MagnetApi : IApi, IApiCoilDevice, IApiSwitchDevice, IApiWireDeviceDest, IApiMagnetEvents
	{
		private readonly MagnetComponent _component;
		private readonly Player _player;
		private readonly PhysicsEngine _physicsEngine;
		private readonly int _itemId;

		private DeviceCoil _magnetCoil;
		private DeviceSwitch _ballHeldSwitch;
		private bool _isEnabled;
		private readonly HashSet<int> _heldBalls = new();

		public event EventHandler Init;
		public event EventHandler<BallEventArgs> BallEntered;
		public event EventHandler<BallEventArgs> BallExited;
		public event EventHandler<BallEventArgs> BallGrabbed;
		public event EventHandler<BallEventArgs> BallReleased;

		internal MagnetApi(GameObject go, Player player, PhysicsEngine physicsEngine)
		{
			_component = go.GetComponentInChildren<MagnetComponent>();
			_player = player;
			_physicsEngine = physicsEngine;
			_itemId = UnityObjectId.Get(_component.gameObject);
			_isEnabled = _component.IsEnabledOnStart;

			_magnetCoil = new DeviceCoil(_player, OnCoilEnabled, OnCoilDisabled, OnCoilEnabledSimThread, OnCoilDisabledSimThread);
			_ballHeldSwitch = new DeviceSwitch(MagnetComponent.BallHeldSwitchItem, false, SwitchDefault.NormallyOpen, _player, _physicsEngine);
		}

		public bool IsEnabled {
			get => _isEnabled;
			set => SetEnabled(value);
		}

		public float Strength {
			get => _component.Strength;
			set {
				_component.Strength = value;
				if (!_physicsEngine) {
					return;
				}
				_physicsEngine.MutateState((ref PhysicsState state) => {
					if (state.MagnetStates.ContainsKey(_itemId)) {
						ref var magnet = ref state.MagnetStates.GetValueByRef(_itemId);
						magnet.Strength = value;
					}
				});
			}
		}

		public float Radius {
			get => _component.Radius;
			set {
				var radius = Mathf.Max(0f, value);
				_component.Radius = radius;
				if (!_physicsEngine) {
					return;
				}
				var radiusVpx = MagnetComponent.MillimetersToVpx(radius);
				_physicsEngine.MutateState((ref PhysicsState state) => {
					if (state.MagnetStates.ContainsKey(_itemId)) {
						ref var magnet = ref state.MagnetStates.GetValueByRef(_itemId);
						magnet.Radius = radiusVpx;
					}
				});
			}
		}

		public void ReleaseBall()
		{
			if (!_physicsEngine) {
				return;
			}
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.MagnetStates.ContainsKey(_itemId)) {
					return;
				}
				ref var magnet = ref state.MagnetStates.GetValueByRef(_itemId);
				MagnetPhysics.ReleaseGrabbedBalls(_itemId, ref magnet, ref state, true);
			});
		}

		public void Eject(float speed, float angleDeg)
		{
			if (!_physicsEngine) {
				return;
			}
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.MagnetStates.ContainsKey(_itemId)) {
					return;
				}
				ref var magnet = ref state.MagnetStates.GetValueByRef(_itemId);
				MagnetPhysics.EjectGrabbedBalls(_itemId, ref magnet, ref state, speed, angleDeg);
			});
		}

		void IApi.OnInit(BallManager ballManager)
		{
			SetEnabled(_component.IsEnabledOnStart);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => Coil(deviceItem);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => Switch(deviceItem);

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch {
				MagnetComponent.MagnetCoilItem => _magnetCoil,
				_ => throw new ArgumentException($"Unknown magnet coil \"{deviceItem}\". Valid name is \"{MagnetComponent.MagnetCoilItem}\".")
			};
		}

		private IApiSwitch Switch(string deviceItem)
		{
			return deviceItem switch {
				MagnetComponent.BallHeldSwitchItem => _ballHeldSwitch,
				_ => throw new ArgumentException($"Unknown magnet switch \"{deviceItem}\". Valid name is \"{MagnetComponent.BallHeldSwitchItem}\".")
			};
		}

		private void OnCoilEnabled() => SetEnabled(true);
		private void OnCoilDisabled() => SetEnabled(false);
		private void OnCoilEnabledSimThread() => SetEnabledSimThread(true);
		private void OnCoilDisabledSimThread() => SetEnabledSimThread(false);

		private void SetEnabled(bool enabled)
		{
			_isEnabled = enabled;
			if (!_physicsEngine) {
				return;
			}
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (state.MagnetStates.ContainsKey(_itemId)) {
					ref var magnet = ref state.MagnetStates.GetValueByRef(_itemId);
					magnet.IsEnabled = enabled;
				}
			});
		}

		private void SetEnabledSimThread(bool enabled)
		{
			_isEnabled = enabled;
			if (!_physicsEngine) {
				return;
			}
			ref var magnet = ref _physicsEngine.MagnetState(_itemId);
			magnet.IsEnabled = enabled;
		}

		void IApiMagnetEvents.OnMagnetBallEntered(int ballId) => BallEntered?.Invoke(this, new BallEventArgs(ballId));
		void IApiMagnetEvents.OnMagnetBallExited(int ballId) => BallExited?.Invoke(this, new BallEventArgs(ballId));

		void IApiMagnetEvents.OnMagnetBallGrabbed(int ballId)
		{
			_heldBalls.Add(ballId);
			_ballHeldSwitch.SetSwitch(true);
			BallGrabbed?.Invoke(this, new BallEventArgs(ballId));
		}

		void IApiMagnetEvents.OnMagnetBallReleased(int ballId)
		{
			_heldBalls.Remove(ballId);
			if (_heldBalls.Count == 0) {
				_ballHeldSwitch.SetSwitch(false);
			}
			BallReleased?.Invoke(this, new BallEventArgs(ballId));
		}
	}

	internal interface IApiMagnetEvents
	{
		void OnMagnetBallEntered(int ballId);
		void OnMagnetBallExited(int ballId);
		void OnMagnetBallGrabbed(int ballId);
		void OnMagnetBallReleased(int ballId);
	}
}
