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
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity
{
	public sealed class NudgeSensorConfig
	{
		public NudgeSensorType Type = NudgeSensorType.GamepadIntent;
		public float Strength = 1f;
		public float CabinetMassKg = 113f;
		public SensorMapping X = new();
		public SensorMapping Y = new();
		public SensorMapping AccelerationX = new();
		public SensorMapping AccelerationY = new();
		public SensorMapping VelocityX = new();
		public SensorMapping VelocityY = new();

		public void Normalize()
		{
			Strength = math.clamp(Strength, 0f, 2f);
			CabinetMassKg = math.clamp(CabinetMassKg <= 0f ? 113f : CabinetMassKg, 0f, 200f);
			X ??= new SensorMapping();
			Y ??= new SensorMapping();
			AccelerationX ??= new SensorMapping();
			AccelerationY ??= new SensorMapping();
			VelocityX ??= new SensorMapping();
			VelocityY ??= new SensorMapping();
		}

		internal NudgeSensorRuntimeConfig ToRuntimeConfig()
		{
			Normalize();
			return new NudgeSensorRuntimeConfig {
				Type = Type,
				Strength = Strength,
				CabinetMassKg = CabinetMassKg,
				XMapped = X.IsMapped ? (byte)1 : (byte)0,
				YMapped = Y.IsMapped ? (byte)1 : (byte)0,
				AccelerationXMapped = AccelerationX.IsMapped ? (byte)1 : (byte)0,
				AccelerationYMapped = AccelerationY.IsMapped ? (byte)1 : (byte)0,
				VelocityXMapped = VelocityX.IsMapped ? (byte)1 : (byte)0,
				VelocityYMapped = VelocityY.IsMapped ? (byte)1 : (byte)0
			};
		}
	}

	public sealed class NudgeSystem : IDisposable
	{
		private readonly PhysicsEngine _physicsEngine;
		private readonly object _configLock = new();
		private readonly List<NudgeSensorConfig> _sensors = new(NudgeState.MaxSensors);
		private volatile NativeInputManager _inputManager;

		internal NudgeSystem(PhysicsEngine physicsEngine)
		{
			_physicsEngine = physicsEngine;
		}

		public int SensorCount
		{
			get {
				lock (_configLock) {
					return _sensors.Count;
				}
			}
		}

		public IReadOnlyList<NativeInputDeviceInfo> ListDevices()
		{
			var inputManager = _inputManager;
			return inputManager == null ? Array.Empty<NativeInputDeviceInfo>() : inputManager.ListDevices();
		}

		public void ConfigureSensors(IReadOnlyList<NudgeSensorConfig> sensors)
		{
			lock (_configLock) {
				_sensors.Clear();
				if (sensors != null) {
					for (var i = 0; i < sensors.Count && i < NudgeState.MaxSensors; i++) {
						var sensor = sensors[i] ?? new NudgeSensorConfig();
						sensor.Normalize();
						_sensors.Add(sensor);
					}
				}

				_physicsEngine.ConfigureNudgeSensorCount(_sensors.Count);
				for (var i = 0; i < _sensors.Count; i++) {
					_physicsEngine.ConfigureNudgeSensor(i, _sensors[i].ToRuntimeConfig());
				}
			}
		}

		internal void AttachNativeInputManager(NativeInputManager inputManager)
		{
			if (_inputManager == inputManager) {
				return;
			}
			DetachNativeInputManager();
			_inputManager = inputManager;
			if (_inputManager != null) {
				_inputManager.AxisInputReceived += OnAxisInputReceived;
			}
		}

		internal void DetachNativeInputManager()
		{
			if (_inputManager != null) {
				_inputManager.AxisInputReceived -= OnAxisInputReceived;
				_inputManager = null;
			}
		}

		public void Dispose()
		{
			DetachNativeInputManager();
		}

		// Called on the native input polling thread: only lock-free reads of the
		// device-id snapshot here, never device (re-)enumeration.
		private void OnAxisInputReceived(NativeInputApi.InputEvent evt)
		{
			if (evt.EventType != (int)NativeInputApi.InputEventType.Axis) {
				return;
			}
			var inputManager = _inputManager;
			if (inputManager == null || !inputManager.TryGetDeviceId(evt.DeviceIndex, out var deviceId)) {
				return;
			}

			lock (_configLock) {
				for (var i = 0; i < _sensors.Count; i++) {
					var sensor = _sensors[i];
					if (sensor.Type == NudgeSensorType.GamepadIntent) {
						TryQueueSample(i, sensor.X, NudgeSensorChannel.X, deviceId, evt);
						TryQueueSample(i, sensor.Y, NudgeSensorChannel.Y, deviceId, evt);
					} else {
						TryQueueSample(i, sensor.VelocityX, NudgeSensorChannel.VelocityX, deviceId, evt);
						TryQueueSample(i, sensor.VelocityY, NudgeSensorChannel.VelocityY, deviceId, evt);
						TryQueueSample(i, sensor.AccelerationX, NudgeSensorChannel.AccelerationX, deviceId, evt);
						TryQueueSample(i, sensor.AccelerationY, NudgeSensorChannel.AccelerationY, deviceId, evt);
					}
				}
			}
		}

		private void TryQueueSample(int sensorIndex, SensorMapping mapping, NudgeSensorChannel channel, string deviceId,
			NativeInputApi.InputEvent evt)
		{
			if (mapping == null || !mapping.IsMapped || mapping.AxisId != evt.AxisId || mapping.DeviceId != deviceId) {
				return;
			}
			var mappedValue = mapping.ProcessRawValue(evt.Value, evt.TimestampUsec);
			_physicsEngine.EnqueueNudgeSensorSample(sensorIndex, channel, mappedValue, (ulong)evt.TimestampUsec);
		}
	}
}
