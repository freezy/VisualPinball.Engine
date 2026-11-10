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
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Main-thread configuration of one analog plunger sensor.
	/// </summary>
	/// <remarks>
	/// The position channel reads 0 at rest and +1 fully retracted after its
	/// mapping (center, dead zone, limit, scale) is applied; forward of rest is
	/// negative. The optional velocity channel is in position units per second,
	/// negative towards the ball.
	/// </remarks>
	public sealed class PlungerSensorConfig
	{
		public bool Enabled = true;
		public SensorMapping Position = new() { Kind = SensorMappingKind.Position };
		public SensorMapping Velocity = new() { Kind = SensorMappingKind.Velocity };

		public bool IsPositionMapped => Position != null && Position.IsMapped;
		public bool IsVelocityMapped => Velocity != null && Velocity.IsMapped;

		/// <summary>
		/// Guarantees non-null mappings of the right kind.
		/// </summary>
		public void Normalize()
		{
			Position ??= new SensorMapping();
			Velocity ??= new SensorMapping();
			Position.Kind = SensorMappingKind.Position;
			Velocity.Kind = SensorMappingKind.Velocity;
		}
	}

	/// <summary>
	/// Bridges native analog input events to the physics engine's plunger input.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Native input is polled off the simulation thread. This system keeps a
	/// locked snapshot of the sensor mappings, maps matching axis events and
	/// queues the normalized samples, with their native timestamps, into the
	/// physics engine. The filtering itself runs in the physics loop.
	/// </para>
	/// <para>
	/// One slot after the configured sensors is reserved for the Unity Input
	/// System's analog plunger action, which is the fallback when native input
	/// is not polling. Like any sensor it only becomes active once it moves.
	/// </para>
	/// </remarks>
	public sealed class PlungerSensorSystem : IDisposable
	{
		private readonly PhysicsEngine _physicsEngine;
		private readonly object _configLock = new();
		private readonly List<PlungerSensorConfig> _sensors = new(PlungerInputState.MaxSensors);
		private readonly bool[] _sensorAvailable = new bool[PlungerInputState.MaxSensors];
		private volatile NativeInputManager _inputManager;
		private HashSet<string> _connectedDevices = new();
		private int _unityFallbackIndex = -1;
		private long _lastUnityTimestampUsec = long.MinValue;
		private float _lastUnityValue = float.NaN;
		private bool _gateSubscribed;

		internal PlungerSensorSystem(PhysicsEngine physicsEngine)
		{
			_physicsEngine = physicsEngine;
			ConfigureSensors(Array.Empty<PlungerSensorConfig>());
		}

		/// <summary>Number of configured sensors, without the Unity fallback slot.</summary>
		public int SensorCount
		{
			get {
				lock (_configLock) {
					return _sensors.Count;
				}
			}
		}

		/// <summary>
		/// Replaces all configured sensors and pushes their runtime shape to the
		/// physics engine. Staged samples of the old mappings are dropped.
		/// </summary>
		public void ConfigureSensors(IReadOnlyList<PlungerSensorConfig> sensors)
		{
			lock (_configLock) {
				_sensors.Clear();
				if (sensors != null) {
					for (var i = 0; i < sensors.Count && i < PlungerInputState.MaxSensors; i++) {
						var sensor = sensors[i] ?? new PlungerSensorConfig();
						sensor.Normalize();
						_sensors.Add(sensor);
					}
				}
				_unityFallbackIndex = _sensors.Count < PlungerInputState.MaxSensors ? _sensors.Count : -1;

				var count = _unityFallbackIndex >= 0 ? _sensors.Count + 1 : _sensors.Count;
				var positionMapped = new bool[count];
				var velocityMapped = new bool[count];
				var enabled = new bool[count];
				var available = new bool[count];
				for (var i = 0; i < _sensors.Count; i++) {
					positionMapped[i] = _sensors[i].IsPositionMapped;
					velocityMapped[i] = _sensors[i].IsVelocityMapped;
					enabled[i] = _sensors[i].Enabled;
					_sensorAvailable[i] = IsSensorAvailable(_sensors[i]);
					available[i] = _sensorAvailable[i];
				}
				if (_unityFallbackIndex >= 0) {
					positionMapped[_unityFallbackIndex] = true;
					enabled[_unityFallbackIndex] = true;
					available[_unityFallbackIndex] = true;
				}
				_physicsEngine.ApplyPlungerSensorConfigurations(positionMapped, velocityMapped, enabled, available);
			}
		}

		/// <summary>
		/// Feeds a value of the Unity Input System's analog plunger action, 0 at
		/// rest and 1 fully retracted. Ignored while native input polls, which
		/// reads the same devices through explicit mappings.
		/// </summary>
		/// <param name="timeSeconds">Event time on the Input System clock.</param>
		/// <returns>False if the sample was ignored.</returns>
		/// <remarks>
		/// Every plunger with an analog action reports the same event, so a
		/// repeat of the last value at the same time is dropped.
		/// </remarks>
		internal bool SubmitUnityAnalogValue(float value, double timeSeconds)
		{
			if (NativeInputManager.TryGetExistingInstance()?.IsPolling == true) {
				return false;
			}
			var timestampUsec = (long)(timeSeconds * 1000000.0);
			int index;
			lock (_configLock) {
				index = _unityFallbackIndex;
				if (index < 0 || timestampUsec == _lastUnityTimestampUsec && value == _lastUnityValue) {
					return false;
				}
				_lastUnityTimestampUsec = timestampUsec;
				_lastUnityValue = value;
			}
			_physicsEngine.EnqueuePlungerSensorSample(index, PlungerSensorChannel.Position, value, timestampUsec);
			return true;
		}

		/// <summary>
		/// Subscribes to native axis and device events of an input manager.
		/// </summary>
		internal void AttachNativeInputManager(NativeInputManager inputManager)
		{
			if (_inputManager == inputManager) {
				return;
			}
			DetachNativeInputManager();
			_inputManager = inputManager;
			if (_inputManager != null) {
				_inputManager.AxisInputReceived += OnAxisInputReceived;
				_inputManager.DevicesChanged += OnDevicesChanged;
				NativeInputManager.GameplayInputGateChanged += OnGameplayInputGateChanged;
				_gateSubscribed = true;
				OnDevicesChanged();
			}
		}

		/// <summary>
		/// Detaches the given input manager if it is the attached one.
		/// </summary>
		internal void DetachNativeInputManager(NativeInputManager inputManager)
		{
			var attached = _inputManager;
			if (attached == null || attached != inputManager) {
				return;
			}
			DetachNativeInputManager();
		}

		private void DetachNativeInputManager()
		{
			if (_gateSubscribed) {
				NativeInputManager.GameplayInputGateChanged -= OnGameplayInputGateChanged;
				_gateSubscribed = false;
			}
			if (_inputManager != null) {
				_inputManager.AxisInputReceived -= OnAxisInputReceived;
				_inputManager.DevicesChanged -= OnDevicesChanged;
				_inputManager = null;
				UpdateConnectedDevices(Array.Empty<NativeInputDeviceInfo>());
			}
		}

		public void Dispose()
		{
			DetachNativeInputManager();
		}

		// Native input polling thread: only lock-free device-id lookups here.
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
				if (_inputManager != inputManager) {
					return;
				}
				for (var i = 0; i < _sensors.Count; i++) {
					var sensor = _sensors[i];
					if (!sensor.Enabled || !_sensorAvailable[i]) {
						continue;
					}
					TryQueueSample(i, sensor.Position, PlungerSensorChannel.Position, deviceId, evt);
					TryQueueSample(i, sensor.Velocity, PlungerSensorChannel.Velocity, deviceId, evt);
				}
			}
		}

		private void TryQueueSample(int sensorIndex, SensorMapping mapping, PlungerSensorChannel channel, string deviceId,
			NativeInputApi.InputEvent evt)
		{
			if (mapping == null || !mapping.IsMapped || mapping.AxisId != evt.AxisId || mapping.DeviceId != deviceId) {
				return;
			}
			var value = mapping.ProcessRawValue(evt.Value, evt.TimestampUsec);
			_physicsEngine.EnqueuePlungerSensorSample(sensorIndex, channel, value, evt.TimestampUsec);
		}

		private void OnGameplayInputGateChanged(bool open)
		{
			// Samples stop while the gate is closed, and the plunger may have moved
			// in the meantime. Restart the filters instead of reading the jump as
			// a release.
			_physicsEngine.EnqueuePlungerClockReset();
		}

		private void OnDevicesChanged()
		{
			var inputManager = _inputManager;
			if (inputManager == null) {
				return;
			}
			var devices = inputManager.ListDevices();
			lock (_configLock) {
				if (_inputManager == inputManager) {
					UpdateConnectedDevices(devices);
				}
			}
		}

		// Device notifications run on the input thread. Availability changes are
		// queued with the samples instead of taking the physics lock here.
		internal void UpdateConnectedDevices(IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			lock (_configLock) {
				var connected = new HashSet<string>();
				foreach (var device in devices) {
					if (device.IsConnected) {
						connected.Add(device.Id);
					}
				}
				_connectedDevices = connected;
				for (var i = 0; i < _sensors.Count; i++) {
					var available = IsSensorAvailable(_sensors[i]);
					if (available == _sensorAvailable[i]) {
						continue;
					}
					_sensorAvailable[i] = available;
					_physicsEngine.EnqueuePlungerSensorAvailability(i, available && _sensors[i].Enabled);
				}
			}
		}

		private bool IsSensorAvailable(PlungerSensorConfig sensor)
		{
			return IsMappingAvailable(sensor.Position) && IsMappingAvailable(sensor.Velocity);
		}

		private bool IsMappingAvailable(SensorMapping mapping)
		{
			return mapping == null || !mapping.IsMapped || _connectedDevices.Contains(mapping.DeviceId);
		}
	}
}
