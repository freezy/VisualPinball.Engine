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
using Unity.Mathematics;
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
		// written by the simulation thread's coil dispatch, read by the main thread
		private volatile bool _isEnabled;
		private readonly HashSet<int> _heldBalls = new();

		public event EventHandler Init;
		public event EventHandler<HitEventArgs> BallEntered;
		public event EventHandler<HitEventArgs> BallExited;
		public event EventHandler<HitEventArgs> BallGrabbed;
		public event EventHandler<HitEventArgs> BallReleased;

		internal MagnetApi(GameObject go, Player player, PhysicsEngine physicsEngine)
		{
			_component = go.GetComponentInChildren<MagnetComponent>();
			_player = player;
			_physicsEngine = physicsEngine;
			_itemId = UnityObjectId.Get(_component.gameObject);
			_isEnabled = _component.IsEnabledOnStart;

			_magnetCoil = new DeviceCoil(_player, onValue: OnCoilValue, onValueSimulationThread: OnCoilValueSimulationThread);
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
				var radius = math.max(0f, value);
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

		public void Eject(float speed, float angleDeg, float verticalAngleDeg = 0f)
		{
			if (!_physicsEngine) {
				return;
			}
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.MagnetStates.ContainsKey(_itemId)) {
					return;
				}
				ref var magnet = ref state.MagnetStates.GetValueByRef(_itemId);
				MagnetPhysics.EjectGrabbedBalls(_itemId, ref magnet, ref state, speed, angleDeg, verticalAngleDeg);
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

		/// <summary>
		/// Sets the normalized electrical command in [0..1] (e.g. Iron Man's ROM
		/// pulses the magnet coils). Physical profiles integrate this command into
		/// effective current; VPX Compatible applies it immediately.
		/// </summary>
		private void OnCoilValue(float value)
		{
			if (!_physicsEngine) {
				return;
			}
			var normalizedValue = math.saturate(value);
			var enabled = normalizedValue > 0f;
			_isEnabled = enabled;
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (state.MagnetStates.ContainsKey(_itemId)) {
					ref var magnet = ref state.MagnetStates.GetValueByRef(_itemId);
					magnet.IsEnabled = enabled;
					magnet.CommandedPower = normalizedValue;
				}
			});
		}

		private void OnCoilValueSimulationThread(float value)
		{
			var normalizedValue = math.saturate(value);
			var enabled = normalizedValue > 0f;
			_isEnabled = enabled;
			if (!_physicsEngine) {
				return;
			}
			ref var magnet = ref _physicsEngine.MagnetState(_itemId);
			magnet.IsEnabled = enabled;
			magnet.CommandedPower = normalizedValue;
		}

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
					magnet.CommandedPower = enabled ? 1f : 0f;
				}
			});
		}

		void IApiMagnetEvents.OnMagnetBallEntered(int ballId) => BallEntered?.Invoke(this, new HitEventArgs(ballId));
		void IApiMagnetEvents.OnMagnetBallExited(int ballId) => BallExited?.Invoke(this, new HitEventArgs(ballId));

		void IApiMagnetEvents.OnMagnetBallGrabbed(int ballId)
		{
			_heldBalls.Add(ballId);
			_ballHeldSwitch.SetSwitch(true);
			BallGrabbed?.Invoke(this, new HitEventArgs(ballId));
		}

		void IApiMagnetEvents.OnMagnetBallReleased(int ballId)
		{
			_heldBalls.Remove(ballId);
			if (_heldBalls.Count == 0) {
				_ballHeldSwitch.SetSwitch(false);
			}
			BallReleased?.Invoke(this, new HitEventArgs(ballId));
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
