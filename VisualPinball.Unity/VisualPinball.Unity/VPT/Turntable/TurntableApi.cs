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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	public class TurntableApi : IApi, IApiCoilDevice, IApiWireDeviceDest
	{
		private readonly TurntableComponent _component;
		private readonly Player _player;
		private readonly PhysicsEngine _physicsEngine;
		private readonly int _itemId;

		private readonly DeviceCoil _motorCoil;
		private readonly DeviceCoil _directionCoil;
		private bool _motorOn;
		private bool _spinClockwise;

		public event EventHandler Init;

		internal TurntableApi(GameObject go, Player player, PhysicsEngine physicsEngine)
		{
			_component = go.GetComponentInChildren<TurntableComponent>();
			_player = player;
			_physicsEngine = physicsEngine;
			_itemId = UnityObjectId.Get(_component.gameObject);
			_motorOn = _component.MotorOnStart;
			_spinClockwise = _component.SpinClockwise;

			_motorCoil = new DeviceCoil(_player, OnMotorCoilEnabled, OnMotorCoilDisabled, OnMotorCoilEnabledSimThread, OnMotorCoilDisabledSimThread);
			_directionCoil = new DeviceCoil(_player, OnDirectionCoilEnabled, OnDirectionCoilDisabled, OnDirectionCoilEnabledSimThread, OnDirectionCoilDisabledSimThread);
		}

		public bool MotorOn {
			get => _motorOn;
			set => SetMotorOn(value);
		}

		public bool SpinClockwise {
			get => _spinClockwise;
			set => SetSpinClockwise(value);
		}

		public float Speed {
			get => _component.PublishedSpeed;
		}

		public float RotationAngle {
			get => _component.PublishedRotationAngle;
		}

		public float MaxSpeed {
			get => _component.MaxSpeed;
			set {
				_component.MaxSpeed = value;
				MutateState((ref TurntableState turntable) => {
					turntable.MaxSpeed = value;
					TurntablePhysics.RefreshTargetSpeed(ref turntable);
				});
			}
		}

		public float SpinUp {
			get => _component.SpinUp;
			set {
				var spinUp = math.max(0f, value);
				_component.SpinUp = spinUp;
				MutateState((ref TurntableState turntable) => turntable.SpinUp = spinUp);
			}
		}

		public float SpinDown {
			get => _component.SpinDown;
			set {
				var spinDown = math.max(0f, value);
				_component.SpinDown = spinDown;
				MutateState((ref TurntableState turntable) => turntable.SpinDown = spinDown);
			}
		}

		void IApi.OnInit(BallManager ballManager)
		{
			SetMotorOn(_component.MotorOnStart);
			SetSpinClockwise(_component.SpinClockwise);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => Coil(deviceItem);

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch {
				TurntableComponent.MotorCoilItem => _motorCoil,
				TurntableComponent.DirectionCoilItem => _directionCoil,
				_ => throw new ArgumentException($"Unknown turntable coil \"{deviceItem}\". Valid names are \"{TurntableComponent.MotorCoilItem}\" and \"{TurntableComponent.DirectionCoilItem}\".")
			};
		}

		private void OnMotorCoilEnabled() => SetMotorOn(true);
		private void OnMotorCoilDisabled() => SetMotorOn(false);
		private void OnMotorCoilEnabledSimThread() => SetMotorOnSimThread(true);
		private void OnMotorCoilDisabledSimThread() => SetMotorOnSimThread(false);
		private void OnDirectionCoilEnabled() => SetSpinClockwise(true);
		private void OnDirectionCoilDisabled() => SetSpinClockwise(false);
		private void OnDirectionCoilEnabledSimThread() => SetSpinClockwiseSimThread(true);
		private void OnDirectionCoilDisabledSimThread() => SetSpinClockwiseSimThread(false);

		private void SetMotorOn(bool motorOn)
		{
			_motorOn = motorOn;
			MutateState((ref TurntableState turntable) => turntable.MotorOn = motorOn);
		}

		private void SetSpinClockwise(bool spinClockwise)
		{
			_spinClockwise = spinClockwise;
			MutateState((ref TurntableState turntable) => {
				turntable.SpinClockwise = spinClockwise;
				TurntablePhysics.RefreshTargetSpeed(ref turntable);
			});
		}

		private void SetMotorOnSimThread(bool motorOn)
		{
			_motorOn = motorOn;
			if (!_physicsEngine) {
				return;
			}
			ref var turntable = ref _physicsEngine.TurntableState(_itemId);
			turntable.MotorOn = motorOn;
		}

		private void SetSpinClockwiseSimThread(bool spinClockwise)
		{
			_spinClockwise = spinClockwise;
			if (!_physicsEngine) {
				return;
			}
			ref var turntable = ref _physicsEngine.TurntableState(_itemId);
			turntable.SpinClockwise = spinClockwise;
			TurntablePhysics.RefreshTargetSpeed(ref turntable);
		}

		private delegate void TurntableStateMutation(ref TurntableState turntable);

		private void MutateState(TurntableStateMutation mutation)
		{
			if (!_physicsEngine) {
				return;
			}
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.TurntableStates.ContainsKey(_itemId)) {
					return;
				}
				ref var turntable = ref state.TurntableStates.GetValueByRef(_itemId);
				mutation(ref turntable);
			});
		}
	}
}
